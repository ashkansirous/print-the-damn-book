### Slice 21 — Scripted desktop checks (test harness)

*Parked (wave 2026-11-01): built and through both review passes plus a check pass, never run in the VM.* The first in-VM run (a `device-test` agent calling `tools/desktop-vm/Invoke-DesktopTest.ps1` against `rtst-desktop-test`) was denied by Claude Code's auto-mode permission check (category "Modify Shared Resources") on 2026-11-01 in the unattended night run, so nothing here has run yet. To go on: allow that run (an allow rule for the Agent/Bash call running `tools/desktop-vm/Invoke-DesktopTest.ps1`, or run it in an interactive session), then run the checks and fix what they find. The code is on draft PR #44. The user said build it, with a Hyper-V VM (2026-11-01). Found by the round-1 feedback loop on 2026-11-01
([note](../notes/2026-11-01-1141-feedback-loop-w1.md)).

**Touches:** `tools/desktop-test/**` (new), `tools/desktop-vm/**` (new), `tests/PrintTheDamnBook.Core.Tests/handover-check.ps1` (moved to `tools/desktop-test/`), `src/PrintTheDamnBook.App/**/*.xaml` (AutomationIds only), `.github/workflows/**` (only if a check runs in CI). The root `CLAUDE.md` line goes in this file's *For CLAUDE.md* block and is lifted by the index pass.

**Claim notes (2026-11-01, unattended night run; premise check on this PC).**
- No AutomationId exists in `src` yet; the checks need them in `Views/OpenStepView.xaml`,
  `Views/CheckStepView.xaml`, `Views/PrintStepView.xaml`, `Views/DinoStoreDialog.xaml`, `MainWindow.xaml`.
- RTST's harness (`D:/ReadTheStupidText/tools/desktop-vm`) builds its VM from the Windows 11
  Enterprise evaluation ISO (about 8 GB) with an answer file; it needs Hyper-V Administrators (the
  user has it), not elevation. Its only reusable disk is RTST's own `disk.vhdx` (25 GB, with RTST's
  app and password inside).
- D: has 31 GB free, too little for a second VM. The first build (before the user's answer below)
  wrote a `New-TestVm.ps1` for an own VM; the answer replaces that.
- The package is the self-contained exe from `release.yml`'s `dotnet publish` command, not an MSIX.
- The checks are not run on the host desktop (user's answer), except the live-store check: the VM
  has no network adapter, so that one runs on the host or is skipped.

**User's answer on the VM (2026-11-01, about 20:38, relayed by the run's supervising session):** do
not build a new VM; reuse the Hyper-V VM `rtst-desktop-test` that ReadTheStupidText and Dino Reader
share (`D:/ReadTheStupidText-worktrees/_desktop-vm/`). Rules for using it:
- Take the same host lock (mutex `Global\rtst-desktop-test-vm`, `Enter-VmLock` in RTST's
  `tools/desktop-vm/_common.ps1`), so a PTDB run never overlaps an RTST or Dino run.
- Restore the `clean` checkpoint before and after every run (RTST's `Reset-TestVm.ps1` pattern) and
  leave the VM Saved. Never take, delete or change checkpoints; never rebuild the VM.
- Password: `DESKTOP_VM_PASSWORD` in `D:/secrets/readthestupidtext/secrets.env`.
- Copy the Release build (self-contained exe) into the guest; no MSIX.
- So `tools/desktop-vm/` here is a thin runner over that VM (it may dot-source or copy RTST's
  `_common.ps1` helpers): build, restore `clean`, copy in, run the `tools/desktop-test` checks in
  the guest session, copy results out, restore `clean`. No `New-TestVm.ps1`, no own VM.

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
  run the `tools/desktop-test` scripts there, collect the report. Superseded by the user's answer
  above: reuse `rtst-desktop-test` under its lock and `clean` checkpoint, no own VM.

Verify: each script passes in the VM against a Release build, and the unverified lines it covers
are removed from `docs/plan/unverified.md`.

## For CLAUDE.md

- Desktop checks are scripts in `tools/desktop-test/` (exit 0/1, controls found by `AutomationId`); device-test agents run them through `tools/desktop-vm/Invoke-DesktopTest.ps1`, a thin runner over the shared `rtst-desktop-test` VM (RTST's lock, restore `clean` before and after, never change VMs or checkpoints, RTST's password), and add a missing check as a new script, never a throw-away. (Slice 21: docs/plan/slices/21-desktop-test-harness.md)

## Built (premise findings)

- The app has no folder command line (only a .dino path), so `open-folder` drives the native folder picker (ids 1152 and 1).
- No sample .dino is in the repo; the Dino checks take `-Dino` and are skipped, loudly, without it.

## User's answer

2026-11-01: build it; the checks run in a Hyper-V VM, not on the host desktop.
