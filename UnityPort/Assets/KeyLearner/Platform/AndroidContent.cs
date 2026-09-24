using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using KeyLearner.Studio;
using UnityEngine;

namespace KeyLearner.Unity.Platform
{
    /// <summary>String-only metadata for build-selected, Unity-imported Android audio.</summary>
    public static class AndroidContent
    {
        public const string ResourceRoot = "AndroidContent/";
        public static bool Enabled => Application.platform == RuntimePlatform.Android;
        public static string VoiceRoot => Path.Combine(Application.persistentDataPath, "imported-voice");
        public static string ReadText(string relative)
        {
            var asset = Resources.Load<TextAsset>(ResourceRoot + relative);
            if (!asset) return null;
            try { return asset.text; }
            finally { Resources.UnloadAsset(asset); }
        }
        public static string VoiceResource(string path)
        {
            var root = Path.GetFullPath(VoiceRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(path);
            if (!full.StartsWith(root, StringComparison.Ordinal)) return null;
            return ResourceRoot + "Voice/" + Path.ChangeExtension(full.Substring(root.Length), null).Replace('\\', '/');
        }
        public static VoicePackRegistry CreateVoiceRegistry()
        {
            var index = ReadText("voice-assets");
            if (index == null) throw new InvalidDataException("Android voice asset index is missing.");
            var paths = new HashSet<string>(JsonSerializer.Deserialize<string[]>(index), StringComparer.Ordinal);
            var registry = new VoicePackRegistry(VoiceRoot, Debug.LogWarning,
                path => {
                    var resource = VoiceResource(path);
                    return resource == null ? null : ReadText(resource.Substring(ResourceRoot.Length));
                }, clip => paths.Contains(VoiceResource(clip.Path)), includeLegacy: false);
            if (registry.DefaultVoiceId != "kyutai-Blake" || registry.Packs.Count == 0)
                throw new InvalidDataException("The required Blake narration pack is missing.");
            return registry;
        }
        public static string AudioResource(string path, bool speech)
        {
            if (speech) return VoiceResource(path);
            var prefix = Application.streamingAssetsPath.TrimEnd('/', '\\') + "/Content/Sounds/";
            var normalized = path.Replace('\\', '/');
            return normalized.StartsWith(prefix.Replace('\\', '/'), StringComparison.Ordinal)
                ? ResourceRoot + "Sounds/" + Path.GetFileNameWithoutExtension(path) : null;
        }
    }
}
