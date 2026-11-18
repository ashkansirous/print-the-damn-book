### Slice 25 — Dino: switching language translates every page, not only the cover

*In progress (wave 2026-11-18), draft PR.*

**Touches:** `src/PrintTheDamnBook.App/ViewModels/MainViewModel.Dino.cs`, `src/PrintTheDamnBook.App/ViewModels/MainViewModel.Story.cs`, `src/PrintTheDamnBook.Core/Text/StoryLines.cs` (new), `tests/PrintTheDamnBook.App.Tests/**`, `tests/PrintTheDamnBook.Core.Tests/**`

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
- A page that kept its own text after a switch is named in the existing language-switch notice path (`NoticeOf`,
  `MainViewModel.Dino.cs`), e.g. "Pages 3, 7 kept your own text." No new view.

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

- [ ] Decide "edited" by what the user did, not by string equality: keep a per-page flag set only when the user types in *Story text*, or compare after the same
      normalisation the save applies. A page the user never edited always follows the language.
- [ ] A page the user did edit is not changed silently: show which pages kept their own text after a language switch.
- [ ] App test: open a .dino with a two-paragraph page, save, reopen, switch to `fa`; every page's band and *Story text* is Farsi. Plus: an edited page keeps its text and is named.
- [ ] Run it against the user's saved `book.json` case above (copy it into the test data, never edit the user's file).

**Verification:** the new app tests; then on the user's PC, Love from Seagull Island switched to فارسی shows Farsi on all 16 pages.
