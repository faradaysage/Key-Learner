# Developer-only. Downloads an isolated interpreter and venv; no PATH/registry/global package changes.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$uvFolder=Join-Path $root '.local/uv'
$uv=Join-Path $uvFolder 'uv.exe'
if(!(Test-Path -LiteralPath $uv)){
 New-Item -ItemType Directory -Force $uvFolder | Out-Null
 $archive=Join-Path $uvFolder 'uv.zip'
 Invoke-WebRequest 'https://github.com/astral-sh/uv/releases/download/0.12.13/uv-x86_64-pc-windows-msvc.zip' -OutFile $archive
 if((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne 'a86c9dc7bad9b03f388583b7187c05fe9951c2e0d392217e8fd43d97787f6ec2'){throw 'uv archive checksum mismatch'}
 Expand-Archive -LiteralPath $archive -DestinationPath $uvFolder -Force
}
$oldPython=$env:UV_PYTHON_INSTALL_DIR
$oldCache=$env:UV_CACHE_DIR
try {
 $env:UV_PYTHON_INSTALL_DIR=Join-Path $root '.local/python'
 $env:UV_CACHE_DIR=Join-Path $root '.local/uv-cache'
 & $uv python install 3.11.16 --no-bin
 if($LASTEXITCODE -ne 0){throw 'Local Python setup failed'}
 $environment=Join-Path $root '.local/speech-venv'
 $python=Join-Path $environment 'Scripts/python.exe'
 if(!(Test-Path -LiteralPath $python)){
  & $uv venv $environment --python 3.11.16 --managed-python
  if($LASTEXITCODE -ne 0){throw 'Speech venv setup failed'}
 }
 & $uv pip install --python $python 'torch==2.6.0+cu124' 'torchaudio==2.6.0+cu124' --index-url https://download.pytorch.org/whl/cu124
 if($LASTEXITCODE -ne 0){throw 'Pinned PyTorch setup failed'}
 & $uv pip install --python $python -r (Join-Path $PSScriptRoot 'speech/requirements.lock')
 if($LASTEXITCODE -ne 0){throw 'Speech dependency setup failed'}
 & $python -c "import torch, chatterbox, perth; assert perth.PerthImplicitWatermarker is not None; print('torch:',torch.__version__,'CUDA available:',torch.cuda.is_available()); print('GPU:',torch.cuda.get_device_name(0) if torch.cuda.is_available() else 'CPU mode')"
 if($LASTEXITCODE -ne 0){throw 'Speech environment verification failed'}
 if(Get-Command nvidia-smi -ErrorAction SilentlyContinue){
  & $python -c "import torch; assert torch.cuda.is_available(), 'NVIDIA tooling was found but CUDA is unavailable; check the driver/toolchain before generating the catalog.'"
  if($LASTEXITCODE -ne 0){throw 'NVIDIA CUDA verification failed'}
 }
} finally {$env:UV_PYTHON_INSTALL_DIR=$oldPython;$env:UV_CACHE_DIR=$oldCache}
