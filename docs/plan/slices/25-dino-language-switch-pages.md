### Slice 25 — Dino: switching language translates every page, not only the cover

*Planned.*

**Touches:** `src/PrintTheDamnBook.App/ViewModels/MainViewModel.Dino.cs`, `src/PrintTheDamnBook.App/ViewModels/MainViewModel.Story.cs`, `tests/PrintTheDamnBook.App.Tests/**`

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
