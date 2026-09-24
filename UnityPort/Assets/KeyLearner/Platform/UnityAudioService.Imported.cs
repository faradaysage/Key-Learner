using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KeyLearner.Unity.Platform
{
    public sealed partial class UnityAudioService
    {
        sealed class ImportedRequest
        {
            public ResourceRequest Request;
            public bool Wanted = true;
        }
        readonly Dictionary<string, ImportedRequest> importedRequests = new Dictionary<string, ImportedRequest>();
        readonly HashSet<AudioClip> importedClips = new HashSet<AudioClip>();
        bool importedSpeechPending;

        AudioClip LoadImportedClip(string path, bool speech)
        {
            if (!clips.TryGetValue(path, out var clip))
            {
                var resource = AndroidContent.AudioResource(path, speech);
                if (!speech) clip = Resources.Load<AudioClip>(resource);
                else
                {
                    if (!importedRequests.TryGetValue(path, out var pending))
                    {
                        pending = new ImportedRequest { Request = Resources.LoadAsync<AudioClip>(resource) };
                        importedRequests.Add(path, pending);
                        pending.Request.completed += operation => {
                            if (!pending.Wanted || disposed) ReleaseAbandonedRequest(path, pending);
                        };
                    }
                    pending.Wanted = true;
                    if (!pending.Request.isDone) { importedSpeechPending = true; return null; }
                    clip = pending.Request.asset as AudioClip;
                    importedRequests.Remove(path);
                }
                if (!clip) return null;
                clips.Add(path, clip);
                importedClips.Add(clip);
                if (speech) speechUse[path] = ++speechClock;
            }
            if (!speech) return clip;
            if (clip.loadState == AudioDataLoadState.Unloaded && !clip.LoadAudioData()) { ReleaseImportedClip(path,clip); return null; }
            if (clip.loadState == AudioDataLoadState.Loading) { importedSpeechPending = true; return null; }
            if (clip.loadState == AudioDataLoadState.Loaded) return clip;
            ReleaseImportedClip(path,clip);
            return null;
        }
        void ReleaseAbandonedRequest(string path, ImportedRequest pending)
        {
            if (importedRequests.TryGetValue(path, out var current) && current == pending)
                importedRequests.Remove(path);
            var clip = pending.Request.asset as AudioClip;
            if (clip && !importedClips.Contains(clip)) Resources.UnloadAsset(clip);
        }
        void ReleaseImportedClip(string path, AudioClip clip)
        {
            speechUse.Remove(path);
            clips.Remove(path);
            if (importedClips.Remove(clip)) { clip.UnloadAudioData(); Resources.UnloadAsset(clip); }
            else UnityEngine.Object.Destroy(clip);
        }
        void ReleaseUnusedImportedSpeech()
        {
            foreach (var pair in importedRequests.ToArray())
                if (!pair.Value.Wanted && pair.Value.Request.isDone) ReleaseAbandonedRequest(pair.Key, pair.Value);
            foreach (var path in speechUse.Keys.ToArray())
            {
                if (!clips.TryGetValue(path, out var clip)) continue;
                if (keys.Concat(words).Any(r => r.Asset?.Path == path || (!r.SkipRecording && r.Recording == path))) continue;
                if (lanes.Any(l => l.Request != null && l.Source && l.Source.clip == clip)) continue;
                foreach (var lane in lanes) if (lane.Source && lane.Source.clip == clip) lane.Source.clip = null;
                ReleaseImportedClip(path,clip);
            }
        }
    }
}
