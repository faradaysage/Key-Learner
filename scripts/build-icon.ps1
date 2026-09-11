# Convert the generated master to a multi-resolution Windows ICO and runtime PNG.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Split-Path $PSScriptRoot -Parent
$source=[Drawing.Image]::FromFile((Join-Path $root 'Content/Branding/keylearner-logo.png'))
$frames=@()
try {
 foreach($size in @(16,20,24,32,40,48,64,96,128,256)) {
  $bitmap=[Drawing.Bitmap]::new($size,$size)
  $graphics=[Drawing.Graphics]::FromImage($bitmap)
  $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $graphics.DrawImage($source,0,0,$size,$size)
  $graphics.Dispose()
  $memory=[IO.MemoryStream]::new();$bitmap.Save($memory,[Drawing.Imaging.ImageFormat]::Png)
  $frames+=,@{Size=$size;Bytes=$memory.ToArray()}
  if($size -eq 256){$bitmap.Save((Join-Path $root 'Content/Branding/window-icon.png'),[Drawing.Imaging.ImageFormat]::Png)}
  $memory.Dispose();$bitmap.Dispose()
 }
 $file=[IO.File]::Create((Join-Path $root 'Content/Branding/KeyLearner.ico'));$writer=[IO.BinaryWriter]::new($file)
 try {
  $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$frames.Count)
  $offset=6+16*$frames.Count
  foreach($frame in $frames){$side=if($frame.Size -eq 256){0}else{$frame.Size};$writer.Write([byte]$side);$writer.Write([byte]$side);$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frame.Bytes.Length);$writer.Write([uint32]$offset);$offset+=$frame.Bytes.Length}
  foreach($frame in $frames){$writer.Write([byte[]]$frame.Bytes)}
 } finally {$writer.Dispose();$file.Dispose()}
} finally {$source.Dispose()}
