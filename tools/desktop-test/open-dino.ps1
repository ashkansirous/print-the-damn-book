# Check: open a .dino by command line (Slice 19). A good book lands on the Check step; a missing file and a non-.dino file show the alert.
# Wipes nothing: starts and stops the app only. Usage: open-dino.ps1 -Exe <PrintTheDamnBook.exe> -Dino <book.dino> [-Language sv]
param([Parameter(Mandatory)] [string] $Exe, [Parameter(Mandatory)] [string] $Dino, [string] $Language = 'sv')
. "$PSScriptRoot/_lib.ps1"
$Exe = Resolve-AppExe $Exe

function Test-Launch {
    # Starts the app with the arguments and returns the AutomationId of whichever marker shows first ('Check' or 'Alert').
    param([string[]] $Arguments)
    $process = Start-App -Exe $Exe -Arguments $Arguments
    try {
        $window = Get-MainWindow -Process $process
        return Wait-Until -Failure 'Neither the Check step nor the alert appeared' -Condition {
            if (Find-ById -Root $window -Id 'DirectionLeftToRight') { 'Check' }
            elseif (Find-ById -Root $window -Id 'HandoverAlert') { 'Alert' }
        }
    } finally { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
}

Complete-Check -Name 'open-dino' -Body {
    $Dino = (Resolve-Path $Dino).Path
    $notes = Join-Path ([IO.Path]::GetTempPath()) 'ptdb-handover-notes.txt'
    Set-Content -Path $notes -Value 'x'
    try {
        $good = Test-Launch -Arguments @($Dino, '--language', $Language)
        $missing = Test-Launch -Arguments @((Join-Path ([IO.Path]::GetTempPath()) 'ptdb-nope.dino'))
        $bad = Test-Launch -Arguments @($notes)
    } finally { Remove-Item $notes -ErrorAction SilentlyContinue }
    Add-Report "good .dino -> $good; missing file -> $missing; .txt file -> $bad"
    if ($good -ne 'Check') { throw "A good .dino should land on the Check step, got: $good" }
    if ($missing -ne 'Alert') { throw "A missing .dino should show the alert, got: $missing" }
    if ($bad -ne 'Alert') { throw "A .txt file should show the alert, got: $bad" }
    'good book opens the Check step; missing and wrong files show the alert'
}
