using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace KeyLearner.Unity.Editor
{
    public static class AndroidBuild
    {
        public const string PackageId = "org.keylearner.app";
        public static void Build()
        {
            if (!File.Exists("Assets/KeyLearnerAndroidBuild/Resources/AndroidContent/voice-assets.json"))
                throw new InvalidOperationException("Run scripts/stage-unity-android.py prepare before building Android.");
            if (Directory.Exists("Assets/StreamingAssets/Platform") || Directory.Exists("Assets/StreamingAssets/Content/Voice"))
                throw new InvalidOperationException("Windows helper/raw voice staging must not enter the Android APK.");
            AndroidFonts.Validate();
            var args = Environment.GetCommandLineArgs();
            string Argument(string name, string fallback)
            {
                int index = Array.IndexOf(args, name);
                return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
            }
            var version = Argument("-keyLearnerVersion", PlayerSettings.bundleVersion);
            if (!System.Text.RegularExpressions.Regex.IsMatch(version, @"^\d+\.\d+\.\d+$"))
                throw new ArgumentException("Use MAJOR.MINOR.PATCH for the shared build version.");
            var components = version.Split('.').Select(int.Parse).ToArray();
            if (components.Any(n => n > 65535)) throw new ArgumentException("Version component exceeds 65535.");
            int code = int.Parse(Argument("-keyLearnerVersionCode", "1"));
            if (code < 1) throw new ArgumentException("Android version code must be positive.");
            PlayerSettings.companyName = "KeyLearner";
            PlayerSettings.productName = "KeyLearner";
            PlayerSettings.bundleVersion = version;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel36;
            PlayerSettings.Android.bundleVersionCode = code;
            PlayerSettings.Android.splitApplicationBinary = false;
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.Android.requestedVisibleInsets = AndroidWindowInsetsType.None;
            PlayerSettings.Android.systemBarsBehavior = AndroidSystemBarsBehavior.ShowTransientBarsBySwipe;
            PlayerSettings.Android.forceInternetPermission = false;
            PlayerSettings.Android.forceSDCardPermission = false;
            PlayerSettings.Android.preferredInstallLocation = AndroidPreferredInstallLocation.ForceInternal;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            AssetDatabase.SaveAssets();
            string repository = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            string output = Path.Combine(repository, "artifacts/leappad", "KeyLearner-LeapPad-" + version + ".apk");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/KeyLearner/Scenes/KeyLearner.unity" },
                locationPathName = output, target = BuildTarget.Android,
                options = BuildOptions.DetailedBuildReport | BuildOptions.CompressWithLz4HC
            });
            var payload = report.packedAssets.SelectMany(pack => pack.contents).GroupBy(a => a.sourceAssetPath)
                .Select(g => new { path = g.Key, bytes = g.Sum(a => (long)a.packedSize) }).OrderByDescending(a => a.bytes).ToArray();
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(output), "build-report.json"), JsonSerializer.Serialize(new {
                version, versionCode = code, packageId = PackageId, abi = "armeabi-v7a", minSdk = 29, targetSdk = 36,
                unity = Application.unityVersion, result = report.summary.result.ToString(),
                apkBytes = File.Exists(output) ? new FileInfo(output).Length : 0,
                audio = new { format = "Vorbis", quality = AndroidAudioImport.NarrationQuality, sampleRate = 24000, mono = true,
                    loadType = "CompressedInMemory", preloadAudioData = false, loadInBackground = true },
                voicePacks = payload.Where(a => a.path.Contains("/AndroidContent/Voice/Packs/"))
                    .GroupBy(a => a.path.Split(new[] { "/Voice/Packs/" }, StringSplitOptions.None)[1].Split('/')[0])
                    .Select(g => new { id = g.Key, packedBytes = g.Sum(a => a.bytes) }).ToArray(),
                packedAssets = payload
            }, new JsonSerializerOptions { WriteIndented = true }));
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Android build failed: " + report.summary.result);
            Debug.Log("KEYLEARNER_ANDROID_APK " + output);
        }
    }
}
