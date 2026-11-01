<#
.SYNOPSIS
  Puts the shared rtst-desktop-test VM back at its 'clean' checkpoint (Slice 21). -Start also boots it to the saved desktop.
  -WhatIf names the VM and the checkpoint and changes nothing.
#>
[CmdletBinding(SupportsShouldProcess)]
param([switch] $Start)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/_common.ps1"
if (-not $PSCmdlet.ShouldProcess("$($script:VmName) -> checkpoint '$($script:CheckpointName)'", 'restore')) { return }
Enter-VmLock
try { Restore-CleanCheckpoint -Start:$Start } finally { Exit-VmLock }
