param([Parameter(Mandatory=$true)][string]$CaptureMap)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$destination=Join-Path $root 'UnityPort/Assets/KeyLearner/Resources/Previews'
New-Item -ItemType Directory -Force $destination | Out-Null
Add-Type -AssemblyName System.Drawing
foreach($entry in (Get-Content -LiteralPath $CaptureMap -Raw | ConvertFrom-Json)){
 if($entry.mode -lt 0 -or $entry.mode -gt 11){throw 'Unknown minigame ID.'}
 $source=[IO.Path]::GetFullPath((Join-Path $root $entry.path))
 $picture=[Drawing.Image]::FromFile($source)
 try{
  $crop=if($entry.crop){$entry.crop}else{@(0,0,1,1)}
  if($crop.Count -ne 4 -or $crop[0] -lt 0 -or $crop[1] -lt 0 -or $crop[2] -le 0 -or $crop[3] -le 0 -or ($crop[0]+$crop[2]) -gt 1 -or ($crop[1]+$crop[3]) -gt 1){throw 'Crop must remain inside the actual gameplay image.'}
  $rectangle=[Drawing.RectangleF]::new($picture.Width*$crop[0],$picture.Height*$crop[1],$picture.Width*$crop[2],$picture.Height*$crop[3])
  $bitmap=[Drawing.Bitmap]::new(384,264)
  $graphics=[Drawing.Graphics]::FromImage($bitmap)
  try{
   $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
   # Fit the crop without stretching objects; the picker repeats this same aspect ratio.
   $ratio=384.0/264
   if($rectangle.Width/$rectangle.Height -gt $ratio){$width=$rectangle.Height*$ratio;$rectangle.X+=($rectangle.Width-$width)/2;$rectangle.Width=$width}else{$height=$rectangle.Width/$ratio;$rectangle.Y+=($rectangle.Height-$height)/2;$rectangle.Height=$height}
   $graphics.DrawImage($picture,[Drawing.RectangleF]::new(0,0,384,264),$rectangle,[Drawing.GraphicsUnit]::Pixel)
   $bitmap.Save((Join-Path $destination ('mode-{0:D2}.jpg' -f [int]$entry.mode)),[Drawing.Imaging.ImageFormat]::Jpeg)
  }finally{$graphics.Dispose();$bitmap.Dispose()}
 }finally{$picture.Dispose()}
}
Write-Output 'Updated picker thumbnails from actual player captures.'
