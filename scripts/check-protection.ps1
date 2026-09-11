# Read-only compatibility check. No Windows configuration is changed.
$edition = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion').EditionID
$supported = $edition -match 'Enterprise|Education|IoT'
[pscustomobject]@{
    Edition = $edition
    KeyboardFilterEditionEligible = $supported
    ApplicationGuardIsAbsoluteIsolation = $false
    NextStep = if ($supported) { 'Verify Keyboard Filter feature, kiosk policy, breakout and hardware behavior on the target.' } else { 'Keyboard Filter is not included in this edition. Choose an eligible dedicated target for stricter OS filtering.' }
}
