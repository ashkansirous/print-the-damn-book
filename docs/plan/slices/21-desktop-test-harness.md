### Slice 21 — Scripted desktop checks (test harness)

*Built 2026-11-01, verified in the VM on 2026-11-01 (store check skipped, VM offline); merged as PR #44 (6221069).* It was parked earlier that night because Claude Code's auto mode denied the first VM run (category "Modify Shared Resources"). The user then added an `autoMode` environment entry for the shared VM (2026-11-01) and the run was finished from the interactive session. The user said build it, with a Hyper-V VM (2026-11-01). Found by the round-1 feedback loop on 2026-11-01
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

- [x] A `tools/desktop-test/` folder of host UI Automation scripts, each one a single check that
  exits 0 or 1 with a short report: open a folder book by command line; open a .dino by command
  line (possible since Slice 19); the direction switch and the "As printed" sheet order; Print to
  PDF, then compare the PDF's page order with the Core formula; the store dialog list, download
  and Cancel (against the live store, read-only).
- [x] Move `handover-check.ps1` into it and run it end to end (one of the Slice 19 unverified lines).
- [x] AutomationIds on the controls the scripts drive, so they don't depend on text or position.
- [x] A README and a folder `CLAUDE.md` that says device-test agents run these scripts first and
  add a missing check as a new script, never as a throw-away.
- [x] A headless Hyper-V VM harness in `tools/desktop-vm/` (user's choice, 2026-11-01), modelled on
  ReadTheStupidText's `D:\ReadTheStupidText\tools\desktop-vm` (`New-TestVm.ps1`, `Reset-TestVm.ps1`,
  `Build-TestPackage.ps1`, `Invoke-DesktopTest.ps1`, `checks/`): build a package, copy it into the VM,
  run the `tools/desktop-test` scripts there, collect the report. Superseded by the user's answer
  above: reuse `rtst-desktop-test` under its lock and `clean` checkpoint, no own VM.

Verify: each script passes in the VM against a Release build, and the unverified lines it covers
are removed from `docs/plan/unverified.md`.

## For CLAUDE.md (lifted 2026-11-01)

- Desktop checks are scripts in `tools/desktop-test/` (exit 0/1, controls found by `AutomationId`); device-test agents run them through `tools/desktop-vm/Invoke-DesktopTest.ps1`, a thin runner over the shared `rtst-desktop-test` VM (RTST's lock, restore `clean` before and after, never change VMs or checkpoints, RTST's password), and add a missing check as a new script, never a throw-away. (Slice 21: docs/plan/slices/21-desktop-test-harness.md)

## Built (premise findings)

- The app has no folder command line (only a .dino path), so `open-folder` drives the native folder picker (ids 1152 and 1).
- No sample .dino is in the repo; the Dino checks take `-Dino` and are skipped, loudly, without it.

## Changed while building

- No own VM: `tools/desktop-vm/` is a thin runner over the shared `rtst-desktop-test` VM (lock `Global\rtst-desktop-test-vm`, `clean` restored before and after, exe copied in, no checkpoint changes).
- `handover-check.ps1` moved to `tools/desktop-test/` with absolute-path handling.
- `store-dialog` runs only on the host, behind `-HostStore -AcknowledgeHostDesktop`; the VM has no network, so a VM run prints `SKIP` for it.
- The order checks use a folder of 8 pages: a .dino adds a white back cover (8 pages become 9).

## Verified

2026-11-01, in the VM: `open-dino`, `open-folder`, `direction-and-sheet-order`, `print-to-pdf`, `handover-check` PASS; `store-dialog` SKIP (VM offline).

Lessons from the VM runs:
- Win32 file dialogs in the VM expose no UIA Value pattern. Set the path with `WM_SETTEXT` on the Edit (ids 1152, 1148, 1001) and press OK with `BM_CLICK` on `IDOK`.
- Page tiles expose only their label text ("page-3") to UIA; read them and sort by position.
- Tooltips are not prompts.

## From review

Review files (outside the repo, in `D:/print-the-damn-book-worktrees/reviews/`): `slice-21-high.md` (10 findings), `slice-21-medium.md` (10), `slice-21-check-medium.md` (7), `slice-21-check-final.md` (8), `slice-21-check-final-2.md` (6, a re-check of the fixes to findings 1-3 and 5-8 of `check-final`). All five were read to the last finding. The fixer's report was not given to this record, so which finding was applied or skipped is not recorded here; the brief says the passes ran. Notable findings that the later passes re-checked: the lost backslash in `Build-TestPackage.ps1` `TrimEnd` (check-medium 1), the back pages rotated 180 degrees by default (high 1), the failed final restore only warning (medium 2), and `-HostStore` touching the real desktop (check-medium 3, check-final 7). `check-final` findings 4 (`handover-check` duplicates `open-dino`) and `check-final-2` findings 1-6 are not stated as applied.

## Rules to lift

Both go to the folder `CLAUDE.md` files (already present in `tools/desktop-test/` and `tools/desktop-vm/`) and the root `CLAUDE.md` line above:
- Device-test agents run `tools/desktop-vm/Invoke-DesktopTest.ps1` first and add a missing check as a new script in `tools/desktop-test/`.
- The VM is the shared `rtst-desktop-test`, never a new one.

#### For CLAUDE.md (lifted 2026-11-01)

- Device-test agents run `tools/desktop-vm/Invoke-DesktopTest.ps1` first; a missing check is a new script in `tools/desktop-test/`, never a throw-away. The VM is the shared `rtst-desktop-test`, never a new one. (Slice 21: docs/plan/slices/21-desktop-test-harness.md)

#### Not yet verified

- `store-dialog` (list, download, Cancel) has not run: the VM has no network; it needs the host run with `-HostStore -AcknowledgeHostDesktop`.
- `open-dino` and `handover-check` ran with the `-Dino` file given at the run; no sample .dino is in the repo.

## User's answer

2026-11-01: build it; the checks run in a Hyper-V VM, not on the host desktop.
