param([string]$Executable="bin/Release/net8.0-windows/KeyLearner.exe")
$ErrorActionPreference='Stop'
Add-Type @"
using System.Runtime.InteropServices;
public static class ShortcutProbe {
 [DllImport("user32.dll",SetLastError=true)] public static extern bool SystemParametersInfo(uint a,uint p,[In,Out]uint[] v,uint f);
 public static uint[] Read(){var result=new uint[3];uint[] actions={0x3A,0x32,0x34};int[] sizes={2,6,2};for(int i=0;i<3;i++){var v=new uint[sizes[i]];v[0]=(uint)sizes[i]*4;if(!SystemParametersInfo(actions[i],v[0],v,0))throw new System.Exception("SPI read failed");result[i]=v[1];}return result;}
}
"@
$before=[ShortcutProbe]::Read()
$normal=Start-Process $Executable -ArgumentList '--probe-accessibility' -PassThru -Wait -WindowStyle Hidden
if($normal.ExitCode -ne 0){throw 'Normal probe failed'}
$after=[ShortcutProbe]::Read()
if(($before -join ',') -ne ($after -join ',')){throw 'Normal restoration mismatch'}
$crash=Start-Process $Executable -ArgumentList @('--probe-accessibility','--wait') -PassThru -WindowStyle Hidden
try {
 $applied=$false
 for($i=0;$i -lt 40;$i++){Start-Sleep -Milliseconds 100;$during=[ShortcutProbe]::Read();if(($during | Where-Object {($_ -band 12) -ne 0}).Count -eq 0){$applied=$true;break}}
 if(!$applied){throw 'Shortcut suppression did not apply'}
 Stop-Process -Id $crash.Id
 for($i=0;$i -lt 40;$i++){Start-Sleep -Milliseconds 100;$after=[ShortcutProbe]::Read();if(($before -join ',') -eq ($after -join ',')){break}}
 if(($before -join ',') -ne ($after -join ',')){throw 'Crash restoration mismatch'}
 Write-Output 'PASS native shortcut suppression, normal restoration, forced-exit guardian restoration'
} finally {if(!$crash.HasExited){Stop-Process -Id $crash.Id}}
