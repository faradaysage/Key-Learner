using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using KeyLearner.Studio;
using NUnit.Framework;
using PlayMode = KeyLearner.Studio.PlayMode;

namespace KeyLearner.Unity.Tests.EditMode
{
    // These run against the shipped domain DLL inside Unity, complementing the
    // larger console regression suite without opening a real child profile.
    public sealed class DomainBridgeTests
    {
        [TestCase(0, PlayMode.SmashGarden, typeof(CanvasGame))]
        [TestCase(1, PlayMode.WordAdventure, typeof(CanvasGame))]
        [TestCase(2, PlayMode.Counting, typeof(CanvasGame))]
        [TestCase(3, PlayMode.BirdFlight, typeof(ExplorerGame))]
        [TestCase(4, PlayMode.Racing, typeof(ExplorerGame))]
        [TestCase(5, PlayMode.Dolphin, typeof(ExplorerGame))]
        [TestCase(6, PlayMode.Subitizing, typeof(DotPopGame))]
        [TestCase(7, PlayMode.HowManyNow, typeof(VisualMathGame))]
        [TestCase(8, PlayMode.WhatsHiding, typeof(VisualMathGame))]
        [TestCase(9, PlayMode.MakeNumber, typeof(VisualMathGame))]
        [TestCase(10, PlayMode.DotDuel, typeof(VisualMathGame))]
        [TestCase(11, PlayMode.CannonHop, typeof(VisualMathGame))]
        [TestCase(12, PlayMode.Dinosaur, typeof(DinosaurGame))]
        public void SavedModeIdsResolveToCatalogAndUnityPresentation(int id, PlayMode mode, Type presentation)
        {
            Assert.That((int)mode, Is.EqualTo(id), "Never renumber a persisted mode.");
            Assert.That(GameCatalog.All.Count(g => g.Mode == mode), Is.EqualTo(1));
            Assert.That(GameCatalog.For(mode).Name, Is.Not.Empty);
            var registry = MinigameRegistry.Create();
            Assert.That(registry.Keys, Is.EquivalentTo(GameCatalog.All.Select(g => g.Mode)));
            Assert.That(registry[mode](), Is.TypeOf(presentation)); // Construction only: Enter starts services.
        }

        [Test]
        public void FocusLossRevokesInputUntilExplicitlyRearmed()
        {
            int clears = 0;
            var ownWindow = new IntPtr(42);
            var focus = new InputFocus(ownWindow, () => clears++);
            Assert.That(focus.Accepts(ownWindow), Is.False);
            focus.SetActive(true);
            Assert.That(focus.Accepts(ownWindow), Is.True);
            Assert.That(focus.Accepts(new IntPtr(43)), Is.False);
            Assert.That(focus.Accepts(ownWindow), Is.False, "Foreground return alone must not resume stale input.");
            Assert.That(focus.Losses, Is.EqualTo(1));
            focus.SetActive(true);
            Assert.That(focus.Accepts(ownWindow, false), Is.False, "A secure desktop revokes input too.");
            Assert.That(focus.Losses, Is.EqualTo(2));
            Assert.That(clears, Is.EqualTo(4));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LateNativeFocusLossIsRearmedWithoutLosingThePendingReset(bool afterApply)
        {
            var window = new IntPtr(42);
            var queue = new KeyTransitionBuffer();
            var physical = new PhysicalKeyboard(queue);
            var focus = new InputFocus(window, physical.Clear);
            bool nativeForeground = true;
            var handoff = new KeyLearner.Unity.Platform.CaptureFocusHandoff(active => {
                focus.SetActive(active && nativeForeground);
                return focus.Losses;
            });
            handoff.Apply(true, true);
            physical.Feed(65, 30, false, true, false, 1);
            Assert.That(queue.Snapshot().IsDown(65), Is.True);
            Assert.That(handoff.NeedsReset(focus.Losses), Is.False);
            if (!afterApply) focus.SetActive(false);
            handoff.Apply(true, false);
            if (afterApply) focus.SetActive(false);
            Assert.That(handoff.NeedsReset(focus.Losses), Is.True, "Do not swallow a loss the frame did not handle.");
            Assert.That(queue.Snapshot().Count, Is.Zero, "Stale held keys must be cleared.");
            handoff.Apply(true, true);
            Assert.That(focus.Accepts(window), Is.True);
            physical.Feed(13, 28, false, true, false, 2);
            Assert.That(queue.TryRead(out var entered), Is.True);
            Assert.That(entered.Key, Is.EqualTo(13), "Fresh input reaches the picker after recovery.");
            Assert.That(handoff.NeedsReset(focus.Losses), Is.False);
            nativeForeground = false;
            handoff.Apply(true, false);
            Assert.That(focus.Accepts(window), Is.False, "Native foreground still gates every rearm.");
            Assert.That(queue.Snapshot().Count, Is.Zero);
        }

        [Test]
        public void MultitouchAndEmulatedMouseCannotBecomeAnAnswer()
        {
            var safety = new PointerGestureSafety();
            Assert.That(safety.ObserveTouches(1, 2, true, true), Is.False);
            Assert.That(safety.ObserveTouches(1.1, 1, true, true), Is.False, "The remaining finger is still the rejected gesture.");
            Assert.That(safety.ObserveTouches(1.2, 0, true, false), Is.False);
            Assert.That(safety.AllowsMouse(1.4), Is.False);
            Assert.That(safety.AllowsMouse(1.46), Is.True);
            Assert.That(safety.ObserveTouches(1.5, 1, true, true), Is.True);
        }

        [Test]
        public void FocusResetRequiresHeldTouchToLiftBeforeAcceptingAgain()
        {
            var safety = new PointerGestureSafety();
            safety.Reset(10);
            Assert.That(safety.AllowsMouse(10.1), Is.False);
            Assert.That(safety.ObserveTouches(10.1, 1, true, true), Is.False);
            safety.ObserveTouches(10.2, 0, false, false);
            Assert.That(safety.ObserveTouches(10.3, 1, true, true), Is.True);
        }
    }

    public sealed class SavedDataBridgeTests
    {
        string directory;

        [SetUp]
        public void CreateIsolatedProfile()
        {
            directory = Path.Combine(Path.GetTempPath(), "KeyLearner-UnityTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            // Bypass first-run local voice discovery; tests never start audio or use AppData.
            File.WriteAllText(Path.Combine(directory, "settings.json"), "{\"Sound\":false,\"EffectsSound\":false}");
        }

        [TearDown]
        public void RemoveIsolatedProfile()
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }

        [Test]
        public void UnityRuntimeRoundTripsNumericEnumsDictionaryKeysAndExternalPathsWithBackups()
        {
            var store = new Store(directory, directory);
            store.Settings.Mode = PlayMode.CannonHop;
            store.Settings.KeyIcons[65] = "apple-whole";
            store.Profile.WordCounts["mommy"] = 7;
            store.Profile.PrefixHabits["mom"] = 1.25;
            store.Words.Clear();
            string image = Path.Combine(directory, "custom art", "mommy.png");
            string recording = Path.Combine(directory, "recordings", "mommy.wav");
            store.Words.Add(new WordEntry { Word = "mommy", Image = image, Recording = recording, Adventure = true });
            Assert.That(store.Save(), Is.True, store.Status);
            using (var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "settings.json"))))
            {
                Assert.That(json.RootElement.GetProperty("Mode").GetInt32(), Is.EqualTo(11));
                Assert.That(json.RootElement.GetProperty("KeyIcons").GetProperty("65").GetString(), Is.EqualTo("apple-whole"));
            }
            store.Settings.Mode = PlayMode.Dolphin;
            Assert.That(store.Save(), Is.True, store.Status);
            using (var previous = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "settings.json.bak"))))
                Assert.That(previous.RootElement.GetProperty("Mode").GetInt32(), Is.EqualTo(11));
            Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);

            var reloaded = new Store(directory, directory);
            Assert.That(reloaded.Settings.Mode, Is.EqualTo(PlayMode.Dolphin));
            Assert.That(reloaded.Settings.KeyIcons[65], Is.EqualTo("apple-whole"));
            Assert.That(reloaded.Profile.WordCounts["mommy"], Is.EqualTo(7));
            Assert.That(reloaded.Profile.PrefixHabits["mom"], Is.EqualTo(1.25));
            Assert.That(reloaded.Words.Single().Image, Is.EqualTo(image));
            Assert.That(reloaded.Words.Single().Recording, Is.EqualTo(recording));
        }

        [Test]
        public void MalformedSavedFileFallsBackWithoutOverwritingOriginal()
        {
            string path = Path.Combine(directory, "profile.json");
            const string broken = "{\"WordCounts\": this is an interrupted file";
            File.WriteAllText(path, broken);
            var store = new Store(directory, directory);
            Assert.That(store.Profile.WordCounts, Is.Empty);
            Assert.That(store.Status, Does.Contain("Original file preserved"));
            Assert.That(File.ReadAllText(path), Is.EqualTo(broken));
        }

        [Test]
        public void ContentRootImportsCustomDictionaryWithoutChangingSavedPathsOnReload()
        {
            string content = Path.Combine(directory, "content");
            Directory.CreateDirectory(Path.Combine(content, "data"));
            File.WriteAllText(Path.Combine(content, "data", "custom_dictionary.csv"), "word,image,recording\nkitten,images/kitten.png,audio/kitten.wav\n");
            var store = new Store(directory, content);
            string expected = Path.GetFullPath(Path.Combine(content, "images/kitten.png"));
            Assert.That(store.Words.Single(w => w.Word == "kitten").Image, Is.EqualTo(expected));
            Assert.That(store.Save(), Is.True);
            var reloaded = new Store(directory, directory);
            Assert.That(reloaded.Words.Single(w => w.Word == "kitten").Image, Is.EqualTo(expected));
        }
    }
}
