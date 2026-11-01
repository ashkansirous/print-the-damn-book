### Slice 20 — Dino open and store hardening (review leftovers)

*Merged as PR #45 (07797d5), 2026-11-01; the desktop run is below, three checks are not yet verified.* The user said build it (2026-11-01). Found by the round-1 feedback loop on 2026-11-01
([note](../notes/2026-11-01-1141-feedback-loop-w1.md)). These are low-severity findings that
review passes on Slices 18 and 19 left unapplied. Their findings files live outside the repo
(`D:/print-the-damn-book-worktrees/reviews/`), so each one is written out here.

**Touches:** `src/PrintTheDamnBook.App/Services/DinoBookCache.cs`, `src/PrintTheDamnBook.App/Services/DinoStoreClient.cs`, `src/PrintTheDamnBook.Core/Dino/DinoStoreTransfer.cs`, `src/PrintTheDamnBook.Core/Dino/DinoStoreCatalog.cs`, `src/PrintTheDamnBook.App/ViewModels/MainViewModel.Dino.cs`, `src/PrintTheDamnBook.App/ViewModels/DinoStoreViewModel.cs`, `src/PrintTheDamnBook.App/ViewModels/MainViewModel.cs`, `src/PrintTheDamnBook.App/Views/OpenStepView.xaml.cs`, `src/PrintTheDamnBook.App/Views/DinoStoreDialog.xaml`, `tests/PrintTheDamnBook.Core.Tests/**`, `tests/PrintTheDamnBook.App.Tests/**` (new), `src/PrintTheDamnBook.App/PrintTheDamnBook.App.csproj` (InternalsVisibleTo only), `PrintTheDamnBook.slnx`

**Claim notes (2026-11-01, premise check on origin/main 234e3dd).** There is no App test project;
the view-model tests go in a new `tests/PrintTheDamnBook.App.Tests` (net10.0-windows, xUnit v3 like
the Core tests), added to the slnx. `MetaText` (the "0 pages" text) is at `DinoStoreCatalog.cs:115`,
not :153. The `File.Move` in `DinoStoreTransfer.cs:246` already uses `overwrite: true` with a unique
`.part` name, so only the reuse-unchecked half of that item remains. The only XAML change is the
failed-state block (`DinoStoreDialog.xaml:150-165`); Slice 21 adds AutomationIds in the same file.

**Why.** Two of these delete a valid file the user downloaded, and one can make a .dino fail to
open when another window holds the cache. Each costs at most a re-download or a confusing alert,
so none blocked a merge, but nothing else tracks them.

## Items

Line numbers come from the review files, written on the slice branches before they merged. Only
the `DinoBookCache.cs` and `MainViewModel*.cs` references were re-checked against main (51109e1).
Find the others by the member they name (`DeleteOtherVersions` / `OtherVersions`,
`DownloadBookAsync`, the cover loader) before building.

- [x] **`PlaceAlternate` copies `book.json` unguarded** (`Services/DinoBookCache.cs:262-268`; Slice 19
  fixer report, wave-1 session entry 697). When `TryReplaceDamaged` fails because another window
  holds the damaged folder, `PlaceAlternate` calls `CopySavedWords` again without a try. If the copy
  fails again (`book.json` locked), the open throws instead of falling back. Fix: make the copy
  best-effort there, so the alternate opens without the saved words and the status says so.
  Verify: a unit or integration test that locks `book.json` and opens through the alternate path.
- [x] **A stale hand-over alert can come back** (Slice 19 `check5` #1, low, uncertain; lines as on
  main 51109e1). A cancelled load stashes the alert (`ViewModels/MainViewModel.Dino.cs:313`,
  `_stashedHandoverProblem = HandoverProblemFile ?? _stashedHandoverProblem`), and a later load
  restores it (`:217`, `HandoverProblemFile ??= _stashedHandoverProblem`). `StartOver`
  (`MainViewModel.cs:426`) doesn't clear the stash, so a later load could show an earlier "couldn't
  open what Dino Reader sent" alert. Fix: clear the stash in `StartOver`. Verify: a view-model test.
- [x] **The version sweep deletes newer versions too** (`Services/DinoStoreClient.cs:270`, Core
  `DinoStoreTransfer.cs:140`; Slice 18 `check4` #4, low). Opening v2 while v3 is on disk deletes v3.
  Fix: delete only older versions. Verify: a Core test with v2 and v3 on disk.
- [x] **A valid download is deleted on any `DinoBookException`** (`ViewModels/MainViewModel.Dino.cs:191`;
  Slice 18 `check4` #5, low). Any `DinoBookException`, including `NoPrintableLanguageText`, counts as
  Rejected, so `Settle` deletes a file that is valid but has no printable language. Fix: delete only
  on a malformed-file reason. Verify: a test for each reason.
- [x] **Cancel can read as a network failure** (`ViewModels/DinoStoreViewModel.cs:253`; Slice 18
  `check2` #5, low-medium). A Cancel racing an abort that comes up as `HttpRequestException` or
  `IOException` shows "Could not reach Dino's store" instead of "Download cancelled". Fix: check
  `cancel.IsCancellationRequested` first. Verify: a view-model test with a cancelled token and an IO
  error.
- [x] **An existing download is reused unchecked** (`Services/DinoStoreClient.cs:121`; Slice 18
  `check4` #3, low). A file already in the store folder is opened without an integrity check, and
  two windows downloading the same book can collide on `File.Move`. Fix: check the size or hash
  before reuse, and treat a lost move race as success when the target exists. Verify: a test with a
  truncated existing file.
- [x] Minor (Slice 18 `check4` #1, #2): the saved `IsEnabled` binding in
  `Views/OpenStepView.xaml.cs:54` is restored without a null check (unreachable today; fragile if the
  XAML changes); an oversized catalog or cover reports "That book is too large."
  (`Core/Dino/DinoStoreTransfer.cs:50`).
- [x] **"Nothing was kept" can be untrue** (Slice 18 file: Design note vs the check-2 Cancel fix).
  The store dialog says "Download cancelled. Nothing was kept." But a download that completes at
  the moment of Cancel is kept and reused next time. Fix: say only "Download cancelled.", or delete
  the file when the view model knows it was fresh. Verify: a view-model test of the race.
- [x] **The failed state always blames the internet** (Slice 18 `check` #2, low, skipped with no
  reason recorded; `Views/DinoStoreDialog.xaml`). A full disk or a too-large book also says to
  check the connection, and there is no way back to the list. Fix: show the reason the view model
  already has, plus a Back to list button.
- [x] Minor (Slice 18 `check` #9, #10, skipped with no reason recorded): several one-line methods
  in `Services/DinoStoreClient.cs` break the repo's 3-30 line method rule; `ShowCards` in
  `DinoStoreViewModel.cs` finds cards with a linear lookup per entry (O(n²), negligible at 500).
- [x] Minor (Slice 18 `check4` #6, #7): `pageCount` 0 shows "0 pages" (`DinoStoreCatalog.cs:153`); a
  cover that fails once stays a placeholder until Try again (`DinoStoreViewModel.cs:337`).

## User's answer

2026-11-01: build it.

## Built

*Changed while building:* a new `tests/PrintTheDamnBook.App.Tests` project (net10.0-windows, xUnit v3, in the slnx, run by CI on windows-latest); `DinoNetworkException` wraps response-stream `IOException`s and `IsAboutConnection` is a whitelist; the dialog says only "Download cancelled."; the failed state shows the reason, a heading per kind, and Back to list after a download failure; the reuse check parses the file off the UI thread and re-downloads an unreadable or damaged one; `MoveIntoPlaceAsync` counts a lost race as success only with identical bytes; the version sweep deletes only older books and covers; the saved-words copy is best effort with a notice; `StartOver` clears the hand-over stash; `ShowDinoFailure` keys on exception type.

*From review* (files in `D:/print-the-damn-book-worktrees/reviews/`, outside the repo):

- `slice-20-high.md`: applied 1-5 and 7-9. Skipped 6 (cover retry has no delay and can re-fetch a corrupt cover): the retry runs at most once.
- `slice-20-medium.md`: applied 1-4 and 8. Skipped 5 (newer files stay after a store rollback): versions only increase. Skipped 6 (cover retry on non-transient failures): bounded retry. Skipped 7 (any error relabelled "cancelled" when the token is cancelled): the user pressed Cancel, so "cancelled" is right. Skipped 9 (`IsStoreUrl` inlined): readability only.
- `slice-20-check-medium.md`: applied 2, 4, 5, 6, 9, 10. Skipped 1 (the reuse check and the open each parse the file): costs only time. Skipped 3 (newer stale file after a catalog rollback): catalog versions only increase. Skipped 7 (allow-list check inlined) and 8 (`SameBytes` re-reads on the Cancel path): readability and cost only, no wrong behaviour.
- `slice-20-check2-medium.md`: applied 1, 3, 4. Skipped 2, 5, 7 and 8 as low, 6 (the `.alt` folder and its sweep are Slice 19 behaviour already on main, so changing them is the user's call; listed under Waiting on the user below), and 9 as the same as medium finding 7.
- `slice-20-check3-medium.md`: applied 1 (any reader failure but cancel means "not usable") and 2 (a locked or different file in the way says "in use by another window", new `DinoFileInUseException`). 7 needed no change: `TryCopySavedWords` only copies `book.json`, it never parses it. Skipped 3-6 and 8 as low (8: an unprintable-language book stays in the store folder by design of item 4).
- `slice-20-check4-medium.md` (final pass on the merged code): no medium or high finding; ten low notes left as they are.

*Desktop run 2026-11-01* on the host (live store, Release exe): the list showed 16 cards with no "0 pages"; a download opened; an existing download was reused without re-download; Cancel showed "Download cancelled."; a truncated file was re-downloaded. Evidence: `D:/print-the-damn-book-worktrees/reviews/slice-20-desktop-run/`.

*Known behaviours:* an existing download that another window holds open is fetched again into a `.part`; a cancelled list load closes silently as before.

*Waiting on the user:* words typed into a book opened in an `.alt-<guid>` folder (another window held the cache) are saved only there and go when the lone-instance sweep runs. This is Slice 19 behaviour on main, kept. Decide whether to warn or merge them back.

#### For CLAUDE.md (lifted 2026-11-01)

- App view-model tests live in `tests/PrintTheDamnBook.App.Tests` (`InternalsVisibleTo`). (Slice 20: docs/plan/slices/20-dino-open-hardening.md)
- Store failure kinds are typed (`DinoDiskException`, `DinoNetworkException`); never classify a failure from a bare `IOException` or message text. (Slice 20: docs/plan/slices/20-dino-open-hardening.md)

#### Not yet verified

- The failed state with Back to list on a real download failure (cannot be forced without changing live state).
- The hand-over alert for an `IOException` hand-over (unit-tested only).
- The "in use by another window" message when two windows download the same book (unit-tested only). Reuse, Cancel and the truncated-file re-download were re-run on the final build on 2026-11-01 and pass (`reviews/slice-20-desktop-run/rerun.md`, outside the repo).
