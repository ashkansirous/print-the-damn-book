# Check: open a .dino by command line (Slice 19). A good book lands on the Check step; a missing file and a non-.dino file show the alert.
# Wipes nothing: starts and stops the app only. Usage: open-dino.ps1 -Exe <PrintTheDamnBook.exe> -Dino <book.dino> [-Language sv]
param([Parameter(Mandatory)] [string] $Exe, [Parameter(Mandatory)] [string] $Dino, [string] $Language = 'sv')
. "$PSScriptRoot/_lib.ps1"
$Exe = Resolve-AppExe $Exe

$script:AlertTitle = "couldn't open what Dino Reader sent"
$script:CheckText = 'Check the order and the words'

function Test-Launch {
    # Starts the app with the arguments (Start-App quotes each one, as handover-check does) and returns whichever marker shows
    # first: 'Check', or 'Alert' only when the window shows the expected title and names the file that was passed (-AlertFile).
    # Like handover-check, texts are looked for anywhere in the window, not only under the HandoverAlert element.
    # -Case names the launch in the log and in the timeout message.
    param([string] $Case, [string[]] $Arguments, [string] $AlertFile)
    Add-Report "launch '$Case': args = $(($Arguments | ForEach-Object { '"' + $_ + '"' }) -join ' ')"
    $process = Start-App -Exe $Exe -Arguments $Arguments
    $leaf = if ($AlertFile) { Split-Path $AlertFile -Leaf } else { '' }
    $script:LastNames = @()
    try {
        $window = Get-MainWindow -Process $process
        Add-Report "launch '$Case': waiting for the Check step or the alert naming '$leaf'"
        return Wait-Until -Failure "Launch '$Case' showed neither the Check step nor the expected alert" -Condition {
            $names = Get-AllNames -Root $window
            $script:LastNames = $names
            if (Find-ById -Root $window -Id 'DirectionLeftToRight') { return 'Check' }
            if ($names | Where-Object { $_ -and $_.Contains($script:CheckText) }) { return 'Check' }
            $hasTitle = [bool]($names | Where-Object { $_ -and $_.Contains($script:AlertTitle) })
            $hasFile = $leaf -and [bool]($names | Where-Object { $_ -and $_.Contains($leaf) })
            if ($hasTitle -and $hasFile) { 'Alert' }
        }
    } catch {
        Add-Report "launch '$Case' names seen: $(($script:LastNames | Where-Object { $_ } | Select-Object -First 25) -join ' / ')"
        throw
    } finally { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
}

Complete-Check -Name 'open-dino' -Body {
    $Dino = (Resolve-Path $Dino).Path
    $notes = Join-Path ([IO.Path]::GetTempPath()) 'ptdb-handover-notes.txt'
    Set-Content -Path $notes -Value 'x'
    try {
        $good = Test-Launch -Case 'good .dino' -Arguments @($Dino, '--language', $Language) -AlertFile $Dino
        $missingFile = Join-Path ([IO.Path]::GetTempPath()) 'ptdb-nope.dino'
        $missing = Test-Launch -Case 'missing file' -Arguments @($missingFile) -AlertFile $missingFile
        $bad = Test-Launch -Case '.txt file' -Arguments @($notes) -AlertFile $notes
    } finally { Remove-Item $notes -ErrorAction SilentlyContinue }
    Add-Report "good .dino -> $good; missing file -> $missing; .txt file -> $bad"
    if ($good -ne 'Check') { throw "A good .dino should land on the Check step, got: $good" }
    if ($missing -ne 'Alert') { throw "A missing .dino should show the alert, got: $missing" }
    if ($bad -ne 'Alert') { throw "A .txt file should show the alert, got: $bad" }
    'good book opens the Check step; missing and wrong files show the alert'
}
