### Slice 25 — Dino: switching language translates every page, not only the cover

*Built 2026-11-18 (night run, wave 1), merged as PR #58 (46a8e2c). Not yet verified on the user's PC.*

**Touches:** `src/PrintTheDamnBook.App/ViewModels/MainViewModel.Dino.cs`, `src/PrintTheDamnBook.App/ViewModels/MainViewModel.Story.cs`, `src/PrintTheDamnBook.Core/Text/StoryLines.cs` (new), `src/PrintTheDamnBook.App/ViewModels/MainViewModel.cs` (clears the notice), `src/PrintTheDamnBook.App/Views/CheckStepView.xaml` (shows the notice), `tests/PrintTheDamnBook.App.Tests/**`, `tests/PrintTheDamnBook.Core.Tests/**`

**Design (wave coordinator, 2026-11-18, night run; the slice offered two options, this takes the second):**

- "Edited" is decided by comparing **cleaned** text: a page counts as user-edited only when
  `StoryLines.Clean(saved) != StoryLines.Clean(source-in-from-language)`. No new field in `book.json`, so old saved files
  (like the user's, which has no flag) work at once, and no file format change collides with Slice 23's `ChosenByUser`.
  Why not the flag: a flag cannot tell, for an already-saved book, whether a past edit happened; the cleaned compare can.
- `Core/Text/StoryLines.cs` is the function Slice 23 item A1 names (drop empty or whitespace-only lines, trim each line's
  ends, keep single line breaks, `\r\n` as `\n`); this slice creates it with its unit tests so Slice 23 reuses it rather
  than writing a second one. This slice does not change layout or printing (that is Slice 23 part A).
- Where the `\n\n` became `\n` is not known (ground-in-code 2026-11-18 found no normaliser in `src/`; likely the Story
  text TextBox round trip). The cleaned compare makes the fix independent of it; the builder records the cause if a test shows it.
- **Review round 1 (2026-11-18), supersedes the "from-language" compare above:** a page is user-edited only when its cleaned
  text matches the cleaned source in **none** of the book's languages (original and every translation). Every other page always
  gets the target language's text, translated from the original; a page already in the target language is not "kept". The same
  reconciliation runs on open for the saved language, so a book saved with `language: fa` over English pages opens in Farsi with
  no notice.
- A page that kept its own text after a switch is named in `KeptTextNotice`, shown on the Check step under the language select
  (not the Open step's `BookNotice`, which later refreshes rebuild), e.g. "Pages 3, 7 kept your own text." The numbers are the
  printed numbers Core `PageNumbering` gives, as the Check step shows them. It stays until the next switch or open.
- **Review round 3 (PR #58 check, `reviews/pr-58-check.md` #1, #3, #6), one design for overlapping runs:** the current language run is the one whose
  `CancellationTokenSource` is `_translateCancel`. Starting a run (a switch or the open-time reconcile) cancels the previous one; closing or
  opening a book cancels it too (`CancelTranslation`). Only the current run may apply its result, revert the select or set `Status`; a
  superseded run (cancelled by a newer one, or its book closed) does nothing at all. The user's Cancel button cancels the current run, which
  still reverts the select. An open-time reconcile that fails or is cancelled sets no "Could not change the language" text; pages stay as saved.

Why: the user, 2026-11-18, printing Love from Seagull Island (`Love from Seagull Island.hd.dino`, store id 9b77e4f2c83247fcbb16eef03d94ffd3): with فارسی and Right to
left picked, the cover band showed Farsi but every page's band and *Story text* stayed English. "why ... is it missing it and why ... the tests did not catch it".

What was found (2026-11-18):

- The package is fine: `story.json` has `Translations.fa` (96 sentences), and `DinoTranslator`'s own longest-match algorithm run on `Pages[i].Text` gives full Farsi.
- The saved text `%LOCALAPPDATA%\PrintTheDamnBook\dino\9b77e4f2c83247fcbb16eef03d94ffd3-804946f8fd7c3d00\book.json` (12:55) has `"language": "fa"`, the Farsi
  cover title, and English pages whose paragraphs are joined by one `\n`, while the book's source text uses `\n\n`.
- `ApplyTranslated` (`MainViewModel.Dino.cs`, about line 633) replaces a page only when `story.Text == result.FromText`, so a page whose saved text differs from the
  source by the collapsed blank line counts as user-edited and is skipped without a word. The cover title is one line, so it passes. Likely every book with
  multi-paragraph pages (all dialogue books) is hit once its text has been saved; confirm with the test below.
- No test drives `SelectedDinoLanguage` / `ApplyLanguageAsync` / `ApplyTranslated`; translation is tested only at the data level (`DinoBookReaderTests.cs`
  `In_TranslatesEachSentence...`), so the view-model glue had no test.

- [x] Decide "edited" by what the user did, not by string equality: keep a per-page flag set only when the user types in *Story text*, or compare after the same
      normalisation the save applies. A page the user never edited always follows the language.
- [x] A page the user did edit is not changed silently: show which pages kept their own text after a language switch.
- [x] App test: open a .dino with a two-paragraph page, save, reopen, switch to `fa`; every page's band and *Story text* is Farsi. Plus: an edited page keeps its text and is named.
- [x] Run it against the user's saved `book.json` case above (copy it into the test data, never edit the user's file).

**Verification:** the new app tests; then on the user's PC, Love from Seagull Island switched to فارسی shows Farsi on all 16 pages.

*Changed while building:*

- Built `Core/Text/StoryLines.cs` (`Clean`, `SameWords`) with `StoryLinesTests`. A page counts as edited when its cleaned text matches the source in none of the book's languages.
- The open-time reconcile to the saved language is async, with the same loading state and Cancel as a switch. A saved language the book lacks falls back to the start language.
- `KeptTextNotice` on the Check step names kept pages by printed number. Run ownership is `_translateCancel`: the newest run wins.
- Tests: `DinoLanguageSwitchTests` (App), `StoryLinesTests` (Core), `TestData/seagull-saved-book.json` (copied from the user's previous `book.json`, never edited). 559 tests pass, `dotnet format` clean.
- The cause of the `\n\n` to `\n` collapse was not found in code: the reader, translator, `SeedText` and save/reload all keep `\n\n`. Likely the Story text TextBox round trip; the fix does not depend on it.
- No VM run on the night run: free memory 7.0 GB, under the 8 GB VM threshold.

#### From review

Review files: `reviews/pr-58-high.md` and `reviews/pr-58-check2.md` read in full by the recording agent; `reviews/pr-58-medium.md` and `reviews/pr-58-check.md` read by the wave coordinator, not re-opened by the recording agent (feedback loop 2026-11-18-1302).

- Skipped #7 (`reviews/pr-58-high.md`): "`LanguageSwitch` is a single overwritten Task test seam; a second switch is not serialized with a first still running." - only a test seam; the select is disabled while busy, so no user path overlaps two switches.
- Skipped #8 (`reviews/pr-58-high.md`): "The package is fabricated, not the user's real .dino." - the fabricated package drives the same ApplyTranslated path; the real 16-page book stays in the on-PC verification above.
- Skipped #3 (`reviews/pr-58-medium.md`): "Reconcile changes page text but calls no ScheduleBookTextSave, so book.json keeps old text/language until another edit." - the disk text is reconciled again on every open, so the difference never reaches the screen or print; a new save path would add an overwrite risk BookTextStore guards against.
- Skipped #4 (`reviews/pr-58-medium.md`): "A user edit equal to another form's cleaned text (including the source fallback when a language has no entry) counts as unedited and is overwritten on the next switch with no kept notice." - a page whose text equals one of the book's own wordings carries no words of the user's to lose; it gets the same sentences in the chosen language.
- Skipped #2 (`reviews/pr-58-check.md`): "reconcile is documented as not rewriting disk text, but ApplyForms calls SetStory, which updates _textEntries and schedules a book.json save." - the save writes the reconciled pages into a book.json that just loaded fine, so disk matches the screen; no unreadable file is overwritten.
- Skipped #4 (`reviews/pr-58-check.md`): "reconcile starts synchronously inside Show before FolderPath, Refresh, Step = Check and ReleaseMemory finish; IsTranslating is set mid-Show." - the reconcile's first await is Task.Run, so nothing is applied until after Show has returned.
- Skipped #5 (`reviews/pr-58-check.md`): "LanguageSwitch is assigned to the reconcile task, but ApplyHandoverLanguageAsync calls ApplyLanguageAsync directly without updating it." - LanguageSwitch is a test seam; no user path reads it.
- Skipped #7 (`reviews/pr-58-check.md`): "if the user deletes or reorders a page during Task.Run, kept pages no longer in the list map to 0 and are dropped from the notice, which then under-reports pages kept." - a page the user deleted has nothing left to name.
- Skipped #5 (`reviews/pr-58-medium.md`): "Notice is not cleared on later edits, reorder or page removal, so it can name stale pages." - the notice describes the last switch and is cleared at the next switch or open.
- Skipped #3 (`reviews/pr-58-check2.md`): "Nothing throws today; wrap Cancel in try/catch or null before dispose." - nothing throws today, and after the second check pass only crash, data-loss, security, privacy and loading-state findings reopen code.

#### Follow-ups

Not fixed: after the second check pass only crash, data-loss, security, privacy and loading-state findings reopen code, and neither is one.

- `reviews/pr-58-check2.md` #1 (`MainViewModel.Dino.cs:568`, `ApplyLanguageAsync`): "keep _appliedLanguage updated only on successful ApplyRun and revert to that." The select is left on a language the pages are not in only after a switch started during the open-time reconcile and then cancelled.
- `reviews/pr-58-check2.md` #2 (`MainViewModel.Dino.cs:593`): "separate the off-thread translate failure from the apply step, or undo applied pages." It needs an exception inside the in-memory apply.

#### For CLAUDE.md (lifted 2026-11-18)

- A Dino page counts as user-edited only when its `StoryLines.Clean` text matches the book's source in none of its languages; never compare raw strings. (Slice 25: docs/plan/slices/25-dino-language-switch-pages.md)

#### Not yet verified

- On the user's PC, Love from Seagull Island opened (already saved as `fa` over English) shows Farsi on all 16 pages, and switching fa, en, fa keeps that.
- No VM run on the night run (free memory 7.0 GB, under the 8 GB VM threshold).
