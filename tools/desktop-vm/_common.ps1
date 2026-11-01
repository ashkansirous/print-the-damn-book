# Shared by Reset-TestVm.ps1 and Invoke-DesktopTest.ps1 (dot-source it). Slice 21.
# A thin runner over the VM that ReadTheStupidText and Dino Reader already share (user's answer, 2026-11-01): it takes RTST's host lock,
# restores the 'clean' checkpoint, and never creates, changes or deletes a VM or a checkpoint. Helpers follow
# D:/ReadTheStupidText/tools/desktop-vm/_common.ps1.

$script:VmName = 'rtst-desktop-test'
$script:CheckpointName = 'clean'
$script:VmUser = 'Administrator'
$script:SecretsFile = 'D:/secrets/readthestupidtext/secrets.env'
$script:SecretKey = 'DESKTOP_VM_PASSWORD'
$script:GuestWorkDir = 'C:/desktop-test'
$script:SessionTimeoutSec = 600
# Worst case for one check script (launch 30 s + several 60 s steps + print 120 s + slow-VM margin); the whole run's limit is this times the number of checks.
$script:CheckBudgetSec = 600
# RTST's lock name, so a PTDB run never overlaps an RTST or Dino run.
$script:LockName = 'Global\rtst-desktop-test-vm'
$script:StallMin = 10
# Per worktree (git-ignored tmp/), so concurrent sessions never share scratch; each run writes into its own stamped subfolder, so old logs and screens never read as this run's.
$script:ScratchRoot = Join-Path (Resolve-Path "$PSScriptRoot/../..").Path 'tmp/ptdb-desktop-test'
$script:RunStamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$script:ScreenshotWidth = 1024
$script:ScreenshotHeight = 768
$script:ScreenshotCimClass = 'Msvm_VirtualSystemManagementService'
$script:VirtualizationNamespace = 'root\virtualization\v2'
# A guest using more CPU than this (percent, from Hyper-V) is working even when nothing else changes.
$script:BusyCpuPercent = 5
$script:DesktopStableSec = 20
$script:DesktopTimeoutMin = 10

function Get-VmCredential {
    if (-not (Test-Path $script:SecretsFile)) { throw "VM password not found: restore $script:SecretsFile (manage-secrets skill)." }
    $line = Select-String -Path $script:SecretsFile -Pattern "^$($script:SecretKey)=(.+)$" | Select-Object -First 1
    if (-not $line) { throw "$($script:SecretKey) is not in $script:SecretsFile." }
    $password = $line.Matches[0].Groups[1].Value.Trim()
    return New-Object System.Management.Automation.PSCredential($script:VmUser, (ConvertTo-SecureString $password -AsPlainText -Force))
}

function Enter-VmLock {
    # One run at a time per host: the VM, its guest folder and its scheduled task are shared by every session.
    $script:VmLock = New-Object System.Threading.Mutex($false, $script:LockName)
    $started = Get-Date
    while ($true) {
        try { if ($script:VmLock.WaitOne(5000)) { return } } catch [System.Threading.AbandonedMutexException] { return }
        Write-Host ("  VM busy (an RTST, Dino or PTDB run holds it), waiting {0:N0} s... (Ctrl+C to give up)" -f ((Get-Date) - $started).TotalSeconds)
    }
}

function Exit-VmLock {
    if ($script:VmLock) { try { $script:VmLock.ReleaseMutex() } catch { } $script:VmLock.Dispose(); $script:VmLock = $null }
}

function Get-VmThumbnail {
    # Asks the host (no guest session needed) for the VM's screen as raw RGB565 bytes.
    $cs = Get-CimInstance -Namespace $script:VirtualizationNamespace -ClassName Msvm_ComputerSystem -Filter "ElementName='$($script:VmName)'"
    $svc = Get-CimInstance -Namespace $script:VirtualizationNamespace -ClassName $script:ScreenshotCimClass
    $out = Invoke-CimMethod -InputObject $svc -MethodName GetVirtualSystemThumbnailImage -Arguments @{
        TargetSystem = $cs; WidthPixels = [uint16]$script:ScreenshotWidth; HeightPixels = [uint16]$script:ScreenshotHeight }
    $pixelBytes = $script:ScreenshotWidth * $script:ScreenshotHeight * 2
    if (-not $out.ImageData -or $out.ImageData.Length -lt $pixelBytes) { throw 'No screen image returned (is the VM running?).' }
    # A short header precedes the pixels; the pixels are the last width*height*2 bytes.
    return [byte[]]$out.ImageData[($out.ImageData.Length - $pixelBytes)..($out.ImageData.Length - 1)]
}

function ConvertTo-Png {
    param([byte[]] $Rgb565, [string] $Path)
    Add-Type -AssemblyName System.Drawing
    $w = $script:ScreenshotWidth
    $h = $script:ScreenshotHeight
    $bmp = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format16bppRgb565)
    try {
        $bits = $bmp.LockBits((New-Object System.Drawing.Rectangle(0, 0, $w, $h)), [System.Drawing.Imaging.ImageLockMode]::WriteOnly, $bmp.PixelFormat)
        for ($y = 0; $y -lt $h; $y++) { [Runtime.InteropServices.Marshal]::Copy($Rgb565, $y * $w * 2, [IntPtr]($bits.Scan0.ToInt64() + $y * $bits.Stride), $w * 2) }
        $bmp.UnlockBits($bits)
        $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $bmp.Dispose() }
}

function Save-VmScreenshot {
    # Saves the VM screen under the scratch folder (never inside RTST's VM folder) and returns the path; $null (with a warning) when it cannot.
    param([string] $Reason = 'screen')
    try {
        $dir = Join-Path (Join-Path $script:ScratchRoot 'screens') $script:RunStamp
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
        $path = Join-Path $dir ("{0}-{1}.png" -f (Get-Date -Format 'yyyyMMdd-HHmmss'), ($Reason -replace '[^A-Za-z0-9_-]', '-'))
        ConvertTo-Png -Rgb565 (Get-VmThumbnail) -Path $path
        return $path
    } catch {
        Write-Warning "Could not save a screenshot: $($_.Exception.Message)"
        return $null
    }
}

function Get-ScreenText {
    param([string] $Path)
    if ($Path) { return $Path }
    return '(screenshot unavailable)'
}

function Get-VmProgressToken {
    # Something that changes while the VM is working: power state, heartbeat, and the disk growing.
    try {
        $vm = Get-VM -Name $script:VmName -ErrorAction Stop
        $disk = (Get-VMHardDiskDrive -VMName $script:VmName | Select-Object -First 1).Path
        return "$($vm.State)|$($vm.Heartbeat)|$([long]((Get-Item $disk).Length / 16MB))"
    } catch { return 'unknown' }
}

function Test-VmBusy {
    try { return ((Get-VM -Name $script:VmName -ErrorAction Stop).CPUUsage -ge $script:BusyCpuPercent) } catch { return $false }
}

function New-StallWatch {
    param([Parameter(Mandatory)] [string] $What)
    return [pscustomobject]@{ What = $What; Token = $null; Since = Get-Date }
}

function Step-StallWatch {
    # Call on every poll with a token that changes when anything moves. No change for StallMin minutes: screenshot and throw.
    param([Parameter(Mandatory)] $Watch, [string] $Token = '')
    $full = "$(Get-VmProgressToken)|$Token"
    if ($full -ne $Watch.Token) { $Watch.Token = $full; $Watch.Since = Get-Date; return }
    if (Test-VmBusy) { $Watch.Since = Get-Date; return }
    if (((Get-Date) - $Watch.Since).TotalMinutes -lt $script:StallMin) { return }
    $png = Save-VmScreenshot -Reason 'stalled'
    throw "stalled: $($Watch.What) (no progress for $($script:StallMin) min); screen: $(Get-ScreenText $png)"
}

function Restore-CleanCheckpoint {
    # Restores the existing checkpoint only; it is never created, changed or deleted here.
    param([switch] $Start, [switch] $Quiet)
    $vm = Get-VM -Name $script:VmName -ErrorAction Stop
    if ($vm.State -notin 'Off', 'Saved') { Stop-VM -Name $script:VmName -TurnOff -Force }
    $snap = Get-VMCheckpoint -VMName $script:VmName -Name $script:CheckpointName -ErrorAction SilentlyContinue
    if (-not $snap) { throw "Checkpoint '$($script:CheckpointName)' not found on $($script:VmName). Do not rebuild it from here: tell the RTST owner (RTST tools/desktop-vm/New-TestVm.ps1)." }
    Restore-VMCheckpoint -VMCheckpoint $snap -Confirm:$false
    if ($Start) { Start-VM -Name $script:VmName | Out-Null }
    if (-not $Quiet) { Write-Host "VM restored to checkpoint '$($script:CheckpointName)'." }
}

function New-VmSession {
    # PowerShell Direct session, retried until the guest is up; a wait with no progress for StallMin minutes screenshots and throws.
    param([Parameter(Mandatory)] [pscredential] $Credential, [int] $TimeoutSec = $script:SessionTimeoutSec)
    $started = Get-Date
    $deadline = $started.AddSeconds($TimeoutSec)
    $nextNote = $started.AddSeconds(30)
    $watch = New-StallWatch -What 'a PowerShell Direct session to the VM'
    $lastError = $null
    while ((Get-Date) -lt $deadline) {
        try { return New-PSSession -VMName $script:VmName -Credential $Credential -ErrorAction Stop }
        catch {
            $lastError = $_.Exception.Message
            if ((Get-Date) -ge $nextNote) { Write-Host ("  waiting for the VM to accept a session, {0:N0} s" -f ((Get-Date) - $started).TotalSeconds); $nextNote = (Get-Date).AddSeconds(30) }
            Step-StallWatch -Watch $watch
            Start-Sleep -Seconds 5
        }
    }
    $png = Save-VmScreenshot -Reason 'no-session'
    throw "No PowerShell Direct session to $($script:VmName) within $TimeoutSec s. Last error: $lastError; screen: $(Get-ScreenText $png)"
}

function Invoke-FreshGuest {
    # Runs a script block in a brand-new session and closes it; returns $null while the guest is not answering.
    param([Parameter(Mandatory)] [pscredential] $Credential, [Parameter(Mandatory)] [scriptblock] $Script)
    $session = $null
    try {
        $session = New-PSSession -VMName $script:VmName -Credential $Credential -ErrorAction Stop
        return Invoke-Command -Session $session -ScriptBlock $Script -ErrorAction Stop
    } catch { return $null }
    finally { if ($session) { Remove-PSSession $session -ErrorAction SilentlyContinue } }
}

function Wait-Desktop {
    # A restored 'clean' cold-boots: PowerShell Direct answers before Administrator's autologon and the shell are up, and an
    # Interactive-logon task registered by then never runs. Ready = explorer and the start menu host exist and stay for a few seconds.
    param([Parameter(Mandatory)] [pscredential] $Credential)
    $started = Get-Date
    $deadline = $started.AddMinutes($script:DesktopTimeoutMin)
    $watch = New-StallWatch -What 'the guest desktop after the restore'
    $stableSince = $null
    while ((Get-Date) -lt $deadline) {
        $up = Invoke-FreshGuest -Credential $Credential -Script { [bool](Get-Process explorer -ErrorAction SilentlyContinue) -and [bool](Get-Process StartMenuExperienceHost -ErrorAction SilentlyContinue) }
        if ($up) {
            if (-not $stableSince) { $stableSince = Get-Date }
            elseif (((Get-Date) - $stableSince).TotalSeconds -ge $script:DesktopStableSec) { return }
        } else { $stableSince = $null }
        Step-StallWatch -Watch $watch -Token "$up"
        Write-Host ("  waiting for the guest desktop, {0:N0} s" -f ((Get-Date) - $started).TotalSeconds)
        Start-Sleep -Seconds 5
    }
    $png = Save-VmScreenshot -Reason 'desktop-timeout'
    throw "The guest desktop did not come up within $($script:DesktopTimeoutMin) min; screen: $(Get-ScreenText $png)"
}

# Runs in the guest desktop session: executes the command file, captures all output, and ALWAYS writes the exit file
# (1 unless the command ended with its own exit code), so a script that throws is reported at once instead of timing out.
$script:TaskWrapper = @'
param([string] $CommandFile, [string] $LogFile, [string] $ExitFile)
$code = 1
try {
    & { . $CommandFile } *> $LogFile
    if ($null -ne $LASTEXITCODE) { $code = $LASTEXITCODE }
} catch {
    ($_ | Out-String) | Add-Content -Path $LogFile
} finally {
    Set-Content -Path $ExitFile -Value $code
}
'@

function Invoke-InteractiveScript {
    # Runs a PowerShell command in the signed-in user's desktop session (UI Automation needs it; a PowerShell Direct session is a
    # separate non-interactive logon) as a one-shot scheduled task. Returns Finished, ExitCode and the captured Output.
    param([Parameter(Mandatory)] $Session, [Parameter(Mandatory)] [string] $Command, [Parameter(Mandatory)] [string] $LogFile, [int] $TimeoutSec = 600)
    return Invoke-Command -Session $Session -ArgumentList $Command, $LogFile, $script:VmUser, $TimeoutSec, $script:TaskWrapper -ScriptBlock {
        param($command, $log, $user, $timeout, $wrapperText)
        $ErrorActionPreference = 'Stop'
        $exitFile = "$log.exit"
        $commandFile = "$log.cmd.ps1"
        $wrapperFile = "$log.wrap.ps1"
        Remove-Item $log, $exitFile -ErrorAction SilentlyContinue
        Set-Content -Path $commandFile -Value $command
        Set-Content -Path $wrapperFile -Value $wrapperText
        $arg = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$wrapperFile`" -CommandFile `"$commandFile`" -LogFile `"$log`" -ExitFile `"$exitFile`""
        $action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $arg
        $principal = New-ScheduledTaskPrincipal -UserId "$env:COMPUTERNAME\$user" -LogonType Interactive -RunLevel Highest
        $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Seconds ($timeout + 30))
        $name = 'ptdb-check'
        Unregister-ScheduledTask -TaskName $name -Confirm:$false -ErrorAction SilentlyContinue
        Register-ScheduledTask -TaskName $name -Action $action -Principal $principal -Settings $settings | Out-Null
        Start-ScheduledTask -TaskName $name
        $deadline = (Get-Date).AddSeconds($timeout)
        while ((Get-Date) -lt $deadline -and -not (Test-Path $exitFile)) { Start-Sleep -Seconds 1 }
        $finished = Test-Path $exitFile
        Stop-ScheduledTask -TaskName $name -ErrorAction SilentlyContinue
        Unregister-ScheduledTask -TaskName $name -Confirm:$false -ErrorAction SilentlyContinue
        $text = if (Test-Path $log) { Get-Content $log -Raw } else { '' }
        $code = 1
        if ($finished) { $parsed = 0; if ([int]::TryParse((Get-Content $exitFile -Raw).Trim(), [ref]$parsed)) { $code = $parsed } }
        return [pscustomobject]@{ Finished = $finished; ExitCode = $code; Output = $text }
    }
}
