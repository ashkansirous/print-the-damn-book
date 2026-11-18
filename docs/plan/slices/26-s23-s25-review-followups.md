### Slice 26 — Follow-ups from Slices 23 and 25 reviews

*parked: feedback loop, waits for the user.* Found by the feedback loop of night 2026-11-18-1302
([note](../notes/2026-11-18-1302-feedback-loop.md)). These are the last review pass's findings that the wave
left unfixed, plus test and harness gaps. The wave called none of them crash or data loss, but three come
close and go first: Slice 23 #5 (a user's Own page choice is lost, against the rule Slice 23 lifted), Slice 25
#2 (a failed apply may already have queued a save of mixed text) and the possible `book.json` write on open. The user decides whether to build it. Quotes are copied here so the slice does not depend on the untracked
`reviews/` folder (the loop deleted none of it; a later prune may).

**Touches:** `src/PrintTheDamnBook.App/ViewModels/MainViewModel.cs`, `src/PrintTheDamnBook.App/ViewModels/MainViewModel.Dino.cs`, `tools/desktop-test/*.ps1`, `tools/desktop-test/run-all.ps1`, `src/PrintTheDamnBook.App/ViewModels/MainViewModel.Story.cs`, `src/PrintTheDamnBook.App/Services/BookTextStore.cs`, `src/PrintTheDamnBook.App/Services/DinoBookCache.cs`, `src/PrintTheDamnBook.App/Services/ThumbnailLoader.cs`, `src/PrintTheDamnBook.Core/Dino/**`, `src/PrintTheDamnBook.Core/Text/**`, `src/PrintTheDamnBook.App/Services/DinoStoreClient.cs`, `src/PrintTheDamnBook.App/ViewModels/DinoStoreViewModel.cs`, `tools/desktop-test/store-dialog.ps1`, `tools/desktop-vm/Invoke-DesktopTest.ps1`, `tests/PrintTheDamnBook.App.Tests/**`, `tests/PrintTheDamnBook.Core.Tests/**`

## Items

Slice 25 (`reviews/pr-58-check2.md`):

- [ ] #1 (`MainViewModel.Dino.cs:568`, `ApplyLanguageAsync`): "keep _appliedLanguage updated only on successful
  ApplyRun and revert to that." The select is left on a language the pages are not in after a switch started
  during the open-time reconcile and then cancelled. Verify: an App test cancels a switch during reconcile and
  asserts the select matches the pages' language.
- [ ] #2 (`MainViewModel.Dino.cs:593`): "separate the off-thread translate failure from the apply step, or undo
  applied pages." Verify: an App test throws inside the in-memory apply and asserts no page is half-switched.

- [ ] Contradictory skip reasons in the Slice 25 file: skipped #3 of `reviews/pr-58-medium.md` says the
  open-time reconcile "calls no ScheduleBookTextSave", skipped #2 of `reviews/pr-58-check.md` says it does save
  (`ApplyForms` calls `SetStory`, which schedules a `book.json` save). If the second is right, opening a book
  rewrites the user's `book.json`. Neither was checked in code. Verify: an App test opens a book whose saved
  language differs and asserts whether `book.json` is written; correct the wrong skip line.
- [ ] The `\n\n` to `\n` collapse in the user's saved Seagull Island text has no known cause (Slice 25,
  *Changed while building*: "likely the Story text TextBox round trip"). Slice 23's cleaning hides it, but user
  text may still be changed on a round trip. Verify: type two blank-line-separated paragraphs, save, reopen,
  compare `book.json`.

- [ ] The doc comment on `ReconcileLanguageOnOpen` (`MainViewModel.Dino.cs`, about lines 765-768) says "the
  disk text is not rewritten", but `ApplyForms` calls `SetStory` (`MainViewModel.Story.cs:539-564`), which calls
  `ScheduleBookTextSave`. Fix the comment or the code with the item above.

Slice 20 code, found while building Slice 25:

- [ ] Flaky test `DinoOpenHardeningTests.StartOverClearsAStashedHandoverAlert`: failed in two of the Slice 25
  builder's runs and passed later, untouched. Verify: 20 repeated runs pass.
- [ ] Race with no UI sync context (Slice 23 builder report, wave entry 800): "the language-switch continuation
  refreshes on a pool thread while the test thread also refreshes". Not fixed; likely the same root as the flaky
  test above. Verify: an App test refreshes from two threads and keeps a consistent page list.
- [ ] Slice 25's language switch had no VM run (7.0 GB free, under the 8 GB threshold). Add a scripted
  `tools/desktop-test` check that switches a Dino book's language and checks every page changed.

Store dialog (independent of Slice 24's purchase work):

- [ ] The `store-dialog` check failed on 2026-11-18 ("neither a progress bar nor a result" in 60 s on a paid
  book, `reviews/s21-store-dialog.log`), blamed on "the app shows nothing on a 402". But
  `DinoStoreClient.SendAsync` (about lines 256-263) throws `DinoStoreException(Refused)` on any non-2xx, so a
  message should show; the check may not recognise the failed state. Reproduce on the VM what the dialog shows
  on a 402 first, then fix the message or the check. The whole log (311 bytes, host desktop run):
  "the store listed 16 stor(ies)" / "FAIL  store-dialog: Downloading 'Goldilocks and the Three Bears' showed
  neither a progress bar nor a result (waited 60 s)". Slice 24 items at its lines 48-49 assume the first cause.

Slice 23 (`reviews/pr-59-check2.md`; #5 loses a user's Own page choice, against the rule Slice 23 lifted):

- [ ] #1: "ApplyAutomaticPlacement can change Placement while a print runs (only the timer path is guarded)."
  Unconfirmed. Verify: a test starts a print and asserts placement is unchanged until it ends.
- [ ] #2: "SplitBackCover compares raw JSON strings Ordinal", so `Back.JPG` and `back.jpg` can print the
  artwork twice. Verify: a Core test with mixed-case names gives one back cover.
- [ ] #3: "when BandFits returns null a ChosenByUser=false page is neither moved nor cleared" after a one-off
  decode failure. Verify: a test with a failing then succeeding decode.
- [ ] #4: "LoadNotingSize starts _sampleTimer on first load on the UI thread", and a second full Refresh can
  drop the placement decision. Verify: a test runs two refreshes and keeps the decision.
- [ ] #5: "ApplyTypedText on a page with no story builds StoryText with ChosenByUser=false", so an Own page
  choice is lost when the text is cleared and retyped. Verify: clear, retype, still Own page.
- [ ] #6: "Verify returns false without the \"layout\\t2\" manifest line", so older cache folders are
  re-extracted and a second window can make the open fail. Verify: a cache folder from before Slice 23 opens
  without re-extracting while another window holds it.
- [ ] #7 (low): "SpellingsIn walks and LINQ-filters every property of an untrusted per-language object before
  the MaxSpellingsPerLanguage break". Verify: a Core test with a huge object stops at the cap.
- [ ] #8 (low): "WithBackCover runs on a load that could not be read (CanSave=false) and does not check
  Language is one the book has". Verify: a test with an unreadable book.json adds no back cover.

Test harness:

- [ ] `tools/desktop-vm/Invoke-DesktopTest.ps1` `$GuestChecks` (line 43) lacks `back-cover-and-fit`, so
  `-Only back-cover-and-fit` is refused while `tools/desktop-test/run-all.ps1` has it (wave 1 of night
  2026-11-18-1302). Fix: build the list from `tools/desktop-test/*.ps1` instead of a hand-kept array.
  Verify: `Invoke-DesktopTest.ps1 -Only back-cover-and-fit` runs that check. Until then
  `tools/desktop-test/CLAUDE.md` says a new check is added to both lists (feedback loop PR).
