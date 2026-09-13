#nullable enable
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System;
using System.Threading;
namespace KeyLearner.Studio
{

    /// <summary>Session-only interception, on a dedicated message-pump thread.
    /// This is not Windows Keyboard Filter and cannot intercept the secure desktop.</summary>
    public sealed class KeyboardGuard : IDisposable
    {
        private delegate void EventProc(nint hook, uint ev, nint window, int obj, int child, uint thread, uint time);
        private readonly EventProc foregroundChanged;
        private nint focusHook, desktopHook;
        private delegate nint HookProc(int code, nint w, nint l);
        private readonly object stateGate = new();
        private readonly InputFocus focus;
        private readonly nint gameWindow, gameDesktop;
        public int FocusLosses
        {
            get
            {
                lock (stateGate)
                    return focus.Losses;
            }
        }
        // UOI_IO verifies that this desktop actually receives input, independently of SDL
        // activation or a foreground HWND left over from before the lock screen appeared.
        public bool TryReadDesktopInput(out bool receivesInput)
        {
            receivesInput = false;
            if (gameDesktop == 0 || !GetUserObjectInformation(gameDesktop, 6, out var input, 4, out _))
                return false;
            receivesInput = input != 0;
            return true;
        }
        public bool DesktopReceivesInput => TryReadDesktopInput(out var input) && input;
        public bool OwnsForeground => gameWindow != 0 && GetForegroundWindow() == gameWindow && DesktopReceivesInput;
        public int SessionResets
        {
            get; private set;
        }
        private readonly HookProc callback;
        private readonly KeyTransitionBuffer events = new();
        private readonly PhysicalKeyboard physical;
        public string Diagnostics => $"held {Snapshot().Count}; session resets {SessionResets}; repairs {physical.RemappedReleases + physical.RepairedReleases}";
        private readonly ManualResetEventSlim ready = new();
        private readonly Thread thread;
        private nint hook;
        private uint threadId;
        private Exception? error;
        private volatile bool disposed;
        private readonly bool suppress;
        public int Recoveries => events.Recoveries;
        public KeyboardGuard(IntPtr nativeWindow, bool suppress = true)
        {

            this.suppress = suppress;
            physical = new(events);
            gameDesktop = GetThreadDesktop(GetCurrentThreadId());
            gameWindow = nativeWindow;

            if (suppress && gameWindow == 0)
                throw new InvalidOperationException("Game window could not be identified; keyboard interception was not enabled.");
            focus = new(gameWindow, () => { physical.Clear(); SessionResets++; });
            callback = OnKey;
            foregroundChanged = (_, ev, window, _, _, _, _) => { if (ev == 0x20 || window != gameWindow) SetGameActive(false); };
            thread = new Thread(Pump) { IsBackground = true, Name = "KeyLearner keyboard guard" };
            thread.Start();
            if (!ready.Wait(TimeSpan.FromSeconds(5)))
            {
                Dispose();
                throw new TimeoutException("Keyboard protection did not start.");
            }
            if (error != null)
            {
                Dispose();
                throw new InvalidOperationException("Keyboard protection could not start.", error);
            }
        }
        public bool TryRead(out KeyEvent e) => events.TryRead(out e);
        public void DiscardEvents() => events.DiscardEvents();
        public KeySnapshot Snapshot() => events.Snapshot();
        // Inactive input belongs to Windows. Never retain it, including parent shortcuts.
        public void SetGameActive(bool active)
        {
            lock (stateGate)
                focus.SetActive(active && OwnsForeground);
        }
        public void ResetInput()
        {
            lock (stateGate)
            {
                physical.Clear();
                SessionResets++;
            }
        }
        public static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        private void Pump()
        {
            threadId = GetCurrentThreadId();
            PeekMessage(out _, 0, 0, 0, 0); // Ensure the thread queue exists before Stop can post.
            try
            {
                hook = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
                if (hook == 0)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                focusHook = SetWinEventHook(3, 3, 0, foregroundChanged, 0, 0, 0);
                desktopHook = SetWinEventHook(0x20, 0x20, 0, foregroundChanged, 0, 0, 0);
                if (focusHook == 0 || desktopHook == 0)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                SetTimer(0, 1, 1000, 0);
                ready.Set();
                while (!disposed && GetMessage(out var message, 0, 0, 0) > 0)
                {
                    if (message.Id == 0x113)
                    {
                        if (!OwnsForeground)
                            SetGameActive(false);
                        // Windows may silently remove a timed-out low-level hook. Replace it
                        // with overlap, never deliberately leave a gap between installations.
                        var fresh = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
                        if (fresh != 0)
                        {
                            var old = hook;
                            hook = fresh;
                            UnhookWindowsHookEx(old);
                        }
                    }
                    TranslateMessage(ref message);
                    DispatchMessage(ref message);
                }
            }
            catch (Exception e) { error = e; ready.Set(); }
            finally { if (focusHook != 0) UnhookWinEvent(focusHook); if (desktopHook != 0) UnhookWinEvent(desktopHook); if (hook != 0) { UnhookWindowsHookEx(hook); hook = 0; } }
        }
        private nint OnKey(int code, nint w, nint l)
        {
            if (code < 0 || disposed)
                return CallNextHookEx(hook, code, w, l);
            // Check the native foreground on EVERY callback, before even reading the key.
            // Also verify the desktop itself is receiving input; errors pass through.
            lock (stateGate)
                if (!focus.Accepts(GetForegroundWindow(), DesktopReceivesInput))
                    return CallNextHookEx(hook, code, w, l);
            var data = Marshal.PtrToStructure<HookData>(l);
            var msg = (int)w;
            if (msg is 0x100 or 0x101 or 0x104 or 0x105)
            {
                lock (stateGate)
                {
                    if (!focus.Accepts(GetForegroundWindow(), DesktopReceivesInput))
                        return CallNextHookEx(hook, code, w, l);
                    physical.Feed((int)data.Key, (int)data.Scan, (data.Flags & 1) != 0, msg is 0x100 or 0x104, (data.Flags & 0x10) != 0, Now);
                }
                return suppress ? 1 : CallNextHookEx(hook, code, w, l);
            }
            return CallNextHookEx(hook, code, w, l);
        }
        public void Dispose()
        {
            disposed = true;
            if (threadId != 0)
                PostThreadMessage(threadId, 0x12, 0, 0);
            if (Thread.CurrentThread != thread && thread.IsAlive)
                thread.Join(2000);
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct HookData
        {
            public uint Key, Scan, Flags, Time; public nuint Extra;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct Message
        {
            public nint Window; public uint Id; public nuint W; public nint L; public uint Time; public int X, Y; public uint Private;
        }
        [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWinEventHook(uint min, uint max, nint module, EventProc callback, uint process, uint thread, uint flags);
        [DllImport("user32.dll")] private static extern bool UnhookWinEvent(nint hook);
        [DllImport("user32.dll")] private static extern nint GetThreadDesktop(uint thread);
        [DllImport("user32.dll", EntryPoint = "GetUserObjectInformationW", SetLastError = true)] private static extern bool GetUserObjectInformation(nint desktop, int index, out int value, uint length, out uint needed);
        [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
        [DllImport("user32.dll")] private static extern nint GetActiveWindow();
        [DllImport("user32.dll")] private static extern nuint SetTimer(nint window, nuint id, uint ms, nint callback);
        [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWindowsHookEx(int id, HookProc proc, nint module, uint thread);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
        [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint w, nint l);
        [DllImport("user32.dll")] private static extern int GetMessage(out Message message, nint window, uint min, uint max);
        [DllImport("user32.dll")] private static extern bool PeekMessage(out Message message, nint window, uint min, uint max, uint remove);
        [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Message message);
        [DllImport("user32.dll")] private static extern nint DispatchMessage(ref Message message);
        [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint thread, uint msg, nuint w, nint l);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    }

}


