<#
.SYNOPSIS
  Runs every desktop check against one PrintTheDamnBook.exe and prints one PASS/FAIL line per check (Slice 21).
.DESCRIPTION
  Each check is a separate script run in its own Windows PowerShell 5.1 process (it exits 0 or 1). Run this inside the
  test VM (tools/desktop-vm/Invoke-DesktopTest.ps1 does), never on the developer's own desktop. -WhatIf lists the
  commands and runs nothing. The store check needs the internet; skip it with -SkipStore where the VM is offline (it then runs
  on the host: tools/desktop-vm/Invoke-DesktopTest.ps1 -HostStore).
  A skipped check prints a SKIP line and is never a pass. Exit code 0 when every selected check ran and passed, 1 when any failed,
  3 when none failed but some were skipped (no -Dino, -SkipStore); -AllowSkip turns that 3 into 0.
.EXAMPLE
  .\run-all.ps1 -Exe C:\desktop-test\pkg\PrintTheDamnBook.exe -Dino C:\desktop-test\sample.dino
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)] [string] $Exe,
    [string] $Dino,
    [string] $Only,
    [switch] $SkipStore,
    [switch] $AllowSkip,
    [int] $StepTimeoutSec = 60
)
$ErrorActionPreference = 'Stop'
$env:PTDB_STEP_TIMEOUT_SEC = "$StepTimeoutSec"   # read by _lib.ps1 in each check's process
$ShellExe = 'powershell.exe'
$SkippedExitCode = 3

# Order matters little: each check starts and stops its own app.
$Checks = @(
    @{ Name = 'open-dino'; Arguments = @('-Exe', $Exe, '-Dino', $Dino) }
    @{ Name = 'open-folder'; Arguments = @('-Exe', $Exe) }
    @{ Name = 'direction-and-sheet-order'; Arguments = @('-Exe', $Exe) }
    @{ Name = 'print-to-pdf'; Arguments = @('-Exe', $Exe) }
    @{ Name = 'store-dialog'; Arguments = @('-Exe', $Exe) }
    @{ Name = 'handover-check'; Arguments = @('-Exe', $Exe, '-Dino', $Dino) }
)
if ($Only -and $Only -notin $Checks.Name) { throw "No check named '$Only' (known: $($Checks.Name -join ', '))." }
if ($Only) { $Checks = @($Checks | Where-Object { $_.Name -eq $Only }) }
$skipped = @()
foreach ($check in $Checks) {
    if (-not $Dino -and $check.Name -in 'open-dino', 'handover-check') { $skipped += $check.Name; Write-Host "SKIP  $($check.Name): no -Dino sample book given (a skip is not a pass)." }
    elseif ($SkipStore -and $check.Name -eq 'store-dialog') { $skipped += $check.Name; Write-Host "SKIP  $($check.Name): -SkipStore (needs the internet; run it on the host with Invoke-DesktopTest -HostStore)." }
}
$Checks = @($Checks | Where-Object { $_.Name -notin $skipped })
if ($Only -and $Checks.Count -eq 0 -and $skipped.Count -gt 0 -and -not $WhatIfPreference) {
    Write-Host "FAIL  -Only $Only was skipped, so nothing ran: a run that checked nothing is not a pass."
    exit 1
}

$failed = 0
foreach ($check in $Checks) {
    $script = Join-Path $PSScriptRoot "$($check.Name).ps1"
    if (-not $PSCmdlet.ShouldProcess($script, 'run check')) { continue }
    Write-Host "Running $($check.Name)..."
    & $ShellExe -NoProfile -ExecutionPolicy Bypass -File $script @($check.Arguments)
    if ($LASTEXITCODE -ne 0) { $failed++ }
}
Write-Host ("{0} of {1} check(s) failed, {2} skipped{3}." -f $failed, $Checks.Count, $skipped.Count, $(if ($skipped.Count) { ' (' + ($skipped -join ', ') + ')' } else { '' }))
if ($failed -gt 0) { exit 1 }
if ($skipped.Count -gt 0 -and -not $AllowSkip) { exit $SkippedExitCode }
exit 0
