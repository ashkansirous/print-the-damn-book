# Scripted desktop check for Slice 19 (run by device-test, not by dotnet test). Needs a built exe and a sample .dino.
# Wipes nothing: it only starts and closes the app, and reads (never writes) HKCU keys the app wrote itself.
# The app skips the "Open with" registration for an exe under a bin or obj folder or a WindowsApps path, so the registry
# check only applies to an exe copied to a normal folder (pass that copy as -Exe).
param([Parameter(Mandatory)][string]$Exe, [Parameter(Mandatory)][string]$Dino, [string]$Language = 'sv')
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$Exe = (Resolve-Path $Exe).Path

function Get-WindowNames($process) {
    $process.Refresh()
    $handle = $process.MainWindowHandle
    if ($handle -eq [IntPtr]::Zero) { return @() }
    try {
        $root = [Windows.Automation.AutomationElement]::FromHandle($handle)
        return @($root.FindAll('Descendants', [Windows.Automation.Condition]::TrueCondition) | ForEach-Object { $_.Current.Name })
    } catch { return @() }
}

function Test-Window($arguments, $needle) {
    $p = Start-Process $Exe -ArgumentList $arguments -PassThru
    try {
        $deadline = (Get-Date).AddSeconds(20)
        $found = $false
        while (-not $found -and (Get-Date) -lt $deadline) {
            Start-Sleep -Milliseconds 500
            $found = [bool](Get-WindowNames $p | Where-Object { $_ -match [regex]::Escape($needle) })
        }
        $found
    } finally { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
}

$handlerKeys = @(
    'HKCU:\Software\Classes\.dino\OpenWithProgids',
    'HKCU:\Software\Classes\PrintTheDamnBook.dino',
    'HKCU:\Software\Classes\Applications\PrintTheDamnBook.exe')
$progIdName = 'PrintTheDamnBook.dino'
$hadProgIdValue = $false
$openWithKey = Get-Item $handlerKeys[0] -ErrorAction SilentlyContinue
if ($openWithKey) { $hadProgIdValue = $openWithKey.GetValueNames() -contains $progIdName }
$existedBefore = @($handlerKeys | ForEach-Object { Test-Path $_ })

$alertText = "couldn't open what Dino Reader sent"
$bad = Join-Path $env:TEMP 'handover-notes.txt'; Set-Content $bad 'x'
$goodArgs = "`"$Dino`" --language $Language"
$missingArgs = "`"$env:TEMP\nope.dino`""
$badArgs = "`"$bad`""
$goodResult = Test-Window -arguments $goodArgs -needle 'Check the order and the words'
$missingResult = Test-Window -arguments $missingArgs -needle $alertText
$badResult = Test-Window -arguments $badArgs -needle $alertText
"good dino lands on Check step : $goodResult"
"missing file shows the alert  : $missingResult"
"txt file shows the alert      : $badResult"
Remove-Item $bad

$skipped = $Exe -match '(?i)\\(bin|obj|WindowsApps)\\'
if ($skipped) {
    "open-with registration       : skipped on purpose (exe is under bin, obj or WindowsApps); copy the exe elsewhere to check it"
} else {
    $command = (Get-ItemProperty 'HKCU:\Software\Classes\PrintTheDamnBook.dino\shell\open\command' -ErrorAction SilentlyContinue).'(default)'
    $expected = '"' + $Exe + '" "%1"'
    $commandMatches = ($command -eq $expected)
    "open-with command points here : $commandMatches  ($command)"
    $progIds = Get-Item 'HKCU:\Software\Classes\.dino\OpenWithProgids' -ErrorAction SilentlyContinue
    "OpenWithProgids registered    : $([bool]($progIds -and $progIds.GetValueNames() -contains 'PrintTheDamnBook.dino'))"
}
"default .dino handler untouched: $((Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.dino\UserChoice' -ErrorAction SilentlyContinue).ProgId)  (compare with the value before the run)"

# Cleanup: remove only what this run's app launches created (keys that did not exist before it started).
if (-not $existedBefore[0]) {
    Remove-Item $handlerKeys[0] -Recurse -Force -ErrorAction SilentlyContinue
} elseif (-not $hadProgIdValue) {
    Remove-ItemProperty -Path $handlerKeys[0] -Name $progIdName -ErrorAction SilentlyContinue
}
foreach ($i in 1..2) {
    if (-not $existedBefore[$i]) { Remove-Item $handlerKeys[$i] -Recurse -Force -ErrorAction SilentlyContinue }
}

# Exit 0 only when every launch case passed (and, when it was checked, the open-with command points at this exe).
$failures = @()
if (-not $goodResult) { $failures += 'good .dino did not land on the Check step' }
if (-not $missingResult) { $failures += 'missing file showed no alert' }
if (-not $badResult) { $failures += '.txt file showed no alert' }
if (-not $skipped -and -not $commandMatches) { $failures += 'open-with command does not point at this exe' }
if ($failures.Count) { Write-Host "FAIL  handover-check: $($failures -join '; ')"; exit 1 }
Write-Host 'PASS  handover-check: launch cases passed'
exit 0
