param([string]$Executable='artifacts/math-build/KeyLearner.exe')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    $exe=(Resolve-Path -LiteralPath $Executable).Path
    $cases=@()
    foreach($activity in @('HowManyNow','Hiding','MakeNumber','Duel','CannonHop')){
        $cases+=@{Name="complete-$activity";Args=@('--math',$activity,'--math-level','4','--seconds','8')}
    }
    $cases+=@{Name='addition-reward';Args=@('--math','HowManyNow','--math-level','4','--math-a','3','--math-b','2','--math-operation','add','--math-state','correct','--seconds','4')}
    $cases+=@{Name='subtract-zero';Args=@('--math','HowManyNow','--math-level','4','--math-a','2','--math-b','2','--math-operation','subtract','--seconds','4')}
    $cases+=@{Name='hiding-reveal';Args=@('--math','Hiding','--math-level','4','--math-a','5','--math-b','2','--math-state','correct','--seconds','4')}
    $cases+=@{Name='hiding-count';Args=@('--math','Hiding','--math-level','4','--math-a','5','--math-b','2','--math-state','count','--seconds','4')}
    $cases+=@{Name='builder-reward';Args=@('--math','MakeNumber','--math-level','4','--math-a','3','--math-b','5','--math-state','correct','--seconds','3')}
    $cases+=@{Name='duel-equal';Args=@('--math','Duel','--math-level','4','--math-a','3','--math-b','3','--seconds','3')}
    $cases+=@{Name='equation';Args=@('--math','HowManyNow','--math-level','8','--math-a','5','--math-b','2','--math-operation','subtract','--seconds','4')}
    $cases+=@{Name='low-resolution';Args=@('--math','HowManyNow','--math-level','5','--math-a','5','--math-b','2','--math-operation','add','--render-scale','.5','--seconds','4')}
    foreach($case in $cases){
        $image=Join-Path $repo ('artifacts/math-qa-'+$case.Name+'.png')
        # Unique disposable profiles prevent previous smoke runs from changing a case's starting stage.
        $profile=Join-Path $repo ('artifacts/math-qa-profile-'+[Guid]::NewGuid().ToString('N'))
        $arguments=@('--preview','--mute','--width','1366','--height','768','--scenario',$(if($case.Name.StartsWith('complete-')){'math-complete'}else{'math-round'}),'--data',('"'+$profile+'"'),'--screenshot',('"'+$image+'"'))
        $arguments+=$case.Args
        $run=Start-Process $exe -ArgumentList $arguments -PassThru -WindowStyle Hidden
        try{
            if(!$run.WaitForExit(25000)){throw ('Preview timed out: '+$case.Name)}
            if($run.ExitCode -ne 0){throw ('Preview failed: '+$case.Name+'. See '+(Join-Path (Split-Path $exe) 'startup-error.log'))}
            Get-Content -LiteralPath ($image+'.verified.txt')
        }finally{if(!$run.HasExited){Stop-Process -Id $run.Id}}
    }
}finally{Pop-Location}

