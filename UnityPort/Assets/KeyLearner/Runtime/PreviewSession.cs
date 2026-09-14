using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using KeyLearner.Studio;
using UnityEngine;

namespace KeyLearner.Unity
{
    // Explicit preview-only UX replay. Never installs hooks or synthesizes OS keys.
    public sealed class PreviewSession : MonoBehaviour
    {
        Suite suite; string output; readonly List<string> passed = new List<string>();
        bool failed; int lostFocus, gainedFocus;
        public static void Attach(Suite suite)
        {
            if (!suite.Services.Preview || !suite.Services.Options.Has("--ux-verify"))
                return;
            var probe = suite.gameObject.AddComponent<PreviewSession>();
            probe.suite = suite;
            probe.output = Path.GetFullPath(suite.Services.Options.Value("--ux-verify"));
            Directory.CreateDirectory(probe.output);
            probe.StartCoroutine(probe.Run());
        }
        void Check(bool condition, string name)
        {
            if (condition)
            {
                passed.Add(name);
                Debug.Log("UX_PASS " + name);
                return;
            }
            failed = true;
            Debug.LogError("UX_FAIL " + name);
            Write("failed", name);
            suite.Quit();
        }
        void Write(string phase, string error = "")
        {
            var report = new
            {
                phase,
                error,
                passed,
                lostFocus,
                gainedFocus,
                focused = Application.isFocused,
                inputActive=suite.PreviewInputActive,
                worldTimeScale=Time.timeScale,
                suite = suite.PreviewState,
                parent = suite.PreviewParent.PreviewState
            };
            string path = Path.Combine(output, "ux-state.json"), temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(report));
            if (File.Exists(path))
                File.Delete(path);
            File.Move(temp, path);
        }
        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png"));
            yield return null;
            yield return null;
        }
        void ParentKey(int key)
        {
            var parent = suite.PreviewParent;
            parent.Key(new KeyEvent(key, true, suite.Services.Now));
            parent.Key(new KeyEvent(key, false, suite.Services.Now));
        }
        IEnumerator Run()
        {
            yield return null;
            yield return null;
            if(suite.Services.Options.Has("--expect-voice"))
            {
                Check(suite.Services.Settings.VoicePackId==suite.Services.Options.Value("--expect-voice"),"Player process restarted with persisted narrator");
                if(failed)yield break;
            }
            suite.Select(KeyLearner.Studio.PlayMode.SmashGarden);
            yield return null;
            var smash = suite.CurrentGame;
            suite.PreviewGameKey(27);
            Check(!suite.Picking && ReferenceEquals(smash, suite.CurrentGame), "single Escape keeps Smash Garden and shows parent reminder");
            if (failed)
                yield break;
            yield return Shot("smash-escape-reminder");
            suite.Select(KeyLearner.Studio.PlayMode.Subitizing);
            yield return null;
            var dots = suite.CurrentGame;
            suite.PreviewGameKey(27);
            Check(!suite.Picking && ReferenceEquals(dots, suite.CurrentGame), "single Escape keeps Dot Pop");
            if (failed)
                yield break;
            suite.Select(KeyLearner.Studio.PlayMode.HowManyNow);
            yield return null;
            suite.PreviewGameKey(27);
            Check(suite.Picking, "single Escape follows math Back route");
            if (failed)
                yield break;
            suite.OpenPicker();
            yield return Shot("picker-all");
            suite.PreviewPickerKey(9);
            yield return null;
            Check(suite.PreviewState.Contains("filter=1"), "picker Tab category");
            if (failed)
                yield break;
            yield return Shot("picker-explore");
            suite.PreviewPickerKey(39);
            suite.PreviewPickerKey(13);
            yield return null;
            Check((int)suite.Services.Settings.Mode == 4 && !suite.Picking, "picker Right and Enter choose Letter Racer");
            if (failed)
                yield break;
            suite.OpenPicker();
            suite.PreviewPickerKey(9);
            suite.PreviewPickerKey(9);
            suite.PreviewPickerKey(9);
            for (int i = 0; i < 7; i++)
                suite.PreviewPickerKey(39);
            Check(suite.PreviewState.Contains("page=1"), "picker keyboard reaches second page");
            if (failed)
                yield break;
            yield return Shot("picker-second-page");
            suite.PreviewPickerKey(13);
            suite.PreviewStudio(true);
            yield return null;
            Check(suite.PreviewState.Contains("studio=True"), "Parent Studio opens");
            if (failed)
                yield break;
            var parent = suite.PreviewParent;
            for (int tab = 0; tab < 9; tab++)
            {
                parent.PreviewTab(tab);
                yield return null;
                yield return Shot("studio-" + tab);
                Check(parent.PreviewState.StartsWith("tab=" + tab + " "), "Parent Studio tab " + tab);
                if (failed)
                    yield break;
            }
            if(suite.Services.Options.Has("--voice-verify"))
            {
                suite.Services.Settings.Sound=true;
                suite.Services.Settings.Volume=35;
                var registry=suite.Services.Store.SpeechPacks;
                var audio=suite.Services.Audio;
                var selectedBefore=suite.Services.Settings.VoicePackId;
                foreach(var pack in registry.Packs)
                {
                    parent.PreviewTab(1);
                    for(int attempt=0;attempt<registry.Packs.Count && suite.Services.Settings.VoicePackId!=pack.Id;attempt++)ParentKey(39);
                    Check(suite.Services.Settings.VoicePackId==pack.Id,"Options selects "+pack.DisplayName);
                    if(failed)yield break;
                    audio.StopSpeech();var started=audio.PreparedStarted;var fallback=audio.FallbackStarted;
                    ParentKey(32);
                    double deadline=Time.realtimeSinceStartupAsDouble+15;
                    while(audio.PreparedStarted==started && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                    Check(audio.PreparedStarted>started && audio.LastSpeechKey==VoicePackRegistry.PreviewKey && audio.LastVoicePackId==pack.Id,"Options preview decodes "+pack.DisplayName);
                    if(failed)yield break;
                    yield return Shot("voice-"+pack.Id);
                    while(audio.Pending>0 && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                    Check(audio.Pending==0 && audio.FallbackStarted==fallback,"Options preview completes without synthesis: "+pack.DisplayName);
                    if(failed)yield break;
                    suite.PreviewStudio(false);yield return null;
                    var restarted=new Store(suite.Services.Store.Root,speechRoot:Path.Combine(Application.streamingAssetsPath,"Content","Voice"));
                    Check(restarted.Settings.VoicePackId==pack.Id,"Restart reloads "+pack.DisplayName);
                    if(failed)yield break;
                    suite.Select(KeyLearner.Studio.PlayMode.Counting);yield return null;
                    audio.StopSpeech();started=audio.PreparedStarted;
                    suite.PreviewGameKey(49);
                    deadline=Time.realtimeSinceStartupAsDouble+10;
                    while(audio.PreparedStarted==started && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                    Check(audio.PreparedStarted>started && audio.LastVoicePackId==pack.Id,"Counting gameplay speaks with "+pack.DisplayName);
                    if(failed)yield break;
                    while(audio.Pending>0 && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                    suite.PreviewStudio(true);yield return null;
                }
                var finalChoice=suite.Services.Options.Has("--finish-voice")?suite.Services.Options.Value("--finish-voice"):selectedBefore;
                parent.PreviewTab(1);
                for(int attempt=0;attempt<registry.Packs.Count && suite.Services.Settings.VoicePackId!=finalChoice;attempt++)ParentKey(39);
                Check(suite.Services.Settings.VoicePackId==finalChoice,"Options leaves requested narrator selected for restart");
                if(failed)yield break;
            }
            parent.PreviewCredits();
            yield return Shot("studio-credits");
            parent.PreviewTab(0);
            yield return null;
            bool gentle = suite.Services.Settings.GentleMotion;
            ParentKey(40);
            ParentKey(40);
            ParentKey(40);
            ParentKey(39);
            Check(suite.Services.Settings.GentleMotion != gentle, "parent arrows edit gentle motion");
            if (failed)
                yield break;
            suite.PreviewStudio(false);
            yield return null;
            var saved = JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path.Combine(suite.Services.Store.Root, "settings.json")));
            Check(saved.GentleMotion == suite.Services.Settings.GentleMotion, "Parent Studio saves isolated profile");
            Check(saved.VoicePackId == suite.Services.Settings.VoicePackId, "Parent Studio saves stable narrator ID");
            if (failed)
                yield break;
            suite.PreviewStudio(true);
            parent.PreviewEdit();
            suite.PreviewFocusLoss();
            Check(parent.PreviewState.Contains("editing=False") && !suite.Services.Keys.Keys.Any(), "shared focus reset clears text editor and held keys");
            if (failed)
                yield break;
            if (suite.Services.Options.Has("--native-focus"))
            {
                double until = Time.unscaledTimeAsDouble + 15;
                while (!Application.isFocused && Time.unscaledTimeAsDouble < until)
                    yield return null;
                Check(Application.isFocused, "native player begins focused");
                if (failed)
                    yield break;
                parent.PreviewEdit();
                int beforeLost = lostFocus, beforeGained = gainedFocus;
                Write("await-native-focus-loss");
                until = Time.unscaledTimeAsDouble + 20;
                while (suite.PreviewInputActive && Time.unscaledTimeAsDouble < until)
                    yield return null;
                Check(!suite.PreviewInputActive, "actual minimized window suspended native input");
                if (failed)
                    yield break;
                Write("await-native-focus-return");
                until = Time.unscaledTimeAsDouble + 20;
                while (!suite.PreviewInputActive && Time.unscaledTimeAsDouble < until)
                    yield return null;
                Check(suite.PreviewInputActive, "actual restored window resumed native input");
                if (failed)
                    yield break;
                Check(parent.PreviewState.Contains("editing=False"), "native focus loss canceled unfinished parent edit");
                if (failed)
                    yield break;
                yield return Shot("studio-focus-recovered");
            }
            // Without the native-focus branch, the simulated transition needs its normal Update frame.
            yield return null;
            Check(Time.timeScale==0,"source animation and particles pause in Parent Studio");
            if(failed)yield break;
            suite.PreviewStudio(false);
            yield return null;
            Check(Time.timeScale==1,"world time resumes with gameplay");
            if(failed)yield break;
            Check(suite.PreviewState.Contains("studio=False"), "Parent Studio closes to play");
            if (failed)
                yield break;
            Write("complete");
            yield return null;
            suite.Quit();
        }
        void OnApplicationFocus(bool focused)
        {
            if (focused)
                gainedFocus++;
            else
                lostFocus++;
        }
    }
}
