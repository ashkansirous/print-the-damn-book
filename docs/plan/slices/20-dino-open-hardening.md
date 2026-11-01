### Slice 20 — Dino open and store hardening (review leftovers)

*Planned.* The user said build it (2026-11-01). Found by the round-1 feedback loop on 2026-11-01
([note](../notes/2026-11-01-1141-feedback-loop-w1.md)). These are low-severity findings that
review passes on Slices 18 and 19 left unapplied. Their findings files live outside the repo
(`D:/print-the-damn-book-worktrees/reviews/`), so each one is written out here.

**Touches:** `src/PrintTheDamnBook.App/Services/DinoBookCache.cs`, `src/PrintTheDamnBook.App/Services/DinoStoreClient.cs`, `src/PrintTheDamnBook.Core/Dino/DinoStoreTransfer.cs`, `src/PrintTheDamnBook.Core/Dino/DinoStoreCatalog.cs`, `src/PrintTheDamnBook.App/ViewModels/MainViewModel.Dino.cs`, `src/PrintTheDamnBook.App/ViewModels/DinoStoreViewModel.cs`, `src/PrintTheDamnBook.App/ViewModels/MainViewModel.cs`, `src/PrintTheDamnBook.App/Views/OpenStepView.xaml.cs`, `src/PrintTheDamnBook.App/Views/DinoStoreDialog.xaml`, `tests/PrintTheDamnBook.Core.Tests/**`

**Why.** Two of these delete a valid file the user downloaded, and one can make a .dino fail to
open when another window holds the cache. Each costs at most a re-download or a confusing alert,
so none blocked a merge, but nothing else tracks them.

## Items

Line numbers come from the review files, written on the slice branches before they merged. Only
the `DinoBookCache.cs` and `MainViewModel*.cs` references were re-checked against main (51109e1).
Find the others by the member they name (`DeleteOtherVersions` / `OtherVersions`,
`DownloadBookAsync`, the cover loader) before building.

- [ ] **`PlaceAlternate` copies `book.json` unguarded** (`Services/DinoBookCache.cs:262-268`; Slice 19
  fixer report, wave-1 session entry 697). When `TryReplaceDamaged` fails because another window
  holds the damaged folder, `PlaceAlternate` calls `CopySavedWords` again without a try. If the copy
  fails again (`book.json` locked), the open throws instead of falling back. Fix: make the copy
  best-effort there, so the alternate opens without the saved words and the status says so.
  Verify: a unit or integration test that locks `book.json` and opens through the alternate path.
- [ ] **A stale hand-over alert can come back** (Slice 19 `check5` #1, low, uncertain; lines as on
  main 51109e1). A cancelled load stashes the alert (`ViewModels/MainViewModel.Dino.cs:313`,
  `_stashedHandoverProblem = HandoverProblemFile ?? _stashedHandoverProblem`), and a later load
  restores it (`:217`, `HandoverProblemFile ??= _stashedHandoverProblem`). `StartOver`
  (`MainViewModel.cs:426`) doesn't clear the stash, so a later load could show an earlier "couldn't
  open what Dino Reader sent" alert. Fix: clear the stash in `StartOver`. Verify: a view-model test.
- [ ] **The version sweep deletes newer versions too** (`Services/DinoStoreClient.cs:270`, Core
  `DinoStoreTransfer.cs:140`; Slice 18 `check4` #4, low). Opening v2 while v3 is on disk deletes v3.
  Fix: delete only older versions. Verify: a Core test with v2 and v3 on disk.
- [ ] **A valid download is deleted on any `DinoBookException`** (`ViewModels/MainViewModel.Dino.cs:191`;
  Slice 18 `check4` #5, low). Any `DinoBookException`, including `NoPrintableLanguageText`, counts as
  Rejected, so `Settle` deletes a file that is valid but has no printable language. Fix: delete only
  on a malformed-file reason. Verify: a test for each reason.
- [ ] **Cancel can read as a network failure** (`ViewModels/DinoStoreViewModel.cs:253`; Slice 18
  `check2` #5, low-medium). A Cancel racing an abort that comes up as `HttpRequestException` or
  `IOException` shows "Could not reach Dino's store" instead of "Download cancelled". Fix: check
  `cancel.IsCancellationRequested` first. Verify: a view-model test with a cancelled token and an IO
  error.
- [ ] **An existing download is reused unchecked** (`Services/DinoStoreClient.cs:121`; Slice 18
  `check4` #3, low). A file already in the store folder is opened without an integrity check, and
  two windows downloading the same book can collide on `File.Move`. Fix: check the size or hash
  before reuse, and treat a lost move race as success when the target exists. Verify: a test with a
  truncated existing file.
- [ ] Minor (Slice 18 `check4` #1, #2): the saved `IsEnabled` binding in
  `Views/OpenStepView.xaml.cs:54` is restored without a null check (unreachable today; fragile if the
  XAML changes); an oversized catalog or cover reports "That book is too large."
  (`Core/Dino/DinoStoreTransfer.cs:50`).
- [ ] **"Nothing was kept" can be untrue** (Slice 18 file: Design note vs the check-2 Cancel fix).
  The store dialog says "Download cancelled. Nothing was kept." But a download that completes at
  the moment of Cancel is kept and reused next time. Fix: say only "Download cancelled.", or delete
  the file when the view model knows it was fresh. Verify: a view-model test of the race.
- [ ] **The failed state always blames the internet** (Slice 18 `check` #2, low, skipped with no
  reason recorded; `Views/DinoStoreDialog.xaml`). A full disk or a too-large book also says to
  check the connection, and there is no way back to the list. Fix: show the reason the view model
  already has, plus a Back to list button.
- [ ] Minor (Slice 18 `check` #9, #10, skipped with no reason recorded): several one-line methods
  in `Services/DinoStoreClient.cs` break the repo's 3-30 line method rule; `ShowCards` in
  `DinoStoreViewModel.cs` finds cards with a linear lookup per entry (O(n²), negligible at 500).
- [ ] Minor (Slice 18 `check4` #6, #7): `pageCount` 0 shows "0 pages" (`DinoStoreCatalog.cs:153`); a
  cover that fails once stays a placeholder until Try again (`DinoStoreViewModel.cs:337`).

## User's answer

2026-11-01: build it.
