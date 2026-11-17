### Slice 23 — Text that doesn't fit gets its own page, no empty lines, and the Dino back cover

*Planned 2026-11-17 from the user's report; not built yet.*

**Touches:** `src/PrintTheDamnBook.Core/Text/StoryLines.cs` (new), `src/PrintTheDamnBook.Core/Text/BookText.cs`, `src/PrintTheDamnBook.Core/Layout/StoryTextLayout.cs`, `src/PrintTheDamnBook.Core/Layout/StoryPlacement.cs` (new), `src/PrintTheDamnBook.Core/Dino/DinoBook.cs`, `src/PrintTheDamnBook.Core/Dino/DinoBookReader.cs`, `src/PrintTheDamnBook.Core/Dino/DinoBackCover.cs` (new), `src/PrintTheDamnBook.Core/Dino/BackCoverWording.cs` (new), `src/PrintTheDamnBook.App/Printing/StoryPainter.cs`, `src/PrintTheDamnBook.App/ViewModels/MainViewModel.Story.cs`, `src/PrintTheDamnBook.App/ViewModels/MainViewModel.Dino.cs`, `src/PrintTheDamnBook.App/ViewModels/PageNotes.cs`, `src/PrintTheDamnBook.App/Services/DinoBookCache.cs`, `src/PrintTheDamnBook.App/Views/CheckStepView.xaml`, `tests/PrintTheDamnBook.Core.Tests/**`, `tests/PrintTheDamnBook.App.Tests/**`, `tools/desktop-test/**`

## Why

The user, 2026-11-17 (RefineScope), three reports from printing a book that day:

1. "when the text cannot be contained, in the text box, you should automatically set the text to be written in a
   separate page. I ran the code today and that was not the case." The user says they asked for this before; no
   slice or note records it. Slice 14 (`14-typed-text-pages.md:20`, `:94`) made *Own page* a per-page switch
   the user flips by hand, and its fit warning tells them to ("give it its own page"). The 2026-10-27 Letter run
   saw three-little-pigs.dino warn "5 pages have more words than fit" (`plan.md` *Outstanding work*) and only noted it.
2. "where there is an empty line between text, you should remove it ... I tried printing a book today and had to
   manually remove the empty lines so that the text can be small enough to be in the text container and not take an
   extra page." No slice mentions blank lines.
3. "I added a back cover page in the dino files. Now all the books have a back cover ... a credits page, where it
   contains writers name, illustrator and if the story is re-written for kids + date. That page is the back cover.
   it should be printed at the end and preferably in the same language as you are printing the book. if the back
   cover is only in english, it is fine to write it in english as well. the text in the back cover should be in the
   middle by default cause the images I generated mostly have the characters in the bottom."

Checked on `origin/main` 2026-11-17 (ground-in-code, read only, the app was not run):

- `Core/Layout/StoryTextLayout.cs` `Choose()` (~:76) tries the shrink steps (band 22/20/18/16/14 pt, own page
  28/24/20/18/16 pt) and returns the smallest with `Fits=false`. The App only counts misfits
  (`MainViewModel.Story.cs:738`); placement changes only through the user's switch (`:309`, `:402`). Dino import
  seeds every page `OnPicture` (`MainViewModel.Dino.cs:418`). A misfit is clipped to the panel (`StoryPainter.cs:71`).
  No test covers moving a page.
- Blank lines: `BookText.cs:134` `Normalize` drops only whitespace-only entries, `DinoBookReader.cs:130` only caps
  length, `StoryPainter.cs:110` trims the ends. Inner `"\n\n"` reaches WPF `FormattedText` as a full-height line.
- Back cover: Dino Reader (`D:\ReadTheStupidText`, `src/Dino/DinoReader.Core/BackCover.cs:25`) stores it as the
  **last** entry of `pages` with a `backCover` object `{ storyBy, retold, illustration, month "YYYY-MM", picture }`
  (`StoryJson.cs:121-135`), picture `back-cover.jpg`, page text = the English credits. Credit lines are built at
  display time in the reading language (`src/Dino/DinoReader.App/BackCoverText.cs`: labels `BackCover_StoryBy`,
  `_OriginalStoryBy`, `_Traditional`, `_RetoldBy`, `_Illustration`, `_Date`, `_And`, `_Months`, `_Digits` in its 15
  `AppResources*.resx`; `storyBy` may hold several names joined with `;`). PTDB's reader has no `BackCover` field,
  so it most likely prints that page as an ordinary page with English text, then the blank back cover
  `DinoBookCache.cs:30,122,218` generates (`:408` "A Dino book has no back cover"). Not confirmed on a real book.
- Dino Reader's own Slice 405 (planned 2026-11-17, not built) will add per-language spellings of the names.
  Until it lands the names stay as written.

## Decisions (user, 2026-11-17: "ok to all recommendation")

1. **Overflow moves by itself.** When a page's text doesn't fit its band even at the smallest band size (14 pt),
   the page switches to *Own page* automatically, and a note in the Check step names the pages that moved. A page
   the user set to *On the picture* by hand is never moved again: their choice wins, it keeps the existing fit warning.
2. **Empty lines are dropped** when a book is laid out and printed, for every book (typed `book.json` and `.dino`).
   Each line keeps its own line break; the text is not joined into one paragraph. Files are never rewritten.
3. **Back cover credits are in the print language**, using the same wording as Dino Reader (its labels, month
   names and digits copied over). A language without that wording uses English.
4. **The back cover prints as the book's real back cover**, the last outside page, in place of the blank one. Its
   credits sit in the **middle** band by default and can be moved up or down in the Check step like any band.
   A `.dino` without a back cover keeps the generated blank, as today.

## Items

Ordered smallest end-to-end first; the three parts touch different code and may be built one after another in
one branch.

### A. Empty lines (Decision 2)

- [ ] `Core/Text/StoryLines.cs` (new): one function that removes lines that are empty or whitespace only and
  trims each line's ends, keeping single line breaks; `"\r\n"` treated like `"\n"`.
- [ ] Apply it in Core where story text enters layout (the text handed to `StoryTextLayout` and to the painter),
  never in `BookTextStore` or the `.dino` file. `StoryPainter.cs:110` gets the cleaned text from Core, no own trimming.
- [ ] Unit tests: `"a\n\nb"` → `"a\nb"`, `"a\n \n\n b"` → `"a\nb"`, CRLF, leading/trailing empty lines, a text
  that is only empty lines (counts as no text).
- [ ] App test: a page whose text fits only without its empty lines reports *fits*.

### B. Text that doesn't fit moves to its own page (Decision 1)

- [ ] Core records whether a page's placement was chosen by the user (`StoryPlacement.cs`, new: placement +
  `ChosenByUser`), saved with the page's text so it survives a reopen; old `book.json` files read as "not chosen".
- [ ] Core decides the move: given the band fit result at the smallest step and `ChosenByUser`, a page not chosen
  by the user and not fitting becomes *Own page*. The App asks Core; it never decides (repo rule: layout in Core).
- [ ] The App re-checks after each typing pause, language change and paper change (same triggers as today's fit
  line). A page moved automatically moves back onto the picture if its text later fits again (it was never the
  user's choice).
- [ ] Flipping the switch in the Check step marks the page `ChosenByUser`; choosing *On the picture* by hand keeps
  today's warning "Too long for the band, even at 14 pt …".
- [ ] Note in the Check step (`PageNotes.cs`): "Moved to its own page so the words fit: pages 3, 7." Copy from
  `docs/design/` if it has one; otherwise this text, recorded here.
- [ ] Unit tests in Core for the decision (fits, doesn't fit, user chose picture, user chose own page, fits again);
  App test that three-little-pigs-like long text moves on its own.

### C. Dino back cover (Decisions 3 and 4)

- [ ] `Core/Dino/DinoBackCover.cs` (new) and `DinoBookReader`: read the `backCover` object on the last page
  (`storyBy`, `retold`, `illustration`, `month` `YYYY-MM`, `picture`), with the reader's existing caps and name
  checks (repo rule: `.dino` is untrusted). A bad or missing field drops only the credits, the picture still
  prints. `DinoBook` gets `BackCover` (null when absent); the page is taken out of `Pages`.
- [ ] `Core/Dino/BackCoverWording.cs` (new): the credit lines in a language, the same lines and order as Dino
  Reader's `BackCover.Lines()` / `BackCoverText.Lines` (story by / original story by / traditional tale, retold by,
  illustration, month and year). Labels, month names, digits and "and" copied from Dino Reader's 15
  `AppResources*.resx` into a Core table; a language not in the table, or a broken entry, uses English. Names are
  printed as written (until Dino Reader's Slice 405 gives per-language names; then read those when present).
- [ ] `DinoBookCache`: when the book has a back cover, its picture becomes the book's back cover page instead of
  the generated blank `back cover.png`; without one, keep the blank (`:30`, `:122`, `:218`, `:408`).
- [ ] The back cover page's text is the credit lines in the selected print language, re-made when the language
  changes; default band **Middle** (`TextBand.Middle`, `StoryTextLayout.BandTop`), movable in the Check step and
  never re-suggested over once the user moves it (Slice 16 rule). Decision 1 also applies to it.
- [ ] Imposition: it lands as `PageKind.BackCover`, so blanks go before it (existing rule, no change to the sheet maths).
- [ ] Tests: reader (with, without, broken `backCover`), wording (English, Farsi digits and months, German, an
  unknown language falls back to English, two names joined with the language's "and"), cache (back cover picture
  used, blank kept when absent), App (language switch re-makes the credits; band Middle by default).

## Verification

- `dotnet test PrintTheDamnBook.slnx` green, with the tests above.
- Desktop check (`tools/desktop-test/`, a new scripted flow, per Slice 21): open a `.dino` that has a back cover
  (a current book from Dino's store), print to PDF in its own language and in Farsi: the last outside page is the
  back-cover picture with the credits in the middle in that language; no blank page after it; pages with long text
  are on their own pages and the note names them; a typed `book.json` page with empty lines lays out without them.
- Not on a device until then: a real paper print and fold of such a book (joins Slice 14's open check).
