using System;
using System.IO;
using System.Linq;
using System.Threading;
using KeyLearner.Studio;
using UnityEngine;
namespace KeyLearner.Unity.Platform
{
    /// <summary>Retains legacy headless diagnostic flags; diagnostic launches can never fall through into protected play.</summary>
    public static class WindowsCompatibility
    {
        public static bool IsDiagnostic(string[] args) => args.Any(a => a == "--probe-guard" || a == "--probe-accessibility" || a == "--restore-accessibility");
        public static bool TryRun(string[] args, out int exitCode)
        {
            exitCode = 0;
            if (!IsDiagnostic(args))
                return false;
            try
            {
                if (args.Contains("--probe-guard"))
                {
                    using (var guard = new KeyboardGuard(IntPtr.Zero, false))
                    {
                        Thread.Sleep(100);
                        if (!guard.TryReadDesktopInput(out _))
                            throw new InvalidOperationException("Native desktop input-state query failed.");
                        if (guard.Snapshot().Count != 0)
                            throw new InvalidOperationException("Disarmed guard retained input.");
                    }
                    var directory = Directory.GetParent(Application.dataPath).FullName;
                    File.WriteAllText(Path.Combine(directory, "guard-probe.txt"), "PASS: native keyboard/focus hooks installed and released in pass-through mode; desktop input-state query succeeded; disarmed input stayed empty.");
                }
                else if (args.Contains("--probe-accessibility"))
                {
                    var root = Path.Combine(Path.GetTempPath(), "KeyLearner-accessibility-probe");
                    Directory.CreateDirectory(root);
                    using (var lease = new AccessibilityLease(root))
                        Thread.Sleep(args.Contains("--wait") ? 30000 : 500);
                }
                else
                {
                    int index = Array.IndexOf(args, "--restore-accessibility");
                    if (index < 0 || index + 3 >= args.Length)
                        throw new ArgumentException("Incomplete accessibility restoration command.");
                    // The independent helper survives this diagnostic Unity player exiting.
                    using (var process = PlatformProcess.Start("--restore-accessibility " + PlatformProcess.Quote(args[index + 1]) + " " + int.Parse(args[index + 2]) + " " + long.Parse(args[index + 3])))
                    {
                    }
                }
            }
            catch (Exception error) { Debug.LogError("KeyLearner diagnostic failed: " + error.GetType().Name + ": " + error.Message); exitCode = 1; }
            return true;
        }
    }
}
