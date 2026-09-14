param([switch]$Samples,[switch]$Verify,[switch]$KeepGoing,[ValidateSet('auto','cuda','cpu')][string]$Device='auto')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$python=Join-Path $root '.local/speech-venv/Scripts/python.exe'
if(!(Test-Path -LiteralPath $python)){throw 'Run tools/setup-speech.ps1 first. Speech generation is an explicit developer task.'}
$arguments=@((Join-Path $PSScriptRoot 'speech/generate.py'),'--device',$Device)
if($Samples){$arguments+='--samples'}
if($KeepGoing){$arguments+='--keep-going'}
if($Verify){$arguments+='--verify'}
& $python @arguments
if($LASTEXITCODE -ne 0){throw 'Speech generation/verification failed; inspect the reported phrase. Existing assets are preserved.'}
