### Slice 22 — Dino follow-ups from Slice 20's reviews and tests

*parked: feedback loop, waits for the user.* Found by the feedback loop of night 2026-11-01-2024, round 1
([note](../notes/2026-11-01-2024-feedback-loop-w1.md)). Nothing here is broken for users today. The
user decides whether to build it, and the failed-state design (item 4) needs their Claude Design pass first.

**Touches:** `src/PrintTheDamnBook.App/Services/DinoBookCache.cs`, `src/PrintTheDamnBook.App/Services/DinoStoreClient.cs`, `src/PrintTheDamnBook.App/ViewModels/DinoStoreViewModel.cs`, `src/PrintTheDamnBook.App/ViewModels/MainViewModel.Dino.cs`, `src/PrintTheDamnBook.App/Views/DinoStoreDialog.xaml`, `src/PrintTheDamnBook.Core/Dino/**`, `tests/PrintTheDamnBook.App.Tests/**`, `tests/PrintTheDamnBook.Core.Tests/**`

## Items

- [ ] **Tests use the real cache.** `tests/PrintTheDamnBook.App.Tests/DinoOpenHardeningTests.cs:102`
  (`AValidBookWithNoPrintableLanguageIsKeptAndShowsThePlainFailure`) runs `MainViewModel.LoadDinoAsync`,
  which opens and discards through the static `DinoBookCache` whose `Root` is under `%LOCALAPPDATA%`
  (`DinoBookCache.cs:54`). Nothing in the test project redirects it, so `dotnet test` likely writes to and
  deletes from the same cache folder a running app uses (likely, from the code and the fixer's report at W 866;
  not reproduced). Fix: an injectable cache root, as `DinoStoreClient(_dir, ...)` already takes, with the
  tests pointed at a temp folder. Verify: a test asserts nothing is created under the real root.
- [ ] **Classification by message text.** `DinoStoreViewModel.cs:410` (`IsAboutConnection`) matches
  `DinoBookException.Message` against `DinoStreamCopier.TimedOut`/`CutShort`, which breaks the repo rule
  "never classify a failure from message text". Check pass 2 flagged it as medium, and passes 3 and 4 as low;
  the wave's fix brief said to keep `IsAboutConnection` as it is, so it was never applied. Fix: a typed reason on `DinoBookException` (or `DinoNetworkException` for these two cases).
  Verify: a unit test per kind, with no string comparison left (`git grep "Message is"` empty).
- [ ] **Low notes from Slice 20's last check pass** (`reviews/slice-20-check4-medium.md`, copied here
  because the review files are deleted after this loop's note merges):
  1. `DinoStoreClient.cs:218`: the `IsUsableDownload` catch-all deletes a good file on `OutOfMemoryException`.
  2. `DinoStoreClient.cs:203`: a truncated file (`EndOfStreamException`) is treated as locked; the re-download overwrites it anyway.
  3. `DinoStoreTransfer.cs:156`: only older versions are cleaned, so a version rollback leaves orphans.
  4. `DinoStoreViewModel.cs:515`: the cover retry also fires for a cover that can't be decoded.
  5. `DinoStoreDialog.xaml:154`: `AutomationProperties.Name` "Failure heading" overrides the heading text for screen readers.
  6. `DinoStoreClient.cs:268`: the `onList` string is built on every call.
  7. `DinoStoreClient.cs:195`: a cached download is fully parsed on every reuse.
  8. Same as the classification item above.
  9. `DinoBookCache.cs:280`: saved words are not copied when the held folder is locked (see the Slice 20 decision in *Outstanding work*).
  10. The WPF App test project is part of the solution, so CI (`ci.yml`, windows-latest) builds and runs it.
- [ ] **Failed-state screens are not designed.** Slice 20 added a heading per failure kind, hints, a
  reason line and a Back to list button, with copy written by the wave ("written here, not from
  docs/design", night report). `docs/design` has only "I couldn't reach Dino's store" with Try again
  and Close. The user decides: keep the wave's copy, or send the failed variants to Claude Design and
  bring them in through `import-claude-design`.
- [ ] **Page tile's accessible name, possibly lost.** `Views/CheckStepView.xaml:103` binds
  `AutomationProperties.Name` to `AutomationName` ("Label, page N of M"). But Slice 21's VM run
  on 2026-11-01 read a tile named only "page-3", and the session concluded "no app bug" without
  finding why. The test script may read the image's name instead, or the binding may not reach the
  UI tree. From the final feedback loop of night 2026-11-01-2024 (audit pass 3, item 5). Verify:
  Inspect.exe or a UI Automation dump of the Check step shows "…, page 3 of N" on the tile.

Verify: fast checks green; one host desktop run of the store dialog's failed state against the design.
