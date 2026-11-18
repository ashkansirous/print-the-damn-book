### Slice 23 — Text that doesn't fit gets its own page, no empty lines, and the Dino back cover

*Built 2026-11-18 (night run, wave 1), merged as PR #59 (2768bce). Tests 629 pass (Core 552, App 77), `dotnet format` clean. Desktop VM run 2026-11-18 14:23: 6 of 6 checks PASS (store-dialog skipped, needs network). Back-cover print in its own language and Farsi, a long book and a real paper print not yet verified (see below).*

*Changed while building:* Part A cleans through `StoryPainter.Prepare` (calling `StoryLines.Clean`) as the single cleaner. Part B: the move decision is Core `StoryPlacement`; `ChosenByUser` is a `bool?` on `PageTextEntry`, always written; a legacy Own page reads as chosen; the fit result is memoised per page; a page is undecided until its real picture size is loaded; the Check step shows `MovedTextNotice`. Part C: `DinoBackCover` and `BackCoverWording` (15 languages copied from Dino Reader); `DinoBookCache` uses the back-cover picture and `.complete` starts with a layout version line; the credits sit in the Middle band, are re-made on a language switch, and `BackCoverCleared` is kept. Known behaviours: the back cover cannot move to its own page (covers have had no Own page since the typed-text slice); credits that do not fit get the existing too-long warning; credits come back once on an old saved book that has no back-cover entry.

**Touches:** `src/PrintTheDamnBook.Core/Text/StoryLines.cs` (exists since Slice 25: reuse and extend, never a second cleaner), `src/PrintTheDamnBook.Core/Text/BookText.cs`, `src/PrintTheDamnBook.Core/Text/StoryText.cs`, `src/PrintTheDamnBook.App/Services/BookTextStore.cs`, `src/PrintTheDamnBook.App/ViewModels/PageViewModel.cs`, `src/PrintTheDamnBook.App/ViewModels/MainViewModel.cs`, `src/PrintTheDamnBook.App/Services/StoryTiles.cs` (review 1 #5), `src/PrintTheDamnBook.App/Services/ThumbnailLoader.cs` (review 1 #5), `src/PrintTheDamnBook.App/Services/ImageDecoder.cs` (review 2 #4: one open gives thumbnail and real size), `src/PrintTheDamnBook.Core/Dino/DinoCacheLayout.cs` (review 1 #3), `src/PrintTheDamnBook.Core/Layout/StoryTextLayout.cs`, `src/PrintTheDamnBook.Core/Layout/StoryPlacement.cs` (new), `src/PrintTheDamnBook.Core/Dino/DinoBook.cs`, `src/PrintTheDamnBook.Core/Dino/DinoBookReader.cs`, `src/PrintTheDamnBook.Core/Dino/DinoBackCover.cs` (new), `src/PrintTheDamnBook.Core/Dino/BackCoverWording.cs` (new), `src/PrintTheDamnBook.App/Printing/StoryPainter.cs`, `src/PrintTheDamnBook.App/ViewModels/MainViewModel.Story.cs`, `src/PrintTheDamnBook.App/ViewModels/MainViewModel.Dino.cs`, `src/PrintTheDamnBook.App/ViewModels/PageNotes.cs`, `src/PrintTheDamnBook.App/Services/DinoBookCache.cs`, `src/PrintTheDamnBook.App/Views/CheckStepView.xaml`, `tests/PrintTheDamnBook.Core.Tests/**`, `tests/PrintTheDamnBook.App.Tests/**`, `tools/desktop-test/**`

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

## Design notes (wave coordinator, 2026-11-18, night run)

- **Slice 25 is merged first** (PR #58): `Core/Text/StoryLines.Clean` already drops empty lines and trims each line; part A
  applies it where text enters layout and the painter, it does not write a second one. Slice 25's rule stands: a Dino page is
  user-edited only when its cleaned text matches the book's source in none of its languages.
- **`ChosenByUser` lives on `PageTextEntry`** (`BookText.cs`) as an optional JSON field, kept by
  `BookText.Normalize`, read and written only through `BookTextStore` (repo rule). This version always writes it (true or false).
  An entry **without** the field comes from before this slice, when only the user could pick Own page: Own page reads as chosen,
  On the picture reads as not chosen (the old default, which Decision 1 wants moved when the words do not fit).
- **Cleared back cover:** `BookText.BackCoverCleared` (written only when true) records that the user took the words off a Dino
  back cover, so the credits are not added again on the next open. It is the only other new saved field.
- **Cache layout version:** `.complete` starts with a `layout` line (`DinoCacheLayout.LayoutVersion`); a folder without it
  (made before the back cover picture was extracted) fails `Verify` and is extracted again.
- **Back cover credits follow Slice 25's rule:** the back cover page's text counts as the user's own only when its cleaned text
  equals the credit lines in none of the book's languages; otherwise a language switch (and the open-time reconcile) re-makes it
  in the new language. The credit lines per language come only from Core `BackCoverWording`.
- **Fit memo (review 2 #3, #4):** the band-fit result per page is memoised in the App (`MainViewModel.Story.cs`, `FitKey`): key =
  cleaned text (which also fixes the font and direction), paper, margin, scaling, page-number position and the picture's real
  size (these fix the band rectangle). `RefreshBook` therefore measures only pages whose key changed. A page whose real picture
  size is not loaded yet is not decided (no move, and `ThumbnailLoader.Peek` never decodes on the UI thread); the preload's
  completion already refreshes the book (`AddPreloadedAsync`, and the open paths preload before showing), which decides it then.
  `ThumbnailLoader` holds one entry per picture (thumbnail and real size from one open of the file, written in one step).
- **Back cover cleared (review 2 #1):** `BackCoverCleared` is set only when the user empties the back cover's words, and set back
  to false when words are given to it again; it is never inferred from a missing entry. Credits added to a book saved before the
  back cover was read use the language the select shows (the saved one when the book has it, else the starting language).
- Every value that rests on more than one input (the move decision: fit at the smallest step x `ChosenByUser`) is one pure,
  unit-tested Core function; the App only calls it.

## Items

Ordered smallest end-to-end first; the three parts touch different code and may be built one after another in
one branch.

### A. Empty lines (Decision 2)

- [x] `Core/Text/StoryLines.cs` (new): one function that removes lines that are empty or whitespace only and
  trims each line's ends, keeping single line breaks; `"\r\n"` treated like `"\n"`.
- [x] Apply it in Core where story text enters layout (the text handed to `StoryTextLayout` and to the painter),
  never in `BookTextStore` or the `.dino` file. `StoryPainter.cs:110` gets the cleaned text from Core, no own trimming.
- [x] Unit tests: `"a\n\nb"` → `"a\nb"`, `"a\n \n\n b"` → `"a\nb"`, CRLF, leading/trailing empty lines, a text
  that is only empty lines (counts as no text).
- [x] App test: a page whose text fits only without its empty lines reports *fits*.

### B. Text that doesn't fit moves to its own page (Decision 1)

- [x] Core records whether a page's placement was chosen by the user (`StoryPlacement.cs`, new: placement +
  `ChosenByUser`), saved with the page's text so it survives a reopen; old `book.json` files read as "not chosen".
- [x] Core decides the move: given the band fit result at the smallest step and `ChosenByUser`, a page not chosen
  by the user and not fitting becomes *Own page*. The App asks Core; it never decides (repo rule: layout in Core).
- [x] The App re-checks after each typing pause, language change and paper change (same triggers as today's fit
  line). A page moved automatically moves back onto the picture if its text later fits again (it was never the
  user's choice).
- [x] Flipping the switch in the Check step marks the page `ChosenByUser`; choosing *On the picture* by hand keeps
  today's warning "Too long for the band, even at 14 pt …".
- [x] Note in the Check step (`PageNotes.cs`): "Moved to its own page so the words fit: pages 3, 7." Copy from
  `docs/design/` if it has one; otherwise this text, recorded here.
- [x] Unit tests in Core for the decision (fits, doesn't fit, user chose picture, user chose own page, fits again);
  App test that three-little-pigs-like long text moves on its own.

### C. Dino back cover (Decisions 3 and 4)

- [x] `Core/Dino/DinoBackCover.cs` (new) and `DinoBookReader`: read the `backCover` object on the last page
  (`storyBy`, `retold`, `illustration`, `month` `YYYY-MM`, `picture`), with the reader's existing caps and name
  checks (repo rule: `.dino` is untrusted). A bad or missing field drops only the credits, the picture still
  prints. `DinoBook` gets `BackCover` (null when absent); the page is taken out of `Pages`.
- [x] `Core/Dino/BackCoverWording.cs` (new): the credit lines in a language, the same lines and order as Dino
  Reader's `BackCover.Lines()` / `BackCoverText.Lines` (story by / original story by / traditional tale, retold by,
  illustration, month and year). Labels, month names, digits and "and" copied from Dino Reader's 15
  `AppResources*.resx` into a Core table; a language not in the table, or a broken entry, uses English. Names are
  printed as written (until Dino Reader's Slice 405 gives per-language names; then read those when present).
- [x] `DinoBookCache`: when the book has a back cover, its picture becomes the book's back cover page instead of
  the generated blank `back cover.png`; without one, keep the blank (`:30`, `:122`, `:218`, `:408`).
- [x] The back cover page's text is the credit lines in the selected print language, re-made when the language
  changes; default band **Middle** (`TextBand.Middle`, `StoryTextLayout.BandTop`), movable in the Check step and
  never re-suggested over once the user moves it (Slice 16 rule). Decision 1 also applies to it.
- [x] Imposition: it lands as `PageKind.BackCover`, so blanks go before it (existing rule, no change to the sheet maths).
- [x] Tests: reader (with, without, broken `backCover`), wording (English, Farsi digits and months, German, an
  unknown language falls back to English, two names joined with the language's "and"), cache (back cover picture
  used, blank kept when absent), App (language switch re-makes the credits; band Middle by default).

## Verification

- `dotnet test PrintTheDamnBook.slnx` green, with the tests above.
- Desktop check (`tools/desktop-test/`, a new scripted flow, per Slice 21): open a `.dino` that has a back cover
  (a current book from Dino's store), print to PDF in its own language and in Farsi: the last outside page is the
  back-cover picture with the credits in the middle in that language; no blank page after it; pages with long text
  are on their own pages and the note names them; a typed `book.json` page with empty lines lays out without them.
- Not on a device until then: a real paper print and fold of such a book (joins Slice 14's open check).

#### From review

Four findings files read in full: `reviews/pr-59-high.md` (10 findings), `reviews/pr-59-medium.md` (10), `reviews/pr-59-check.md` (9), `reviews/pr-59-check2.md` (8). Every finding of the first three files that has no Skipped line below was fixed; the 8 findings of `pr-59-check2.md` are follow-ups, not fixed (after the second check pass only crash, data-loss, security, privacy and loading-state findings reopen code, and none of the 8 is one).

- Skipped #6 (`reviews/pr-59-high.md`): "ApplyAutomaticPlacement runs the FormattedText size search for every worded page on every RefreshBook; CountTooLong and tile drawing repeat it. Long books stall on each edit." - main already ran the same size search per refresh in its too-long count, and no measurement shows a wait; a long-book responsiveness check goes to Not yet verified.
- Skipped #8 (`reviews/pr-59-high.md`): "ChosenByUser added without a schema version bump. An older build ignores the field and rewrites the file without it" - no older build is in anyone's hands: no GitHub release and no Store listing exist.
- Skipped #10, second half (`reviews/pr-59-high.md`): "There is also no way to return a chosen page to automatic." - no control for returning to automatic was asked for.
- Skipped #5 (`reviews/pr-59-medium.md`): "an automatic OwnPage placement (ChosenByUser=false) is written into _textEntries and saved to book.json once any page text saves" - this version always writes chosenByUser, so a saved automatic Own page reads back as not chosen; the "flag lost" path needs a file this version never writes.
- Skipped #7 (`reviews/pr-59-medium.md`): "layout-version check re-extracts every existing cache folder when the layout line is missing. With an old-build window open on the same hash, the re-extract can hit an IOException, or the old build sweeps the new folder as incomplete." - needs two different builds open on the same book at once; no older build is distributed.
- Skipped #8 (`reviews/pr-59-medium.md`): "SplitBackCover only reads the last page's backCover object; if the last page is skipped/unreadable or the object is on an earlier page, the credits and picture are silently dropped with no warning." - Dino Reader writes the back cover only as the last page (ReadTheStupidText `src/Dino/DinoReader.Core/StoryJson.cs:121-129`); an unreadable last page has no usable back cover to lose.
- Skipped #9 (`reviews/pr-59-medium.md`): "clicking the already-selected Own page on an auto-moved page marks it ChosenByUser=true, so it never moves back after the text is shortened." - a click on Own page is the user's own choice, and Decision 1 says their choice wins.
- Skipped #10 (`reviews/pr-59-medium.md`): "sanitiser rewrites ChosenByUser = entry.IsChosenByUser on read, so legacy OwnPage entries with no field become explicit true and are saved that way." - matches the review-1 design: a legacy Own page is the user's choice, so saving it as explicit true keeps that meaning.
- Skipped #1 (`reviews/pr-59-check.md`): "A failed or transient decode is cached forever as LoadedPicture(null,null)" - a page whose picture cannot be decoded has no picture to lay a band over, so the move decision has nothing to measure.
- Skipped #4 (`reviews/pr-59-check.md`): "First fit measurement (HalfSheetLayout.Resolve + StoryPainter.FitBand, FormattedText) for every page runs on the UI thread in RefreshBook" - main already measured each page on the UI thread in its too-long count; the memo makes every later refresh cheap; long-book responsiveness stays in Not yet verified.
- Skipped #5 (`reviews/pr-59-check.md`): "Memo clears entirely at 1000 entries; each typed text adds a key." - a wipe at 1000 entries re-measures once; books have tens of pages.
- Skipped #7 (`reviews/pr-59-check.md`): "_backCoverCleared is never reset once set and is book-wide, not per language." - the user cleared the credits on purpose, and typing words again clears the flag.
- Skipped #9 (`reviews/pr-59-check.md`): "DecodeWithSize reads the header via a delay-created decoder, rewinds the same stream and hands it to BitmapImage; the first decoder is not disposed" - the file marks it unconfirmed and names no failure path.

#### Follow-ups

Not fixed in this slice; each from `reviews/pr-59-check2.md`. Moved to [Slice 26](26-s23-s25-review-followups.md) (parked) by the feedback loop of night 2026-11-18-1302; that file is the one to update.

- `reviews/pr-59-check2.md` #1: "ApplyAutomaticPlacement can change Placement while a print runs (only the timer path is guarded)." Unconfirmed.
- `reviews/pr-59-check2.md` #2: "SplitBackCover compares raw JSON strings Ordinal" so `Back.JPG` and `back.jpg` can print the artwork twice.
- `reviews/pr-59-check2.md` #3: "when BandFits returns null a ChosenByUser=false page is neither moved nor cleared" after a one-off decode failure.
- `reviews/pr-59-check2.md` #4: "LoadNotingSize starts _sampleTimer on first load on the UI thread" and a second full Refresh can drop the placement decision.
- `reviews/pr-59-check2.md` #5: "ApplyTypedText on a page with no story builds StoryText with ChosenByUser=false", so an Own page choice is lost when the text is cleared and retyped.
- `reviews/pr-59-check2.md` #6: "Verify returns false without the \"layout\\t2\" manifest line" so older cache folders are re-extracted and a second window can make the open fail.
- `reviews/pr-59-check2.md` #7 (low): "SpellingsIn walks and LINQ-filters every property of an untrusted per-language object before the MaxSpellingsPerLanguage break".
- `reviews/pr-59-check2.md` #8 (low): "WithBackCover runs on a load that could not be read (CanSave=false) and does not check Language is one the book has".
- `tools/desktop-vm/Invoke-DesktopTest.ps1` `$GuestChecks` (line 43) lacks `back-cover-and-fit`, so `-Only back-cover-and-fit` is refused; `tools/desktop-test/run-all.ps1` has it.

#### For CLAUDE.md (lifted 2026-11-18)

- Fit-to-page moves (text that doesn't fit goes to its own page) are decided only in Core `StoryPlacement`; a page the user placed by hand (`ChosenByUser`, legacy Own page counts) is never moved. (Slice 23: docs/plan/slices/23-fit-and-back-cover.md)
- A Dino back cover's credit lines come only from Core `BackCoverWording`; names and labels are never built in the App. (Slice 23: docs/plan/slices/23-fit-and-back-cover.md)

#### Not yet verified

- Print to PDF a store Dino book with a back cover in its own language and in Farsi: the credits are in the middle of the last outside page in that language, with no blank page after it (the credits are drawn into the picture; no script can read them).
- A long book (40+ pages) stays responsive while editing text.
- The real paper print and fold.
