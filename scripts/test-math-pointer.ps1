param([string]$Executable='artifacts/math-build/KeyLearner.exe',[switch]$Interactive)
if(!$Interactive){throw 'This test briefly shows a preview and moves the pointer. Pass -Interactive in an unlocked session.'}
$ErrorActionPreference='Stop'
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class MathPointerProbe {
 delegate bool Enumerate(IntPtr window,IntPtr value);
 [DllImport("user32.dll")] static extern bool EnumWindows(Enumerate callback,IntPtr data);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr window,StringBuilder title,int count);
 [StructLayout(LayoutKind.Sequential)] public struct Point {public int X,Y;}
 [StructLayout(LayoutKind.Sequential)] public struct Rect {public int Left,Top,Right,Bottom;}
 [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr window,out Rect rect);
 [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point p);
 [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr window,ref Point p);
 [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int w,int h,uint flags);
 [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr window,int show);
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
 [DllImport("user32.dll")] static extern void mouse_event(uint flags,uint dx,uint dy,uint data,UIntPtr extra);
 public static IntPtr Find(int pid){IntPtr result=IntPtr.Zero;EnumWindows((w,p)=>{uint owner;GetWindowThreadProcessId(w,out owner);var title=new StringBuilder(256);GetWindowText(w,title,256);if(owner==pid&&title.ToString().StartsWith("KeyLearner")){result=w;return false;}return true;},IntPtr.Zero);return result;}
 static bool held;
 public static void Release(){if(held){mouse_event(4,0,0,0,UIntPtr.Zero);held=false;}}
 public static void Mouse(IntPtr window,uint message,int x,int y){
  if(GetForegroundWindow()!=window)throw new Exception("Preview lost foreground; refusing to click");
  var p=new Point{X=x,Y=y};ClientToScreen(window,ref p);SetCursorPos(p.X,p.Y);
  Point actual;GetCursorPos(out actual);if(message==0x200)Console.WriteLine("Pointer requested "+p.X+","+p.Y+" actual "+actual.X+","+actual.Y);
  if(message!=0x200){held=message==0x201;mouse_event(held?2u:4u,0,0,0,UIntPtr.Zero);}
 }
}
"@
$repo=Split-Path $PSScriptRoot -Parent
Push-Location $repo
$run=$null
$oldDpi=[MathPointerProbe]::SetThreadDpiAwarenessContext([IntPtr](-4))
$oldCursor=New-Object MathPointerProbe+Point
[MathPointerProbe]::GetCursorPos([ref]$oldCursor)|Out-Null
try {
    $profile='artifacts/math-native-'+[Guid]::NewGuid().ToString('N')
    $shot=$profile+'.png'
    $run=Start-Process (Resolve-Path $Executable).Path -ArgumentList @('--preview','--math','HowManyNow','--math-level','4','--math-a','3','--math-b','2','--math-operation','add','--mute','--width','1366','--height','768','--scenario','math-pointer','--seconds','20','--data',$profile,'--screenshot',$shot) -WindowStyle Hidden -PassThru
    function Wait-MathState([string]$phase) {
        for($i=0;$i -lt 180;$i++){
            if($run.HasExited){throw 'Preview exited before input was ready'}
            if(Test-Path ($shot+'.state.json')){try{$state=Get-Content ($shot+'.state.json') -Raw|ConvertFrom-Json;if($state.Phase -eq $phase){return $state}}catch{}}
            Start-Sleep -Milliseconds 100
        }
        throw ('Timed out waiting for math state '+$phase)
    }
    $ready=Wait-MathState 'AwaitAnswer'
    if($ready.Answer -ne 5 -or $ready.Choices[3] -ne 5){throw 'Fixed preview layout changed; update the observed click target'}
    $window=[MathPointerProbe]::Find($run.Id)
    if($window -eq 0 -or $run.HasExited){throw 'Preview window unavailable'}
    # Show only this interactive preview for the real pointer check; it never installs the keyboard guard.
    [MathPointerProbe]::ShowWindow($window,5)|Out-Null
    [MathPointerProbe]::SetWindowPos($window,[IntPtr]::Zero,40,40,0,0,5)|Out-Null
    [MathPointerProbe]::SetForegroundWindow($window)|Out-Null
    Start-Sleep -Seconds 2
    $rect=New-Object MathPointerProbe+Rect
    [MathPointerProbe]::GetClientRect($window,[ref]$rect)|Out-Null
    Write-Host "Client $($rect.Right)x$($rect.Bottom); ready $($ready.Phase); image $shot"
    $answerX=[int](1146*$rect.Right/1440);$answerY=[int](779*$rect.Bottom/900)
    [MathPointerProbe]::Mouse($window,0x200,$answerX,$answerY)
    Start-Sleep -Milliseconds 120
    for($i=0;$i -lt 2;$i++){
        [MathPointerProbe]::Mouse($window,0x201,$answerX,$answerY);Start-Sleep -Milliseconds 150
        [MathPointerProbe]::Mouse($window,0x202,$answerX,$answerY);Start-Sleep -Milliseconds 90
    }
    Start-Sleep -Milliseconds 200
    if(Test-Path ($shot+'.input.log')){Get-Content ($shot+'.input.log')|Write-Host}
    $reward=Wait-MathState 'Reward'
    if($reward.Stage -ne 2){throw 'Answer was not awarded exactly once'}
    Start-Sleep -Seconds 2
    [MathPointerProbe]::Mouse($window,0x200,105,52)
    Start-Sleep -Milliseconds 120
    [MathPointerProbe]::Mouse($window,0x201,105,52);Start-Sleep -Milliseconds 150
    [MathPointerProbe]::Mouse($window,0x202,105,52)
    if(!$run.WaitForExit(25000)){throw 'Native pointer preview timed out'}
    if($run.ExitCode -ne 0){Get-Content artifacts/math-build/startup-error.log;throw 'Native pointer regression failed'}
    Get-Content ($shot+'.verified.txt')
    Copy-Item -LiteralPath $shot -Destination artifacts/math-native-pointer.png
}finally{
    [MathPointerProbe]::Release()
    if($run -and !$run.HasExited){Stop-Process -Id $run.Id}
    [MathPointerProbe]::SetCursorPos($oldCursor.X,$oldCursor.Y)|Out-Null
    [MathPointerProbe]::SetThreadDpiAwarenessContext($oldDpi)|Out-Null
    Pop-Location
}
