### Slice 21 — Scripted desktop checks (test harness)

*In progress (wave 2026-11-01), draft PR.* The user said build it, with a Hyper-V VM (2026-11-01). Found by the round-1 feedback loop on 2026-11-01
([note](../notes/2026-11-01-1141-feedback-loop-w1.md)).

**Touches:** `tools/desktop-test/**` (new), `tools/desktop-vm/**` (new), `tests/PrintTheDamnBook.Core.Tests/handover-check.ps1`, `src/PrintTheDamnBook.App/**/*.xaml` (AutomationIds only), `.github/workflows/**` (only if a check runs in CI). The root `CLAUDE.md` line goes in this file's *For CLAUDE.md* block and is lifted by the index pass.

**Claim notes (2026-11-01, unattended night run; premise check on this PC).**
- No AutomationId exists in `src` yet; the checks need them in `Views/OpenStepView.xaml`,
  `Views/CheckStepView.xaml`, `Views/PrintStepView.xaml`, `Views/DinoStoreDialog.xaml`, `MainWindow.xaml`.
- RTST's harness (`D:/ReadTheStupidText/tools/desktop-vm`) builds its VM from the Windows 11
  Enterprise evaluation ISO (about 8 GB) with an answer file; it needs Hyper-V Administrators (the
  user has it), not elevation. Its only reusable disk is RTST's own `disk.vhdx` (25 GB, with RTST's
  app and password inside); copying or chaining it would reuse the RTST VM, which this slice forbids.
- D: has 31 GB free (C: is outside the allowed folders). A fresh VM needs the 8 GB ISO plus a VHDX
  that grows to about 20 GB during install: most of D:, with other sessions writing there. **Decision
  (taken unattended):** this slice builds the harness scripts (`New-TestVm.ps1` with a `-VmRoot`
  parameter so the VM can live on another drive, `Reset-TestVm.ps1`, `Build-TestPackage.ps1`,
  `Invoke-DesktopTest.ps1`) and the check scripts, but does not create the VM tonight. Creating it
  and the first in-VM run wait for the user to pick a drive with room (see *Waiting on the user*).
  The package is the self-contained exe from `release.yml`'s `dotnet publish` command, not an MSIX.
- The checks are not run on the host desktop (user's answer). They get a parse check and a
  `-WhatIf`/dry-run where possible; the in-VM run goes to *Not yet verified*.

## Waiting on the user

- Where the test VM lives: D: has 31 GB free, the VM needs about 28 GB at its peak. Run
  `tools/desktop-vm/New-TestVm.ps1 -VmRoot <folder on a drive with 40 GB free>` once, or free space on D:.

**Why.** The repo has no repeatable desktop checks. `tools/` holds only `store-assets`, plus one
committed script, `tests/PrintTheDamnBook.Core.Tests/handover-check.ps1`. On 2026-11-01, the three
desktop runs (Slices 17, 18 and 19) wrote throw-away UI Automation scripts in a temp folder and
deleted them afterwards; only `tools/store-assets/capture-app.ps1` was reused. The builders were
asked to write scripted checks, but only Slice 19 did, and its script had a parse error. The Slice
17 run could not open a Farsi .dino because the native Open dialog has no AutomationId 1148. The
`handover-check.ps1` fix was never re-run. About 4 of the 11 checks in
[unverified.md](../unverified.md) wait on scripts like these; the rest need Farsi store books, a
paper print or the user. The global rules expect a device-test
pipeline from an app's first UI slice; this repo never got one.

## Items

- [ ] A `tools/desktop-test/` folder of host UI Automation scripts, each one a single check that
  exits 0 or 1 with a short report: open a folder book by command line; open a .dino by command
  line (possible since Slice 19); the direction switch and the "As printed" sheet order; Print to
  PDF, then compare the PDF's page order with the Core formula; the store dialog list, download
  and Cancel (against the live store, read-only).
- [ ] Move `handover-check.ps1` into it and run it end to end (one of the Slice 19 unverified lines).
- [ ] AutomationIds on the controls the scripts drive, so they don't depend on text or position.
- [ ] A README and a folder `CLAUDE.md` that says device-test agents run these scripts first and
  add a missing check as a new script, never as a throw-away.
- [ ] A headless Hyper-V VM harness in `tools/desktop-vm/` (user's choice, 2026-11-01), modelled on
  ReadTheStupidText's `D:\ReadTheStupidText\tools\desktop-vm` (`New-TestVm.ps1`, `Reset-TestVm.ps1`,
  `Build-TestPackage.ps1`, `Invoke-DesktopTest.ps1`, `checks/`): build a package, copy it into the VM,
  run the `tools/desktop-test` scripts there, collect the report. Its own VM name (for example
  `ptdb-desktop-test`); never start, change or reuse the `rtst-desktop-test` VM. Cheapest path first:
  if RTST's base image or VHD can be copied, copy it rather than install Windows from scratch.
  Snapshot after setup and reset from the snapshot before each run.

Verify: each script passes in the VM against a Release build, and the unverified lines it covers
are removed from `docs/plan/unverified.md`.

## User's answer

2026-11-01: build it; the checks run in a Hyper-V VM, not on the host desktop.
