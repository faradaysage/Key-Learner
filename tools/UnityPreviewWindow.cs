using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;
using System.Threading.Tasks;

// Bounded UI acceptance operations exclusively on the preview process supplied by the runner.
public static class UnityPreviewWindow
{
    // The player replaces its preview reports atomically. A polling reader must
    // allow deletion of the old inode/file entry while its own handle is open.
    public static string ReadSharedReport(string path)
    {
        using (var stream = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete))
        using (var reader = new System.IO.StreamReader(stream))
            return reader.ReadToEnd();
    }
    [StructLayout(LayoutKind.Sequential)] struct Point { public int X,Y; }
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct MouseInput { public int X,Y; public uint Data,Flags,Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] struct KeyInput { public ushort Key,Scan; public uint Flags,Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Explicit)] struct InputData { [FieldOffset(0)] public MouseInput Mouse; [FieldOffset(0)] public KeyInput Keyboard; }
    [StructLayout(LayoutKind.Sequential)] struct Input { public uint Type;public InputData Data; }
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window,int command);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr window,out Rect rect);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr window,ref Point point);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")] static extern uint SendInput(uint count,Input[] events,int size);
    [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    delegate bool EnumWindowProc(IntPtr window,IntPtr state);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowProc callback,IntPtr state);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder name,int capacity);
    static IntPtr Window(int processId,bool includeMinimized=false)
    {
        using(var process=Process.GetProcessById(processId)){
            process.Refresh();IntPtr window=IntPtr.Zero;
            {
                EnumWindows((candidate,state)=>{
                    GetWindowThreadProcessId(candidate,out uint candidateOwner);if(candidateOwner!=processId)return true;
                    var name=new StringBuilder(128);GetClassName(candidate,name,name.Capacity);
                    if(name.ToString()!="UnityWndClass" || !IsWindowVisible(candidate))return true;
                    if((!GetClientRect(candidate,out Rect bounds)||bounds.Right<64||bounds.Bottom<64) && !(includeMinimized&&IsIconic(candidate)))return true;
                    window=candidate;return false;
                },IntPtr.Zero);
            }
            if(window==IntPtr.Zero)throw new InvalidOperationException("Preview has no visible Unity window yet.");
            GetWindowThreadProcessId(window,out uint owner);
            if(owner!=processId)throw new InvalidOperationException("Window does not belong to the launched preview.");
            return window;
        }
    }
    public static bool Focus(int processId){var window=Window(processId);ShowWindow(window,9);SetForegroundWindow(window);return GetForegroundWindow()==window;}
    public static bool IsMaximized(int processId)=>IsZoomed(Window(processId));
    public static void Minimize(int processId){ShowWindow(Window(processId),6);}
    public static void Restore(int processId){var window=Window(processId,true);ShowWindow(window,9);SetForegroundWindow(window);}
    // Only navigation/confirm keys accepted by this scoped gameplay acceptance helper.
    public static Task HoldRightAsync(int processId,int milliseconds)=>Task.Run(()=>HoldRight(processId,milliseconds));
    public static void HoldRight(int processId,int milliseconds)=>HoldGameKey(processId,39,milliseconds);
    public static Task HoldGameKeyAsync(int processId,int key,int milliseconds)=>Task.Run(()=>HoldGameKey(processId,key,milliseconds));
    public static void HoldGameKey(int processId,int key,int milliseconds)
    {
        if(key!=13 && key!=32 && key!=112 && key!=162 && key!=163 && (key<37 || key>40)) throw new ArgumentOutOfRangeException(nameof(key));
        if(milliseconds<1||milliseconds>2000)throw new ArgumentOutOfRangeException(nameof(milliseconds));
        IntPtr window=Window(processId);
        if(GetForegroundWindow()!=window)throw new InvalidOperationException("Refusing key input because the preview is not foreground.");
        var down=new Input{Type=1,Data=new InputData{Keyboard=new KeyInput{Key=(ushort)key,Flags=(key==163 || (key>=37 && key<=40))?1u:0u}}};
        var up=new Input{Type=1,Data=new InputData{Keyboard=new KeyInput{Key=(ushort)key,Flags=(key==163 || (key>=37 && key<=40))?3u:2u}}};
        bool pressed=false;
        try{
            if(SendInput(1,new[]{down},Marshal.SizeOf<Input>())!=1)throw new InvalidOperationException("Gameplay key press was not delivered.");
            pressed=true;var elapsed=Stopwatch.StartNew();
            while(elapsed.ElapsedMilliseconds<milliseconds){if(GetForegroundWindow()!=window)throw new InvalidOperationException("Preview lost foreground during gameplay key test.");Thread.Sleep(10);}
        }finally{if(pressed)SendInput(1,new[]{up},Marshal.SizeOf<Input>());}
    }
    public static void Click(int processId,int x,int y,bool right)
    {
        IntPtr dpi=SetThreadDpiAwarenessContext(new IntPtr(-4));Point previous=default;bool saved=false;
        try{
            IntPtr window=Window(processId);
            if(GetForegroundWindow()!=window)throw new InvalidOperationException("Refusing pointer input because the preview is not foreground.");
            if(!GetClientRect(window,out Rect rect)||x<0||y<0||x>=rect.Right||y>=rect.Bottom)throw new ArgumentOutOfRangeException("Preview client coordinates");
            var point=new Point{X=x,Y=y};if(!ClientToScreen(window,ref point))throw new InvalidOperationException("Cannot locate preview client area.");
            saved=GetCursorPos(out previous);SetCursorPos(point.X,point.Y);
            if(GetForegroundWindow()!=window)throw new InvalidOperationException("Preview lost foreground before pointer input.");
            var down=new Input{Type=0,Data=new InputData{Mouse=new MouseInput{Flags=right?8u:2u}}};
            var up=new Input{Type=0,Data=new InputData{Mouse=new MouseInput{Flags=right?16u:4u}}};
            if(SendInput(1,new[]{down},Marshal.SizeOf<Input>())!=1)throw new InvalidOperationException("Pointer press was not delivered.");
            Thread.Sleep(85);SendInput(1,new[]{up},Marshal.SizeOf<Input>());
        }finally{if(saved)SetCursorPos(previous.X,previous.Y);if(dpi!=IntPtr.Zero)SetThreadDpiAwarenessContext(dpi);}
    }
}
