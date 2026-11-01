# tools/desktop-test: scripted desktop checks (Slice 21)

UI Automation scripts that drive a built `PrintTheDamnBook.exe`. Each is one check, prints one `PASS`/`FAIL name: message`
line (plus detail lines) and exits 0 or 1. They find controls by `AutomationProperties.AutomationId`, never by text or position.
They are meant to run in the headless test VM (`tools/desktop-vm`), not on the developer's own desktop.

| Script | What it checks |
| --- | --- |
| `open-dino.ps1 -Exe -Dino` | A .dino on the command line lands on the Check step; a missing file and a .txt file show the alert (Slice 19). |
| `open-folder.ps1 -Exe` | A folder of 8 colour pages opens through the native folder picker (the app has no folder command line) and lists them in file-name order. |
| `direction-and-sheet-order.ps1 -Exe` | Left to right / Right to left and the "As printed" face order, against the Core formula (Slice 17). |
| `print-to-pdf.ps1 -Exe` | Prints to "Microsoft Print to PDF" (Manual duplex: fronts file, backs file), renders each PDF page with `Windows.Data.Pdf` and compares the colour in each half with the Core formula. Needs Windows PowerShell 5.1. |
| `store-dialog.ps1 -Exe` | The store dialog against the live store (plain GETs): list loads, a download starts, Cancel returns to the list. Needs the internet. |
| `handover-check.ps1 -Exe -Dino` | The Slice 19 launch cases plus the HKCU "Open with" keys (moved here from the Core tests folder). |
| `run-all.ps1 -Exe -Dino [-Only name] [-SkipStore] [-AllowSkip] [-WhatIf]` | Runs them all, one process each, and sums up. |

`_lib.ps1` holds the shared helpers (launch, find by AutomationId, invoke, sample book, the expected face order).

## AutomationIds the checks use

`OpenFolderButton`, `OpenDinoButton`, `GetStoryButton`, `HandoverAlert` (OpenStepView); `ViewReadingOrder`, `ViewAsPrinted`,
`BackButton`, `NextButton`, `StatusText` (MainWindow); `DirectionLeftToRight`, `DirectionRightToLeft` (CheckStepView);
`PrinterCombo` (PrintStepView); `StoreCloseButton`, `StoreCancelLoadingButton`, `StoreList`, `StoreCardButton`,
`StoreDownloadBar`, `StoreCancelDownloadButton` (DinoStoreDialog). A new control a check drives gets an id in the same change.

## Known limits

- Never run on the host desktop (user's choice, 2026-11-01); the VM runs them.
- No script here has been run yet (the VM is not built); the first in-VM run is listed in `docs/plan/unverified.md`.
- The native dialogs are found under the app window or on the desktop (whichever UIA lists), and the name box by id 1001, then 1152, then the first Edit; each run reports which matched, so the first VM run settles it. The store check is host-only (the VM has no network).
