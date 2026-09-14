using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;

namespace KeyLearner.Unity.Tests.EditMode
{
    public sealed class SessionDiagnosticsTests
    {
        string root;
        [SetUp] public void Setup() => root = Path.Combine(Path.GetTempPath(), "KeyLearner-DiagnosticTests-" + Guid.NewGuid().ToString("N"));
        [TearDown] public void Cleanup() { if (Directory.Exists(root)) Directory.Delete(root, true); else if (File.Exists(root)) File.Delete(root); }

        [Test]
        public void ProtectedPlayerConfigurationAndCompiledBackendExcludeCompetingInputSystem()
        {
            var asset = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").First();
            var settings = new UnityEditor.SerializedObject(asset);
            Assert.That(settings.FindProperty("activeInputHandler").intValue, Is.Zero,
                "Both input backends prevented native protected-hook callbacks in the actual Windows player.");
            Assert.That(SessionDiagnostics.UnityInputBackend, Is.EqualTo("legacy-only"),
                "The compiled runtime must match the configured backend; restart Unity after a backend change.");
        }

        [Test]
        public void SnapshotIsFlushedAndReadableBeforeNormalShutdown()
        {
            using var log = new SessionDiagnostics(root);
            log.Write("heartbeat", new { active = false, callbacks = 0, physicalPackets = 0, syntheticPackets = 0 });
            using var source = new FileStream(log.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(source);
            using var json = JsonDocument.Parse(reader.ReadLine());
            Assert.That(json.RootElement.GetProperty("schema").GetInt32(), Is.EqualTo(1));
            Assert.That(json.RootElement.GetProperty("data").GetProperty("active").GetBoolean(), Is.False);
            Assert.That(json.RootElement.GetProperty("utc").GetString(), Is.Not.Empty);
        }

        [Test]
        public void UnwritableDiagnosticDirectoryCannotThrowIntoGameplay()
        {
            File.WriteAllText(root, "owned test file blocks directory creation");
            using var log = new SessionDiagnostics(root);
            Assert.That(log.Available, Is.False);
            Assert.DoesNotThrow(() => log.Write("heartbeat", new { active = false }));
            Assert.That(log.Failure, Is.Not.Empty);
        }

        [Test]
        public void LongRunningSessionStopsAtBoundWithoutDamagingExistingJson()
        {
            using var log = new SessionDiagnostics(root);
            for (int i = 0; i < 400 && log.Available; i++) log.Write("budget-test", new { boundedPayload = new string('x', 8192) });
            Assert.That(log.Available, Is.False);
            Assert.That(new FileInfo(log.FilePath).Length, Is.LessThanOrEqualTo(SessionDiagnostics.MaximumBytes));
            foreach (string line in File.ReadAllLines(log.FilePath)) using (JsonDocument.Parse(line)) { }
        }

        [Test]
        public void RetentionPreservesOtherFilesAndCurrentWritableSession()
        {
            Directory.CreateDirectory(Path.Combine(root, "diagnostics"));
            string unrelated = Path.Combine(root, "diagnostics", "parent-notes.txt");
            File.WriteAllText(unrelated, "keep");
            for (int i = 0; i < 9; i++) using (var log = new SessionDiagnostics(root)) log.Write("session", new { number = i });
            Assert.That(Directory.GetFiles(Path.Combine(root, "diagnostics"), "input-session-*.jsonl").Length, Is.EqualTo(6));
            Assert.That(File.ReadAllText(unrelated), Is.EqualTo("keep"));
        }

        [Test]
        public void IntroductionWaitsForUnityAndAnActualRepaintThenAdvancesWithoutKeys()
        {
            var intro = new StartupIntroduction();
            intro.Tick(100);
            Assert.That(intro.Active, Is.True);
            intro.Painted(100, false);
            intro.Tick(120);
            Assert.That(intro.Active, Is.True);
            intro.Painted(120, true);
            intro.Painted(123, true);
            intro.Tick(123.9);
            Assert.That(intro.Active, Is.True);
            intro.Tick(124);
            Assert.That(intro.Completed, Is.True);
        }
    }
}
