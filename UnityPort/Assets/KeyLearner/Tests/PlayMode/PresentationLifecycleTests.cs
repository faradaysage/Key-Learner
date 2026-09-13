using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KeyLearner.Studio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace KeyLearner.Unity.Tests.PlayMode
{
    // Use the real Unity bridge but never create Suite or Windows/audio adapters.
    // The test scene is empty, profiles are disposable, and no keyboard hooks run.
    public sealed class PresentationLifecycleTests
    {
        readonly List<Object> owned = new List<Object>();
        GameServices services;
        string directory;
        bool priorFog;
        Material priorSkybox;
        UnityEngine.Rendering.AmbientMode priorAmbientMode;
        Color priorSky, priorEquator, priorGround;
        Light priorSun;

        [SetUp]
        public void CreateIsolatedServices()
        {
            directory = Path.Combine(Path.GetTempPath(), "KeyLearner-UnityPlayTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "settings.json"), "{\"Sound\":false,\"EffectsSound\":false}");
            var cameraObject = Track(new GameObject("Test camera"));
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false; // Geometry and lifecycle checks also work with -nographics.
            camera.aspect = 16f / 9;
            services = new GameServices { Store = new Store(directory, directory), Camera = camera };
            priorFog = RenderSettings.fog;
            priorSkybox = RenderSettings.skybox;
            priorAmbientMode = RenderSettings.ambientMode;
            priorSky = RenderSettings.ambientSkyColor;
            priorEquator = RenderSettings.ambientEquatorColor;
            priorGround = RenderSettings.ambientGroundColor;
            priorSun = RenderSettings.sun;
            RenderSettings.sun = null;
        }

        T Track<T>(T value) where T : Object { owned.Add(value); return value; }

        [UnityTearDown]
        public IEnumerator CleanUp()
        {
            services.Feedback.Clear();
            foreach (var item in owned)
                if (item) Object.Destroy(item);
            owned.Clear();
            RenderSettings.fog = priorFog;
            RenderSettings.skybox = priorSkybox;
            RenderSettings.ambientMode = priorAmbientMode;
            RenderSettings.ambientSkyColor = priorSky;
            RenderSettings.ambientEquatorColor = priorEquator;
            RenderSettings.ambientGroundColor = priorGround;
            RenderSettings.sun = priorSun;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
            yield return null; // Verify actual deferred Unity destruction before the next test.
        }

        sealed class LifecycleProbe : Minigame
        {
            public GameObject Object => Root;
            public override void Tick(float dt) { }
        }
        sealed class QuantityProbe : QuantityMinigame
        {
            public GameObject Object => Root;
            public override void Tick(float dt) { }
            public bool Tap(bool right) => AcceptTap(right);
            public void Present(int count)
            {
                Tokens.Clear();
                for (int i = 0; i < count; i++) Tokens.Add(new Token(new Vector2(180 + i * 100, 250), 24, i));
                SyncSpheres(Vector2.zero);
            }
        }

        [UnityTest]
        public IEnumerator MinigameExitDestroysItsOwnedPresentation()
        {
            var game = new LifecycleProbe();
            game.Enter(services);
            var root = Track(game.Object);
            var child = new GameObject("presentation child");
            child.transform.SetParent(root.transform);
            game.Exit();
            yield return null;
            Assert.That(!root, Is.True);
            Assert.That(!child, Is.True);
        }

        [UnityTest]
        public IEnumerator QuantityPresentationReusesSpheresAndClearsHiddenTargets()
        {
            var game = new QuantityProbe();
            game.Enter(services);
            Track(game.Object);
            game.Present(3);
            yield return null;
            var root = game.Object.transform;
            Assert.That(root.childCount, Is.EqualTo(3));
            var first = root.GetChild(0).gameObject;
            Assert.That(first.GetComponent<Collider>(), Is.Null, "Learning targets use deliberate pointer hit areas, not scene colliders.");
            Assert.That(first.transform.localPosition, Is.EqualTo(new Vector3(180, -250, 0)));
            Assert.That(first.transform.localScale, Is.EqualTo(Vector3.one * 48));
            Assert.That(first.GetComponent<Renderer>().sharedMaterial.shader, Is.Not.Null);
            game.Present(1);
            Assert.That(root.GetChild(1).gameObject.activeSelf, Is.False);
            Assert.That(root.GetChild(2).gameObject.activeSelf, Is.False);
            services.Settings.Theme = Mood.BlackAndWhite;
            game.Present(2);
            Assert.That(root.childCount, Is.EqualTo(3), "Phase changes should reuse the bounded pool.");
            Assert.That(root.GetChild(0).gameObject, Is.SameAs(first));
            Assert.That(first.GetComponent<Renderer>().sharedMaterial.color, Is.EqualTo(Color.white));
        }

        [Test]
        public void QuantityAnswerGateRejectsRightClickAndRapidSecondPress()
        {
            var game = new QuantityProbe();
            game.Enter(services);
            Track(game.Object);
            Assert.That(game.Tap(true), Is.False);
            Assert.That(game.Tap(false), Is.True, "A rejected right click must not consume the left-click window.");
            Assert.That(game.Tap(false), Is.False);
            services.AdvanceClock(.1f);
            Assert.That(game.Tap(false), Is.False);
            services.AdvanceClock(.08f);
            Assert.That(game.Tap(false), Is.True);
        }

        [Test]
        public void PortraitCanvasFitsAndPointerProjectionRoundTrips()
        {
            services.Camera.aspect = 16f / 9;
            services.CanvasCamera(720, 1080);
            Assert.That(services.Camera.orthographicSize, Is.EqualTo(540).Within(.001));
            services.Camera.aspect = .5f;
            services.UpdateViewport();
            Assert.That(services.Camera.orthographicSize, Is.EqualTo(720).Within(.001));
            foreach (var logical in new[] { Vector2.zero, new Vector2(360, 540), new Vector2(700, 1060) })
                Assert.That(Vector2.Distance(services.PointerFromScreen(services.ScreenFromLogical(logical)), logical), Is.LessThan(.002f));
        }

        [Test]
        public void CanvasEntryRestoresNeutralLightingAfterUnderwaterScene()
        {
            services.Camera.orthographic = false;
            services.ConfigureLighting(true);
            RenderSettings.fog = true;
            var underwater = RenderSettings.ambientSkyColor;
            services.Settings.Theme = Mood.BlackAndWhite;
            services.CanvasCamera();
            Assert.That(RenderSettings.fog, Is.False);
            Assert.That(RenderSettings.skybox, Is.Null);
            var sky = RenderSettings.ambientSkyColor;
            Assert.That(sky, Is.Not.EqualTo(underwater));
            Assert.That(sky.r, Is.EqualTo(sky.g).Within(.001));
            Assert.That(sky.g, Is.EqualTo(sky.b).Within(.001));
            Assert.That(services.Camera.backgroundColor, Is.EqualTo(Color.black));
        }

        [UnityTest]
        public IEnumerator CameraRewardPulseRestoresBasePoseAndHonorsGentleMotion()
        {
            var camera = services.Camera;
            var baseline = new Vector3(30, 80, -1200);
            camera.transform.position = baseline;
            services.Feedback.Pulse(5);
            services.Feedback.Apply(camera, .016f, false);
            Assert.That(Vector3.Distance(camera.transform.position, baseline), Is.GreaterThan(.01f));
            services.Feedback.Restore();
            Assert.That(Vector3.Distance(camera.transform.position, baseline), Is.LessThan(.001f));
            yield return null;
            services.Feedback.Apply(camera, .016f, true);
            Assert.That(Vector3.Distance(camera.transform.position, baseline), Is.LessThan(.001f));
            services.Feedback.Pulse(5);
            for (int i = 0; i < 60; i++) services.Feedback.Apply(camera, .016f, false);
            Assert.That(Vector3.Distance(camera.transform.position, baseline), Is.LessThan(.001f), "Expired feedback must not drift the camera.");
        }

        [UnityTest]
        public IEnumerator ContentSpawnPreservesSourceProportionsAndHandlesNegativeIndices()
        {
            var library = Track(ScriptableObject.CreateInstance<ContentLibrary>());
            var source = Track(new GameObject("source content"));
            var parent = Track(new GameObject("scene region"));
            library.SetItems(new[] { new ContentItem { Id = "path", Category = "path", Prefab = source, Height = 2, Size = new Vector3(4, 2, 1), Yaw = 15 } });
            var byHeight = library.Spawn("path", int.MinValue, parent.transform, new Vector3(1, 2, 3), 10, 25);
            var byWidth = library.SpawnWidth("path", -1, parent.transform, Vector3.zero, 20);
            yield return null;
            Assert.That(byHeight.transform.localPosition, Is.EqualTo(new Vector3(1, 2, 3)));
            Assert.That(byHeight.transform.localScale, Is.EqualTo(Vector3.one * 5));
            Assert.That(Quaternion.Angle(byHeight.transform.localRotation, Quaternion.Euler(0, 40, 0)), Is.LessThan(.01f));
            Assert.That(byWidth.transform.localScale, Is.EqualTo(Vector3.one * 5));
            Assert.That(library.Spawn("missing", 0, parent.transform, Vector3.zero, 1), Is.Null);
        }

        [UnityTest]
        public IEnumerator ImportedBirdAnimationActuallyChangesItsRigPose()
        {
            var library = Resources.Load<ContentLibrary>("ContentLibrary");
            Assert.That(library, Is.Not.Null);
            var parent = Track(new GameObject("test sky region"));
            var bird = library.Spawn("bird", 0, parent.transform, Vector3.zero, 5);
            Assert.That(bird, Is.Not.Null);
            yield return null;
            var animation = bird.GetComponentInChildren<Animation>();
            Assert.That(animation, Is.Not.Null);
            Assert.That(animation.clip, Is.Not.Null);
            animation.Stop();
            var bones = bird.GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(s => s.bones).Distinct().ToArray();
            Assert.That(bones, Is.Not.Empty);
            animation.clip.SampleAnimation(animation.gameObject, 0);
            var rotations = bones.Select(b => b.localRotation).ToArray();
            var positions = bones.Select(b => b.localPosition).ToArray();
            animation.clip.SampleAnimation(animation.gameObject, animation.clip.length * .37f);
            Assert.That(bones.Where((b, i) => Quaternion.Angle(b.localRotation, rotations[i]) > .1f || Vector3.Distance(b.localPosition, positions[i]) > .001f).Any(), Is.True,
                "An Animation component alone is insufficient; the imported clip must animate the source rig.");
        }
    }
}
