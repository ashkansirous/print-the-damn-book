<#
.SYNOPSIS
  Desktop acceptance run in the shared rtst-desktop-test VM (Slice 21): take RTST's host lock, restore 'clean', copy the exe and the tools/desktop-test scripts in,
  run them, collect the report, restore 'clean' again. Nothing is installed on, or shown by, the host.
.DESCRIPTION
  Without -Package the exe is built first (Build-TestPackage.ps1, outside the VM lock). -Dino is a sample .dino for the two
  Dino launch checks (none ships in the repo: without it those checks are skipped and said so). The VM has no network
  adapter, so the store-dialog check is never run there: it is SKIPped in the VM, and runs on the host (the user's REAL desktop, with the
  internet; it may move the real cursor and click) only when you pass -HostStore together with -AcknowledgeHostDesktop; -HostStore alone is refused.
  -StepTimeoutSec (default 60) is how long each check step may wait for a control. -ResultsDir (default tmp/ptdb-desktop-test/results in this worktree)
  gets one stamped subfolder per run. -Only naming a check that would be skipped exits 3 (0 with -AllowSkip) without touching the VM. A wait with no progress for -StallMin minutes screenshots and throws.
  The VM, its checkpoints and its password are RTST's: this script only restores 'clean' (before and after) and never creates or changes one.
  Exit codes: 0 every check passed and none was skipped; 1 a check or the harness failed; 3 nothing failed but a check was skipped
  (no -Dino, no -HostStore); -AllowSkip turns 3 into 0; 2 the final restore of 'clean' failed (the VM may be left dirty or running: reset it
  with Reset-TestVm.ps1 before anyone uses it). -WhatIf prints what would happen and touches no VM.
.EXAMPLE
  .\Invoke-DesktopTest.ps1 -Dino D:/samples/story.dino
  .\Invoke-DesktopTest.ps1 -Only print-to-pdf -WhatIf
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $Package,
    [string] $Dino,
    [string] $Only,
    [switch] $HostStore,
    [switch] $AcknowledgeHostDesktop,
    [switch] $AllowSkip,
    [int] $StallMin = 10,
    [int] $StepTimeoutSec = 60,
    [string] $ResultsDir
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/_common.ps1"
$script:StallMin = $StallMin
if (-not $ResultsDir) { $ResultsDir = Join-Path $script:ScratchRoot 'results' }
$ResultsDir = Join-Path $ResultsDir $script:RunStamp   # a fresh folder per run: results of different runs never merge
if ($HostStore -and -not $AcknowledgeHostDesktop) { throw '-HostStore drives the REAL host desktop (it launches the app, moves focus and may move the real cursor and click). Add -AcknowledgeHostDesktop to confirm you are at the machine and want that.' }
$ChecksDir = (Resolve-Path "$PSScriptRoot/../desktop-test").Path
$GuestExe = "$($script:GuestWorkDir)/pkg/PrintTheDamnBook.exe"
$GuestDino = "$($script:GuestWorkDir)/sample.dino"
$SkippedExitCode = 3
$RestoreFailedExitCode = 2
$GuestChecks = @('open-dino', 'open-folder', 'direction-and-sheet-order', 'print-to-pdf', 'handover-check')
$GuestLog = "$($script:GuestWorkDir)/results/run-all.log"

function Copy-ResultsOut {
    # Pulls the guest's results folder (run log, any screenshots) to the host before the restore.
    param($Session)
    New-Item -ItemType Directory -Force -Path $ResultsDir | Out-Null
    try { Copy-Item -FromSession $Session -Path "$($script:GuestWorkDir)/results/*" -Destination $ResultsDir -Recurse -Force; Write-Host "Results copied to $ResultsDir" }
    catch { Write-Warning "Could not copy the results out: $($_.Exception.Message)" }
}

function Get-RunAllCommand {
    $parts = @("-Exe '$GuestExe'", '-AllowSkip', "-StepTimeoutSec $StepTimeoutSec")   # skips are counted by this script, which knows what the host covers
    if ($Dino) { $parts += "-Dino '$GuestDino'" }
    if ($Only) { $parts += "-Only '$($Only -replace "'", "''")'" }   # a quote in -Only must not end the guest's string
    $parts += '-SkipStore'   # the VM has no network adapter: the store check runs on the host only
    return "& '$($script:GuestWorkDir)/checks/run-all.ps1' $($parts -join ' ')"
}

function Get-GuestCheckNames {
    # The checks run-all runs in the VM (the store check never does), narrowed by -Only and by a missing -Dino.
    $names = @($GuestChecks | Where-Object { $Dino -or $_ -notin 'open-dino', 'handover-check' })
    if ($Only) { $names = @($names | Where-Object { $_ -eq $Only }) }
    return $names
}

function Get-SkippedChecks {
    $skipped = @()
    if (-not $Dino) { $skipped += @('open-dino', 'handover-check' | Where-Object { -not $Only -or $_ -eq $Only }) }
    if (-not $HostStore -and (-not $Only -or $Only -eq 'store-dialog')) { $skipped += 'store-dialog' }
    return $skipped
}

function Invoke-HostStoreCheck {
    # The one check that runs on the host: the VM has no network. Returns the check's exit code.
    param([string] $Exe)
    Write-Host 'Running store-dialog on the REAL HOST desktop (the VM has no network; -AcknowledgeHostDesktop given): do not touch the mouse or keyboard until it ends...'
    $env:PTDB_STEP_TIMEOUT_SEC = "$StepTimeoutSec"
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $ChecksDir 'store-dialog.ps1') -Exe $Exe | Out-Host
    return $LASTEXITCODE
}

function Show-Summary {
    foreach ($name in Get-SkippedChecks) { Write-Host "SKIP  $name$(if ($name -eq 'store-dialog') { ': run it on the host with -HostStore (the VM has no network)' } else { ': no -Dino sample book given' }) (a skip is not a pass)." }
}

function Get-FinalExitCode {
    param([bool] $Failed)
    if ($Failed) { return 1 }
    if (@(Get-SkippedChecks).Count -gt 0 -and -not $AllowSkip) { return $SkippedExitCode }
    return 0
}

function Copy-ToGuest {
    param($Session, [string] $Exe)
    Invoke-Command -Session $Session -ArgumentList $script:GuestWorkDir -ScriptBlock {
        param($dir)
        Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue
        New-Item -ItemType Directory -Force -Path "$dir/pkg", "$dir/checks", "$dir/results" | Out-Null
    }
    Write-Host 'Copying the exe and the checks into the VM...'
    Copy-Item -ToSession $Session -Path $Exe -Destination $GuestExe
    Copy-Item -ToSession $Session -Path "$ChecksDir/*" -Destination "$($script:GuestWorkDir)/checks/" -Recurse
    if ($Dino) { Copy-Item -ToSession $Session -Path $Dino -Destination $GuestDino }
}

if (-not $PSCmdlet.ShouldProcess($script:VmName, "restore '$($script:CheckpointName)', run: $(Get-RunAllCommand), restore again")) {
    Write-Host "WhatIf: exe = $(if ($Package) { $Package } else { 'built by Build-TestPackage.ps1' }); checks from $ChecksDir; the VM is not touched."
    Write-Host "WhatIf: VM checks: $((Get-GuestCheckNames) -join ', '); host store check: $(if ($HostStore) { 'yes' } else { 'no' }); skipped: $((Get-SkippedChecks) -join ', ')."
    return
}
if ($Dino -and -not (Test-Path $Dino)) { throw "-Dino file not found: $Dino" }
if ($Only -and $Only -ne 'store-dialog' -and $Only -notin $GuestChecks) { throw "No check named '$Only' (known: $($GuestChecks -join ', '), store-dialog)." }
if ($Only -eq 'store-dialog' -and -not $HostStore) { throw 'store-dialog runs only on the host: pass -HostStore with -Only store-dialog.' }
if ($Only -and $Only -in (Get-SkippedChecks)) {
    # The selected check cannot run here (no -Dino, or store-dialog without -HostStore): report the skip before touching the VM.
    Show-Summary
    exit $(if ($AllowSkip) { 0 } else { $SkippedExitCode })
}
$exe = if ($Package) { (Resolve-Path $Package).Path } else { @(& "$PSScriptRoot/Build-TestPackage.ps1") | Select-Object -Last 1 }
if (-not $exe -or -not (Test-Path $exe)) { throw 'No exe to test.' }
$hostStoreCode = 0
$runHostStore = $HostStore -and (-not $Only -or $Only -eq 'store-dialog')
if ($Only -eq 'store-dialog') { $hostStoreCode = Invoke-HostStoreCheck -Exe $exe; Show-Summary; exit (Get-FinalExitCode -Failed ($hostStoreCode -ne 0)) }
$credential = Get-VmCredential
$session = $null
$exitCode = 1
$restoreFailed = $false
Enter-VmLock
try {
    Write-Host "Restoring '$($script:CheckpointName)' and starting the VM..."
    Restore-CleanCheckpoint -Start -Quiet
    $session = New-VmSession -Credential $credential
    Remove-PSSession $session -ErrorAction SilentlyContinue
    $session = $null
    Wait-Desktop -Credential $credential
    $session = New-VmSession -Credential $credential
    Copy-ToGuest -Session $session -Exe $exe
    Write-Host "Running the checks in the VM desktop session ($(Get-RunAllCommand))..."
    $run = Invoke-InteractiveScript -Session $session -Command (Get-RunAllCommand) -LogFile $GuestLog -TimeoutSec ($script:CheckBudgetSec * [Math]::Max(1, @(Get-GuestCheckNames).Count))
    Write-Host $run.Output
    Copy-ResultsOut -Session $session
    if (-not $run.Finished) { Write-Host "FAIL  harness: the checks did not finish within the time limit; screen: $(Get-ScreenText (Save-VmScreenshot -Reason 'run-timeout'))" }
    elseif ($run.ExitCode -eq 0) { $exitCode = 0 }
    else { Write-Host "FAIL  run-all exited $($run.ExitCode) in the VM." }
} catch {
    Write-Host "FAIL  harness: $($_.Exception.Message)"
} finally {
    if ($session) { Remove-PSSession $session -ErrorAction SilentlyContinue }
    # Always restored (leaves the shared VM Saved for the next RTST, Dino or PTDB run).
    Write-Host "Restoring '$($script:CheckpointName)'..."
    try { Restore-CleanCheckpoint -Quiet }
    catch {
        $restoreFailed = $true
        Write-Host "FAIL  RESTORE: the final restore of '$($script:CheckpointName)' failed: $($_.Exception.Message)"
        try { Stop-VM -Name $script:VmName -TurnOff -Force -ErrorAction Stop } catch { Write-Host "FAIL  RESTORE: and the VM could not be turned off either: $($_.Exception.Message)" }
        Write-Host "FAIL  RESTORE: the shared VM $($script:VmName) is NOT at '$($script:CheckpointName)' (it may be dirty or running). Run Reset-TestVm.ps1 before anyone uses it."
    }
    Exit-VmLock
}
# The host store check runs after the VM part (validation and the lock are done), so a bad argument or a busy VM never waits behind the host desktop and the internet.
if ($runHostStore -and ($exitCode -ne 0 -or $restoreFailed)) { Write-Host 'SKIP  store-dialog: not run on the host because the VM run or the final restore failed (a skip is not a pass).' }
elseif ($runHostStore) { $hostStoreCode = Invoke-HostStoreCheck -Exe $exe }
Show-Summary
if ($restoreFailed) { exit $RestoreFailedExitCode }
exit (Get-FinalExitCode -Failed (($exitCode -ne 0) -or ($hostStoreCode -ne 0)))
