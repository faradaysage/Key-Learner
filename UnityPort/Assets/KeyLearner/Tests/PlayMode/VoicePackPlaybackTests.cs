using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using KeyLearner.Studio;
using KeyLearner.Unity.Platform;
using NUnit.Framework;
using UnityEngine;

namespace KeyLearner.Unity.Tests.PlayMode
{
    // Small isolated PCM fixtures test the real decoder/queue/AudioSource path, not narration quality.
    public sealed class VoicePackPlaybackTests
    {
        string root;
        GameObject owner;
        UnityAudioService audio;
        Settings settings;
        [SetUp] public void SetUp()
        {
            root=Path.Combine(Path.GetTempPath(),"KeyLearner-VoicePlayback-"+Guid.NewGuid().ToString("N"));
            Pack(root,1000);Pack(Path.Combine(root,"first"),2000);Pack(Path.Combine(root,"second"),4000);
            File.WriteAllText(Path.Combine(root,"voice-packs.json"),"{\"schema\":1,\"status\":\"complete\",\"defaultVoiceId\":\"first\",\"voices\":["+
                "{\"voiceId\":\"first\",\"displayName\":\"First\",\"packDirectory\":\"first\",\"catalog\":\"catalog.json\",\"readyForIntegration\":true},"+
                "{\"voiceId\":\"second\",\"displayName\":\"Second\",\"packDirectory\":\"second\",\"catalog\":\"catalog.json\",\"readyForIntegration\":true}]}");
            owner=new GameObject("Isolated voice playback");
            audio=new UnityAudioService(owner,Path.Combine(root,"profile"),new VoicePackRegistry(root));
            settings=new Settings{Sound=true,Volume=50,VoicePackId="first"};
        }
        [TearDown] public void TearDown()
        {
            audio?.Dispose();if(owner)UnityEngine.Object.DestroyImmediate(owner);
            var full=Path.GetFullPath(root);var temp=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(full.StartsWith(temp,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(full).StartsWith("KeyLearner-VoicePlayback-",StringComparison.Ordinal))Directory.Delete(full,true);
        }
        [Test] public void AudioDisposeToleratesDestroyedOwner()
        {
            audio.SayKey("number-three", settings);
            audio.Update(settings);
            UnityEngine.Object.DestroyImmediate(owner);
            Assert.DoesNotThrow(() => audio.Dispose());
            Assert.DoesNotThrow(() => audio.Dispose());
        }
        [Test] public void FireCleanupToleratesDestroyedParent()
        {
            var parent = new GameObject("Destroyed fire parent");
            var type = typeof(Suite).Assembly.GetType("KeyLearner.Unity.CanvasFireRenderer");
            var renderer = Activator.CreateInstance(type, new object[] { parent.transform });
            try
            {
                UnityEngine.Object.DestroyImmediate(parent);
                Assert.DoesNotThrow(() => type.GetMethod("Clear").Invoke(renderer, null));
            }
            finally { ((IDisposable)renderer).Dispose(); if (parent) UnityEngine.Object.DestroyImmediate(parent); }
        }
        sealed class MenuActivityProbe : Minigame
        {
            public int Progress = 3;
            public bool Suspended;
            public override void Tick(float dt) { Progress++; }
            public override void Suspend() { Suspended = true; Progress = 0; }
        }
        [Test] public void SharedMenuPreservesActivityAndQueuedVoice()
        {
            var panel = new GameObject("Inactive menu harness");
            panel.transform.SetParent(owner.transform);
            panel.SetActive(false); // Never start the player or acquire Windows input hooks.
            var suite = panel.AddComponent<Suite>();
            var game = new MenuActivityProbe();
            var services = new GameServices { Audio = audio };
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(Suite).GetField("services", flags).SetValue(suite, services);
            typeof(Suite).GetField("game", flags).SetValue(suite, game);
            typeof(Suite).GetField("ready", flags).SetValue(suite, true);
            float priorTime = Time.timeScale;
            bool priorPause = AudioListener.pause;
            AudioListener.pause = false;
            try
            {
                audio.SayKey("number-three", settings);
                suite.OpenGameMenu();
                suite.OpenGameMenu(); // Repeated input must not overwrite the saved audio state.
                Assert.IsTrue(suite.GameMenuOpen);
                Assert.AreEqual(0, Time.timeScale);
                Assert.IsTrue(AudioListener.pause);
                Assert.IsFalse(game.Suspended);
                Assert.AreEqual(3, game.Progress);
                audio.Update(settings);
                Assert.AreEqual(1, audio.Pending);
                Assert.AreEqual(0, audio.Started);
                typeof(Suite).GetMethod("CloseGameMenu", flags).Invoke(suite, null);
                Assert.IsFalse(suite.GameMenuOpen);
                Assert.IsFalse(AudioListener.pause);
                Assert.AreEqual(3, game.Progress);
                audio.Update(settings);
                Assert.AreEqual(1, audio.PreparedStarted);
            }
            finally { Time.timeScale = priorTime; AudioListener.pause = priorPause; }
        }
        [Test] public void MenuPausePreservesQueuedNarrationUntilResume()
        {
            audio.SayKey("number-three", settings);
            audio.SetMenuPaused(true);
            audio.Update(settings);
            Assert.AreEqual(0, audio.Started, "Opening the menu must not start queued narration");
            Assert.AreEqual(1, audio.Pending, "Pause must retain the queued phrase");
            audio.SetMenuPaused(false);
            audio.Update(settings);
            Assert.AreEqual(1, audio.PreparedStarted);
            Assert.AreEqual("number-three", audio.LastSpeechKey);
        }
        [Test] public void SwitchingAndPreviewUseDistinctClipsImmediatelyWithoutPersistingCandidate()
        {
            audio.SayKey("number-three",settings);audio.Update(settings);
            Assert.AreEqual("first",audio.LastVoicePackId);Assert.AreEqual("number-three",audio.LastSpeechKey);
            var first=owner.GetComponentsInChildren<AudioSource>().First(s=>s.clip&&s.clip.name.Contains("speech-number-three" )).clip;
            audio.SelectVoice(settings,"second");Assert.AreEqual(0,audio.Pending);
            audio.Say("Three",settings);audio.Update(settings);
            Assert.AreEqual("second",audio.LastVoicePackId);Assert.AreEqual("number-three",audio.LastSpeechKey);
            var second=owner.GetComponentsInChildren<AudioSource>().First(s=>s.clip&&s.clip.name.Contains("speech-number-three")).clip;
            Assert.AreNotSame(first,second,"A cached clip from the previous pack must not be reused");
            audio.PreviewVoice("first",settings);audio.Update(settings);
            Assert.AreEqual("second",settings.VoicePackId);Assert.AreEqual("first",audio.LastVoicePackId);
            Assert.AreEqual(VoicePackRegistry.PreviewKey,audio.LastSpeechKey);Assert.AreEqual(3,audio.PreparedStarted);Assert.AreEqual(0,audio.FallbackStarted);
            audio.SelectVoice(settings,"first");audio.SayKey("number-three",settings);audio.Update(settings);
            Assert.AreEqual("first",audio.LastVoicePackId);
            audio.StopSpeech();audio.SayKey("semantic-only",settings);audio.Update(settings);
            Assert.AreEqual("semantic-only",audio.LastSpeechKey);Assert.AreEqual(5,audio.PreparedStarted);
            Assert.AreEqual(0,audio.FallbackStarted,"Semantic-key requests must not depend on a textual alias");
        }
        [Test] public void MissingSelectedClipUsesSameKeyDefaultAndMissingBothNeverSynthesizesAnotherPhrase()
        {
            File.Delete(Path.Combine(root,"second","speech-number-three.wav"));
            audio.SelectVoice(settings,"second");audio.SayKey("number-three",settings);audio.Update(settings);
            Assert.AreEqual("first",audio.LastVoicePackId);Assert.AreEqual("number-three",audio.LastSpeechKey);Assert.AreEqual(1,audio.PreparedStarted);
            audio.Stop();File.Delete(Path.Combine(root,"first","speech-number-three.wav"));
            audio.SayKey("number-three",settings);audio.Update(settings);
            Assert.AreEqual(1,audio.PreparedStarted);Assert.AreEqual(0,audio.FallbackStarted);Assert.AreEqual(0,audio.Pending);
            audio.SayKey("unknown-key",settings);audio.Update(settings);Assert.AreEqual(0,audio.Pending);
        }
        [Test] public void ProductionPacksDecodeRepresentativeSemanticKeysAndOriginalRemainsFallback()
        {
            audio.Dispose();
            var registry=new VoicePackRegistry(Path.Combine(Application.streamingAssetsPath,"Content","Voice"));
            Assert.AreEqual("kyutai-Blake",registry.DefaultVoiceId);
            Assert.AreEqual(VoicePackRegistry.LegacyId,registry.FallbackVoiceId);
            CollectionAssert.IsSubsetOf(new[]{"Blake","Original narrator"},registry.Packs.Select(p=>p.DisplayName).ToArray());
            audio=new UnityAudioService(owner,Path.Combine(root,"production-profile"),registry);
            var keys=new[]{VoicePackRegistry.PreviewKey,"letter-d","letter-n","word-milk","word-apollo","word-athena","word-dave","number-003","number-100","cue-bird","cue-ocean","cue-racer","cue-how-many","cue-hop-help","cue-try-again","cue-well-done","cue-hundred"};
            foreach(var pack in registry.Packs)
            {
                audio.SelectVoice(settings,pack.Id);
                foreach(var key in keys)
                {
                    audio.StopSpeech();int before=audio.PreparedStarted;
                    audio.SayKey(key,settings);audio.Update(settings);
                    Assert.AreEqual(before+1,audio.PreparedStarted,pack.Id+" / "+key);
                    Assert.AreEqual(pack.Id,audio.LastVoicePackId);Assert.AreEqual(key,audio.LastSpeechKey);
                }
            }
            audio.StopSpeech();registry.Reject(registry.Resolve("word-milk",registry.DefaultVoiceId));
            audio.SelectVoice(settings,registry.DefaultVoiceId);audio.SayKey("word-milk",settings);audio.Update(settings);
            Assert.AreEqual(VoicePackRegistry.LegacyId,audio.LastVoicePackId);Assert.AreEqual("word-milk",audio.LastSpeechKey);
            Assert.AreEqual(0,audio.FallbackStarted);
        }
        static void Pack(string folder,short sample)
        {
            Directory.CreateDirectory(folder);
            var entries=new System.Collections.Generic.List<string>();
            foreach(var pair in new[]{new[]{"number-three","Three"},new[]{VoicePackRegistry.PreviewKey,"Hello explorer"},new[]{"semantic-only","Spoken text without alias"}})
            {
                var filename="speech-"+pair[0]+".wav";var path=Path.Combine(folder,filename);
                using(var writer=new BinaryWriter(File.Create(path)))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(9636);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                    writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(24000);writer.Write(48000);writer.Write((short)2);writer.Write((short)16);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(9600);for(int i=0;i<4800;i++)writer.Write((short)(i%48<24?sample:-sample));
                }
                string hash;using(var sha=SHA256.Create())using(var file=File.OpenRead(path))hash=BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();
                var alias=pair[0]=="semantic-only"?"separate alias":pair[1];
                entries.Add("\""+pair[0]+"\":{\"text\":\""+pair[1]+"\",\"file\":\""+filename+"\",\"sha256\":\""+hash+"\",\"aliases\":[\""+alias+"\"]}");
            }
            File.WriteAllText(Path.Combine(folder,"catalog.json"),"{\"clips\":{"+string.Join(",",entries)+"}}");
        }
    }
}
