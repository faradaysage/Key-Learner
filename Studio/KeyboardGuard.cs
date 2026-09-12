using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
namespace KeyLearner.Studio;

/// <summary>Session-only interception, on a dedicated message-pump thread.
/// This is not Windows Keyboard Filter and cannot intercept the secure desktop.</summary>
public sealed class KeyboardGuard : IDisposable
{
    private delegate nint HookProc(int code,nint w,nint l);
    private readonly HookProc callback;
    private readonly KeyTransitionBuffer events=new();
    private readonly PhysicalKeyboard physical;
    public string Diagnostics=>$"scan repairs {physical.RemappedReleases+physical.RepairedReleases}; ignored packets {physical.IgnoredPackets}";
    private readonly ManualResetEventSlim ready=new();
    private readonly Thread thread;
    private nint hook;
    private uint threadId;
    private Exception? error;
    private volatile bool disposed;
    private readonly bool suppress;
    public int Recoveries=>events.Recoveries;
    public KeyboardGuard(bool suppress=true)
    {
        if(!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Protected play requires Windows.");
        this.suppress=suppress;physical=new(events);
        callback=OnKey;
        thread=new Thread(Pump) { IsBackground=true, Name="KeyLearner keyboard guard" };
        thread.Start();
        if(!ready.Wait(TimeSpan.FromSeconds(5))) { Dispose(); throw new TimeoutException("Keyboard protection did not start."); }
        if(error!=null) { Dispose(); throw new InvalidOperationException("Keyboard protection could not start.",error); }
    }
    public bool TryRead(out KeyEvent e) => events.TryRead(out e);
    public static double Now => Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
    private void Pump()
    {
        threadId=GetCurrentThreadId();
        PeekMessage(out _,0,0,0,0); // Ensure the thread queue exists before Stop can post.
        try
        {
            // Track already-held physical keys so startup cannot manufacture a clean chord.
            for(var k=8;k<256;k++)
                if(k is not (16 or 17 or 18) && (GetAsyncKeyState(k)&0x8000)!=0){uint scan=MapVirtualKey((uint)k,4);physical.Seed(k,(int)(scan&255),(scan&0xff00)==0xe000);}
            hook=SetWindowsHookEx(13,callback,GetModuleHandle(null),0);
            if(hook==0) throw new Win32Exception(Marshal.GetLastWin32Error());
            SetTimer(0,1,1000,0);
            ready.Set();
            while(!disposed && GetMessage(out var message,0,0,0)>0)
            {
                if(message.Id==0x113)
                {
                    // Windows may silently remove a timed-out low-level hook. Replace it
                    // with overlap, never deliberately leave a gap between installations.
                    var fresh=SetWindowsHookEx(13,callback,GetModuleHandle(null),0);
                    if(fresh!=0){var old=hook;hook=fresh;UnhookWindowsHookEx(old);}
                }
                TranslateMessage(ref message);DispatchMessage(ref message);
            }
        }
        catch(Exception e) { error=e; ready.Set(); }
        finally { if(hook!=0) { UnhookWindowsHookEx(hook); hook=0; } }
    }
    private nint OnKey(int code,nint w,nint l)
    {
        if(code<0 || disposed) return CallNextHookEx(hook,code,w,l);
        var data=Marshal.PtrToStructure<HookData>(l);
        var msg=(int)w;
        if(msg is 0x100 or 0x101 or 0x104 or 0x105)
        {
            physical.Feed((int)data.Key,(int)data.Scan,(data.Flags&1)!=0,msg is 0x100 or 0x104,(data.Flags&0x10)!=0,Now);
            return suppress ? 1 : CallNextHookEx(hook,code,w,l); // Probe mode never intercepts input.
        }
        return CallNextHookEx(hook,code,w,l);
    }
    public void Dispose()
    {
        disposed=true;
        if(threadId!=0) PostThreadMessage(threadId,0x12,0,0);
        if(Thread.CurrentThread!=thread && thread.IsAlive) thread.Join(2000);
    }
    [StructLayout(LayoutKind.Sequential)] private struct HookData { public uint Key,Scan,Flags,Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct Message { public nint Window; public uint Id; public nuint W; public nint L; public uint Time; public int X,Y; public uint Private; }
    [DllImport("user32.dll")] private static extern uint MapVirtualKey(uint code,uint type);
    [DllImport("user32.dll")] private static extern nuint SetTimer(nint window,nuint id,uint ms,nint callback);
    [DllImport("user32.dll",SetLastError=true)] private static extern nint SetWindowsHookEx(int id,HookProc proc,nint module,uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook,int code,nint w,nint l);
    [DllImport("user32.dll")] private static extern int GetMessage(out Message message,nint window,uint min,uint max);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out Message message,nint window,uint min,uint max,uint remove);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll")] private static extern nint DispatchMessage(ref Message message);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint thread,uint msg,nuint w,nint l);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
}
