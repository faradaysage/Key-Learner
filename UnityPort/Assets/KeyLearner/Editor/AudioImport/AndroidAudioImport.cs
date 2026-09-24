using System;
using UnityEditor;
using UnityEngine;

namespace KeyLearner.Unity.Editor
{
    public sealed class AndroidAudioImport : AssetPostprocessor
    {
        public const float NarrationQuality = .65f;
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/KeyLearnerAndroidBuild/Resources/AndroidContent/", StringComparison.Ordinal)) return;
            var importer = (AudioImporter)assetImporter;
            bool narration = assetPath.Contains("/Voice/");
            importer.forceToMono = narration;
            importer.loadInBackground = true;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = NarrationQuality;
            settings.preloadAudioData = false;
            settings.sampleRateSetting = narration ? AudioSampleRateSetting.OverrideSampleRate : AudioSampleRateSetting.PreserveSampleRate;
            if (narration) settings.sampleRateOverride = 24000;
            importer.SetOverrideSampleSettings("Android", settings);
        }
    }
}
