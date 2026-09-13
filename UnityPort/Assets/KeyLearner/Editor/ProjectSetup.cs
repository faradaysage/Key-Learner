using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KeyLearner.Unity.Editor
{
    public static class ProjectSetup
    {
        [MenuItem("KeyLearner/Configure project and content")]
        public static void Configure()
        {
            PlayerSettings.companyName = "KeyLearner";
            PlayerSettings.productName = "KeyLearner";
            PlayerSettings.bundleVersion = "3.0.0";
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(UnityEditor.Build.NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.defaultScreenWidth = 1366;
            PlayerSettings.defaultScreenHeight = 768;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.usePlayerLog = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/KeyLearner/Resources/Branding/window-icon.png") }, IconKind.Any);
            var playerAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
            if (playerAsset)
            {
                var serialized = new SerializedObject(playerAsset);
                var input = serialized.FindProperty("activeInputHandler");
                if (input != null)
                    input.intValue = 2;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            QualitySettings.vSyncCount = 1;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            pipeline.msaaSampleCount = 4;
            pipeline.renderScale = 1;
            pipeline.supportsHDR = true;
            pipeline.shadowDistance = 220;
            pipeline.shadowCascadeCount = 3;
            pipeline.supportsCameraDepthTexture = true;
            pipeline.supportsCameraOpaqueTexture = true;
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            EditorUtility.SetDirty(pipeline);
            var stripping = GraphicsSettings.GetRenderPipelineSettings<URPShaderStrippingSetting>();
            if (stripping != null)
                stripping.stripUnusedPostProcessingVariants = false;
            var graphics = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset").FirstOrDefault();
            if (graphics)
            {
                var so = new SerializedObject(graphics);
                var fog = so.FindProperty("m_FogStripping");
                if (fog != null)
                    fog.intValue = 1;
                var array = so.FindProperty("m_AlwaysIncludedShaders");
                for (int i = array.arraySize - 1; i >= 0; i--)
                {
                    var shader = array.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                    if (shader && shader.name == "GUI/Text Shader")
                    {
                        array.GetArrayElementAtIndex(i).objectReferenceValue = null;
                        array.DeleteArrayElementAtIndex(i);
                    }
                }
                foreach (string name in new[] { "KeyLearner/Terrain", "KeyLearner/Water", "KeyLearner/Particle", "KeyLearner/Sky", "KeyLearner/SoftCloud", "KeyLearner/SeabedCaustics", "Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit" })
                {
                    var shader = Shader.Find(name);
                    if (!shader)
                        continue;
                    bool exists = false;
                    for (int i = 0; i < array.arraySize; i++)
                        if (array.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                            exists = true;
                    if (!exists)
                    {
                        int n = array.arraySize;
                        array.InsertArrayElementAtIndex(n);
                        array.GetArrayElementAtIndex(n).objectReferenceValue = shader;
                    }
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            ContentBuilder.Build();
            // Configure imports and project settings repeatedly, but keep authored scene edits.
            const string scenePath = "Assets/KeyLearner/Scenes/KeyLearner.unity";
            if (File.Exists(scenePath))
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
                AssetDatabase.SaveAssets();
                Debug.Log("KEYLEARNER_CONFIGURED existing scene preserved " + Application.unityVersion);
                return;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Gameplay Camera", typeof(Camera), typeof(AudioListener));
            camera.tag = "MainCamera";
            var cam = camera.GetComponent<Camera>();
            cam.backgroundColor = Style.Navy;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            var cameraData = camera.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var sun = new GameObject("Warm sunlight", typeof(Light));
            var light = sun.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1, .94f, .80f);
            light.intensity = 1.8f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48, -32, 0);
            RenderSettings.sun = light;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.55f, .66f, .78f);
            RenderSettings.ambientEquatorColor = new Color(.38f, .46f, .53f);
            RenderSettings.ambientGroundColor = new Color(.23f, .29f, .33f);
            var volume = new GameObject("Gentle color and glow", typeof(Volume));
            var v = volume.GetComponent<Volume>();
            v.isGlobal = true;
            string profilePath = "Assets/KeyLearner/Generated/KeyLearnerVolume.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            if (!profile.TryGet<Bloom>(out var bloom))
                bloom = profile.Add<Bloom>();
            bloom.threshold.Override(1.3f);
            bloom.intensity.Override(.16f);
            bloom.scatter.Override(.55f);
            if (!profile.TryGet<ColorAdjustments>(out var color))
                color = profile.Add<ColorAdjustments>();
            color.saturation.Override(8);
            color.contrast.Override(7);
            if (!profile.TryGet<Tonemapping>(out var tone))
                tone = profile.Add<Tonemapping>();
            tone.mode.Override(TonemappingMode.Neutral);
            v.sharedProfile = profile;
            new GameObject("KeyLearner Suite", typeof(Suite));
            Directory.CreateDirectory("Assets/KeyLearner/Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/KeyLearner/Scenes/KeyLearner.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scene.path, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("KEYLEARNER_CONFIGURED " + Application.unityVersion);
        }
        [MenuItem("KeyLearner/Build Windows player")]
        public static void BuildWindows()
        {
            if (!File.Exists("Assets/KeyLearner/Scenes/KeyLearner.unity"))
                Configure();
            string output = Path.GetFullPath("../artifacts/unity-windows/KeyLearner.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { "Assets/KeyLearner/Scenes/KeyLearner.unity" }, locationPathName = output, target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
            Directory.CreateDirectory("../artifacts/unity-migration");
            File.WriteAllText("../artifacts/unity-migration/build-result.txt", report.summary.result + "\n" + report.summary.totalErrors + " errors\n" + report.summary.totalSize + " bytes\n" + report.summary.totalTime);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Windows build failed: " + report.summary.result);
            string targetData = Path.Combine(Path.GetDirectoryName(output), "data");
            Directory.CreateDirectory(targetData);
            foreach (var file in Directory.GetFiles("../data", "*_dictionary.csv").Where(f => !Path.GetFileName(f).Equals("private_dictionary.csv", StringComparison.OrdinalIgnoreCase)))
                File.Copy(file, Path.Combine(targetData, Path.GetFileName(file)), true);
            foreach (string notice in new[] { "THIRD_PARTY_ASSETS.md", "LICENSE" })
                if (File.Exists("../" + notice))
                    File.Copy("../" + notice, Path.Combine(Path.GetDirectoryName(output), notice), true);
            Debug.Log("KEYLEARNER_BUILD_SUCCEEDED " + output);
        }
        public static void ConfigureAndBuild()
        {
            Configure();
            BuildWindows();
        }
    }
}
