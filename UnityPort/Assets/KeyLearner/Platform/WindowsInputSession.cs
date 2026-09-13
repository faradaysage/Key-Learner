using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using KeyLearner.Studio;
using UnityEngine;

namespace KeyLearner.Unity.Platform
{
    public sealed class LaunchOptions
    {
        readonly string[] args;
        public bool Preview
        {
            get; private set;
        }
        public bool Studio
        {
            get; private set;
        }
        public bool Unprotected => Preview || Studio || Application.isEditor;
        public string DataRoot
        {
            get; private set;
        }
        LaunchOptions(string[] args)
        {
            this.args = args;
        }
        public bool Has(string flag) => args.Contains(flag, StringComparer.OrdinalIgnoreCase);
        public string Value(string flag, string fallback = "")
        {
            var index = Array.FindIndex(args, a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }
        public static LaunchOptions Parse(string[] args)
        {
            var result = new LaunchOptions(args);
            result.Preview = result.Has("--preview") || Application.isEditor || WindowsCompatibility.IsDiagnostic(args);
            result.Studio = result.Has("--studio");
            result.DataRoot = result.Value("--data");
            result.DataRoot = ProfileLocation.Resolve(result.Preview, result.DataRoot);
            result.DataRoot = Path.GetFullPath(result.DataRoot);
            return result;
        }
    }

    public sealed class InputFrame
    {
        public bool Active
        {
            get; internal set;
        }
        public bool Reset
        {
            get; internal set;
        }
        public KeySnapshot Snapshot
        {
            get; internal set;
        }
        public IReadOnlyList<KeyEvent> Events
        {
            get; internal set;
        }
        public ParentAction ParentAction
        {
            get; internal set;
        }
        public bool OpenPicker
        {
            get; internal set;
        }
    }

    // A callback can revoke capture after the frame's reset check. Never acknowledge
    // that newer loss unless the frame actually reset its gameplay/parent state.
    public sealed class CaptureFocusHandoff
    {
        readonly Func<bool, int> setActive;
        int handledLosses;
        public CaptureFocusHandoff(Func<bool, int> setActive) { this.setActive = setActive; }
        public bool NeedsReset(int losses) => losses != handledLosses;
        public void Apply(bool active, bool resetHandled)
        {
            int losses = setActive(active); // Refresh every frame, including stable foreground.
            if (resetHandled) handledLosses = losses;
        }
    }

    /// <summary>One owner for physical state, focus and parent escape controls. Never samples background keys.</summary>
    public sealed class WindowsInputSession : IDisposable
    {
        readonly bool unprotected;
        readonly string root;
        readonly IntPtr window;
        readonly ParentHold hold = new ParentHold();
        readonly EscapeExit escape = new EscapeExit();
        readonly TapSequence options = new TapSequence(79);
        readonly GameShortcut picker = new GameShortcut();
        readonly HashSet<int> previewHeld = new HashSet<int>(), eventHeld = new HashSet<int>();
        readonly List<KeyEvent> events = new List<KeyEvent>();
        readonly InputFrame frame = new InputFrame();
        readonly KeyboardGuard guard;
        readonly Mutex single;
        AccessibilityLease accessibility;
        bool active, disarmed = true, allowExit, disposed, mutexOwned;
        readonly CaptureFocusHandoff capture;
        uint previousWindowState;
        long reportedPackets;
        double reportAfter;
        long polls, activePolls, dispatchedEvents, staleEvents, resets;
        double maximumEventAge;
        public object DiagnosticSnapshot()
        {
            var foreground = window == IntPtr.Zero ? IntPtr.Zero : GetForegroundWindow();
            uint foregroundPid = 0, boundPid = 0;
            if (foreground != IntPtr.Zero) GetWindowThreadProcessId(foreground, out foregroundPid);
            if (window != IntPtr.Zero) GetWindowThreadProcessId(window, out boundPid);
            bool desktopInput = false;
            bool desktopRead = guard != null && guard.TryReadDesktopInput(out desktopInput);
            return new {
                protectedPlay = !unprotected, active, disarmed, unityFocused = Application.isFocused,
                boundWindow = window.ToInt64(), foregroundWindow = foreground.ToInt64(), boundPid, foregroundPid,
                windowVisible = window != IntPtr.Zero && IsWindowVisible(window),
                minimized = window != IntPtr.Zero && IsIconic(window),
                maximized = window != IntPtr.Zero && IsZoomed(window),
                desktopRead, desktopInput, polls, activePolls, dispatchedEvents, staleEvents, maximumEventAge, resets,
                guard = guard?.DiagnosticSnapshot()
            };
        }
        public bool Protected => !unprotected;
        public string Status => unprotected ? "Preview / Parent Studio: keyboard protection is disabled." : guard.Diagnostics;
        public WindowsInputSession(bool previewOrStudio, string profileRoot)
        {
            unprotected = previewOrStudio || Application.isEditor || Application.platform != RuntimePlatform.WindowsPlayer;
            root = profileRoot;
            if (Application.platform == RuntimePlatform.WindowsPlayer)
            {
                SetCurrentProcessExplicitAppUserModelID("KeyLearner.Desktop");
                window = FindPlayerWindow();
                UnityEngine.Debug.Log("KEYLEARNER_INPUT_WINDOW bound=" + window + " foreground=" + GetForegroundWindow() + " focused=" + Application.isFocused + " fullscreen=" + Screen.fullScreen);
            }
            try
            {
                if (!unprotected)
                {
                    if (window == IntPtr.Zero)
                        throw new InvalidOperationException("The native Unity game window could not be identified. Keyboard protection was not enabled.");
                    single = new Mutex(false, "Local\\KeyLearner.ProtectedSession");
                    try
                    {
                        mutexOwned = single.WaitOne(0);
                    }
                    catch (AbandonedMutexException) { mutexOwned = true; }
                    if (!mutexOwned)
                        throw new InvalidOperationException("A protected KeyLearner session is already open.");
                    guard = new KeyboardGuard(window);
                    capture = new CaptureFocusHandoff(guard.SetGameActive);
                    Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                    Screen.fullScreen = true;
                }
                Application.wantsToQuit += CanQuit;
            }
            catch { Dispose(); throw; }
        }
        bool CanQuit() => unprotected || allowExit || disposed || disarmed || !active || guard == null || !guard.CaptureActive || !guard.OwnsForeground;
        public void AuthorizeExit()
        {
            allowExit = true;
            Disarm();
        }
        public void Suspend()
        {
            Disarm();
        }
        void Disarm()
        {
            var lease = accessibility;
            accessibility = null;
            disarmed = true;
            try
            {
                guard?.SetGameActive(false);
                guard?.ResetInput();
            }
            finally
            {
                try
                {
                    if (!unprotected)
                    {
                        // Release native confinement even if Unity state cleanup or accessibility
                        // restoration fails while Windows is changing desktops.
                        if (window != IntPtr.Zero)
                        {
                            ClipCursor(IntPtr.Zero);
                            SetWindowPos(window, new IntPtr(-2), 0, 0, 0, 0, 0x13);
                        }
                        Cursor.lockState = CursorLockMode.None;
                        Cursor.visible = true;
                    }
                }
                finally
                {
                    // On failure the lease file remains for the independent watchdog.
                    lease?.Dispose();
                }
            }
        }
        public InputFrame Poll(bool visible)
        {
            polls++;
            events.Clear();
            frame.Reset = false;
            frame.ParentAction = ParentAction.None;
            frame.OpenPicker = false;
            var state = window == IntPtr.Zero ? 0u : ((IsWindowVisible(window) ? 1u : 0u) | (IsIconic(window) ? 2u : 0u) | (IsZoomed(window) ? 4u : 0u));
            bool next = visible && Application.isFocused && (window == IntPtr.Zero || (state & 3) == 1) && (guard == null || guard.OwnsForeground);
            bool changed = next != active || disarmed || state != previousWindowState || (guard != null && capture.NeedsReset(guard.FocusLosses));
            previousWindowState = state;
            if (changed)
            {
                resets++;
                Disarm();
                hold.Reset();
                escape.Reset();
                options.Reset();
                picker.Reset();
                previewHeld.Clear();
                eventHeld.Clear();
                frame.Reset = true;
                disarmed = false;
                if (next && guard != null)
                {
                    // Missing helper or failed guardian must fail before capturing any keyboard input.
                    accessibility = new AccessibilityLease(root);
                    // Starting the independent restoration helper can take time. Recheck native
                    // ownership before changing cursor/topmost state or authorizing capture.
                    if (!guard.OwnsForeground)
                    {
                        Disarm();
                        next = false;
                    }
                    else
                    {
                        SetWindowPos(window, new IntPtr(-1), 0, 0, 0, 0, 0x13);
                        Cursor.lockState = CursorLockMode.Confined;
                        Cursor.visible = true;
                        // Capture is authorized below, after cursor/guardian setup.
                    }
                }
            }
            capture?.Apply(next, changed);
            if (next && guard != null && !guard.CaptureActive)
            {
                Disarm();
                next = false;
                frame.Reset = true;
            }
            active = next;
            if (next) activePolls++;
            frame.Active = next;
            if (changed)
                UnityEngine.Debug.Log("KEYLEARNER_INPUT_TRANSITION active=" + next + " bound=" + window + " foreground=" + GetForegroundWindow() + " visibleState=" + state + " unityFocus=" + Application.isFocused + " nativeFocus=" + (guard?.OwnsForeground ?? false));
            if (!next)
            {
                frame.Snapshot = default;
                frame.Events = events;
                return frame;
            }
            double now = KeyboardGuard.Now;
            if (guard != null && now >= reportAfter && guard.PacketCount != reportedPackets)
            {
                reportedPackets = guard.PacketCount;
                reportAfter = now + 5;
                UnityEngine.Debug.Log("KEYLEARNER_INPUT_COUNTS " + guard.Diagnostics);
            }
            if (guard != null)
            {
                while (guard.TryRead(out var e))
                {
                    if (!guard.OwnsForeground)
                    {
                        Disarm();
                        frame.Reset = true;
                        frame.Active = false;
                        frame.ParentAction = ParentAction.None;
                        frame.OpenPicker = false;
                        events.Clear();
                        break;
                    }
                    if (e.Key < 0)
                    {
                        eventHeld.Clear();
                        hold.Reset();
                        escape.Reset();
                        options.Reset();
                        picker.Reset();
                        events.Add(e);
                        continue;
                    }
                    maximumEventAge = Math.Max(maximumEventAge, now - e.Time);
                    if (now - e.Time > .25)
                    {
                        staleEvents++;
                        continue;
                    }
                    Consume(e);
                    dispatchedEvents++;
                }
                frame.Snapshot = guard.Snapshot();
            }
            else
            {
                // Read only Unity's focused player input in unprotected mode. No native hook or global polling.
                foreach (var mapping in KeyMap)
                {
                    bool down = Input.GetKey(mapping.Value);
                    if (down == previewHeld.Contains(mapping.Key))
                        continue;
                    if (down)
                        previewHeld.Add(mapping.Key);
                    else
                        previewHeld.Remove(mapping.Key);
                    Consume(new KeyEvent(mapping.Key, down, now));
                    dispatchedEvents++;
                }
                frame.Snapshot = KeySnapshot.From(previewHeld);
            }
            // Reconcile transient modifier bookkeeping from the independently published live state every frame.
            eventHeld.Clear();
            eventHeld.UnionWith(frame.Snapshot.Keys);
            var action = hold.Update(frame.Snapshot, now);
            if (action != ParentAction.None)
                frame.ParentAction = action;
            frame.Events = events;
            return frame;
        }
        void Consume(KeyEvent e)
        {
            if (e.Down)
                eventHeld.Add(e.Key);
            else
                eventHeld.Remove(e.Key);
            hold.Observe(KeySnapshot.From(eventHeld));
            if (escape.Feed(e))
                frame.ParentAction = ParentAction.Exit;
            if (options.Feed(e))
            {
                frame.ParentAction = ParentAction.Options;
                guard?.ResetInput();
                hold.Reset();
                eventHeld.Clear();
                options.Reset();
            }
            if (picker.Feed(e, eventHeld.Any(ParentChord.IsModifier)))
                frame.OpenPicker = true;
            events.Add(e);
        }
        static readonly Dictionary<int, KeyCode> KeyMap = MakeKeyMap();
        static Dictionary<int, KeyCode> MakeKeyMap()
        {
            var map = new Dictionary<int, KeyCode> { { 8, KeyCode.Backspace }, { 9, KeyCode.Tab }, { 13, KeyCode.Return }, { 19, KeyCode.Pause }, { 20, KeyCode.CapsLock }, { 27, KeyCode.Escape }, { 32, KeyCode.Space }, { 33, KeyCode.PageUp }, { 34, KeyCode.PageDown }, { 35, KeyCode.End }, { 36, KeyCode.Home }, { 37, KeyCode.LeftArrow }, { 38, KeyCode.UpArrow }, { 39, KeyCode.RightArrow }, { 40, KeyCode.DownArrow }, { 45, KeyCode.Insert }, { 46, KeyCode.Delete }, { 144, KeyCode.Numlock }, { 145, KeyCode.ScrollLock }, { 160, KeyCode.LeftShift }, { 161, KeyCode.RightShift }, { 162, KeyCode.LeftControl }, { 163, KeyCode.RightControl }, { 164, KeyCode.LeftAlt }, { 165, KeyCode.RightAlt }, { 186, KeyCode.Semicolon }, { 187, KeyCode.Equals }, { 188, KeyCode.Comma }, { 189, KeyCode.Minus }, { 190, KeyCode.Period }, { 191, KeyCode.Slash }, { 192, KeyCode.BackQuote }, { 219, KeyCode.LeftBracket }, { 220, KeyCode.Backslash }, { 221, KeyCode.RightBracket }, { 222, KeyCode.Quote } };
            for (int i = 0; i < 26; i++)
                map[65 + i] = KeyCode.A + i;
            for (int i = 0; i < 10; i++)
            {
                map[48 + i] = KeyCode.Alpha0 + i;
                map[96 + i] = KeyCode.Keypad0 + i;
            }
            for (int i = 0; i < 12; i++)
                map[112 + i] = KeyCode.F1 + i;
            return map;
        }
        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            Application.wantsToQuit -= CanQuit;
            try
            {
                Disarm();
            }
            finally
            {
                try
                {
                    guard?.Dispose();
                }
                finally
                {
                    try
                    {
                        if (mutexOwned)
                        {
                            single?.ReleaseMutex();
                            mutexOwned = false;
                        }
                    }
                    finally
                    {
                        single?.Dispose();
                    }
                }
            }
        }
        static IntPtr FindPlayerWindow()
        {
            uint process = (uint)Process.GetCurrentProcess().Id;
            IntPtr current = GetActiveWindow();
            GetWindowThreadProcessId(current, out uint owner);
            if (current != IntPtr.Zero && owner == process)
                return current;
            IntPtr found = IntPtr.Zero;
            EnumWindows((candidate, _) => { GetWindowThreadProcessId(candidate, out uint id); if (id == process && IsWindowVisible(candidate) && GetWindow(candidate, 4) == IntPtr.Zero) { found = candidate; return false; } return true; }, IntPtr.Zero);
            return found;
        }
        delegate bool EnumWindow(IntPtr window, IntPtr parameter);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindow callback, IntPtr parameter);
        [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window, uint command);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr window);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] static extern bool ClipCursor(IntPtr rect);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
    }

    internal sealed class AccessibilityLease : IDisposable
    {
        public sealed class Lease
        {
            public uint Get
            {
                get; set;
            }
            public uint Set
            {
                get; set;
            }
            public uint[] Original
            {
                get; set;
            }
            public uint[] Applied
            {
                get; set;
            }
        }
        readonly string file;
        public AccessibilityLease(string root)
        {
            Directory.CreateDirectory(root);
            file = Path.Combine(root, "accessibility-" + Guid.NewGuid().ToString("N") + ".json");
            var leases = new List<Lease>();
            try
            {
                foreach (var spec in new[] { new[] { 0x3Au, 0x3Bu, 2u }, new[] { 0x32u, 0x33u, 6u }, new[] { 0x34u, 0x35u, 2u } })
                {
                    var original = Read(spec[0], (int)spec[2]);
                    var applied = (uint[])original.Clone();
                    applied[1] &= ~12u;
                    leases.Add(new Lease { Get = spec[0], Set = spec[1], Original = original, Applied = applied });
                }
                File.WriteAllText(file, JsonSerializer.Serialize(leases));
                using (var owner = Process.GetCurrentProcess())
                using (var helper = PlatformProcess.Start("--restore-accessibility " + PlatformProcess.Quote(file) + " " + owner.Id + " " + owner.StartTime.ToUniversalTime().Ticks))
                {
                    if (helper == null)
                        throw new IOException("Accessibility restoration helper could not start.");
                    foreach (var lease in leases)
                        Write(lease.Set, lease.Applied);
                }
            }
            catch { Dispose(); throw; }
        }
        static uint[] Read(uint action, int count)
        {
            var data = new uint[count];
            data[0] = (uint)(count * 4);
            if (!SystemParametersInfo(action, data[0], data, 0))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            return data;
        }
        static void Write(uint action, uint[] data)
        {
            if (!SystemParametersInfo(action, data[0], data, 0))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        public void Dispose()
        {
            if (!File.Exists(file))
                return;
            foreach (var lease in JsonSerializer.Deserialize<List<Lease>>(File.ReadAllText(file)))
            {
                var current = Read(lease.Get, lease.Original.Length);
                if ((current[1] & 12) == (lease.Applied[1] & 12))
                {
                    current[1] = (current[1] & ~12u) | (lease.Original[1] & 12u);
                    Write(lease.Set, current);
                }
            }
            File.Delete(file);
        }
        [DllImport("user32.dll", SetLastError = true)] static extern bool SystemParametersInfo(uint action, uint size, [In, Out] uint[] data, uint flags);
    }
    internal static class PlatformProcess
    {
        public static string Quote(string value)
        {
            var result = new System.Text.StringBuilder("\"");
            int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\')
                {
                    slashes++;
                    continue;
                }
                if (c == '"')
                {
                    result.Append('\\', slashes * 2 + 1);
                    result.Append(c);
                    slashes = 0;
                    continue;
                }
                result.Append('\\', slashes);
                slashes = 0;
                result.Append(c);
            }
            result.Append('\\', slashes * 2);
            result.Append('"');
            return result.ToString();
        }
        public static Process Start(string arguments)
        {
            string executable = Path.Combine(Application.streamingAssetsPath, "Platform", "KeyLearner.PlatformHelper.exe");
            if (!File.Exists(executable))
                throw new FileNotFoundException("Build the Windows platform helper before protected play.", executable);
            return Process.Start(new ProcessStartInfo(executable, arguments) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
        }
    }
}
