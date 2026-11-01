# tools/desktop-vm: runner over the shared test VM (Slice 21)

Runs the `tools/desktop-test` checks inside the Hyper-V VM `rtst-desktop-test` that ReadTheStupidText and Dino Reader already
share (user's answer, 2026-11-01: no VM of our own). This folder only borrows it: it takes RTST's host lock, restores the `clean`
checkpoint before and after every run (the VM is left Saved), and never creates, changes or deletes a VM or a checkpoint.
Needs Hyper-V on and your user in *Hyper-V Administrators*; no elevation. The VM is built and refreshed from RTST's
`tools/desktop-vm/New-TestVm.ps1`, by whoever owns RTST, not from here.

```powershell
tools/desktop-vm/Invoke-DesktopTest.ps1 -Dino <a .dino file>     # builds the exe, runs every check, exit 0 = all passed, none skipped
tools/desktop-vm/Invoke-DesktopTest.ps1 -Only print-to-pdf
tools/desktop-vm/Invoke-DesktopTest.ps1 -WhatIf                  # prints the plan, touches no VM
tools/desktop-vm/Reset-TestVm.ps1 [-Start] [-WhatIf]             # just restore 'clean'
```

## What a run does

Build the self-contained exe (`Build-TestPackage.ps1`, the same `dotnet publish` command as `release.yml`) -> take the lock
(mutex `Global\rtst-desktop-test-vm`, shared with RTST and Dino runs) -> restore `clean`, start -> wait for the guest desktop
(a cold boot: PowerShell Direct answers before the autologon desktop exists) -> copy the exe and the checks into `C:/desktop-test`
-> run `run-all.ps1` in the desktop session as a one-shot scheduled task (UI Automation needs it) -> copy `results` out to
`-ResultsDir` (default `%TEMP%/ptdb-desktop-test/results`) -> restore `clean` -> release the lock.

A wait with no progress for `-StallMin` minutes (default 10) saves the VM screen under `%TEMP%/ptdb-desktop-test/screens` and
throws `stalled: <what>; screen: <png>`. A check script that throws still writes its exit code, so the run reports it at once.

## Password

`DESKTOP_VM_PASSWORD` in `D:/secrets/readthestupidtext/secrets.env` (RTST's). This folder only reads it.

## Limits

- The VM has no network adapter: the `store-dialog` check never runs there. `-HostStore` runs it on the host desktop (the user allowed this one check on the host); without it the run prints a `SKIP` line.
- No sample .dino is in the repo; without `-Dino` the two Dino launch checks are skipped and said so.
- Exit codes: 0 all passed and none skipped; 1 a check or the harness failed; 3 nothing failed but a check was skipped (`-AllowSkip` makes it 0); 2 the final restore of `clean` failed, so the shared VM may be dirty or running (run `Reset-TestVm.ps1`).
- Nothing here has run yet; see `docs/plan/unverified.md`.
