### Slice 14 — Story text: typed in the app, on the picture or on its own page

*Built 2026-10-26 (PR #26), desktop acceptance passed (10 of 10 items); not yet verified on Letter paper or a real paper print.* The design pass is done (`docs/design/Story Text.dc.html`, frames 1a-1c; see Design below).

**Touches:** `src/PrintTheDamnBook.Core/BookPage.cs`, `src/PrintTheDamnBook.Core/Ordering/**`,
`src/PrintTheDamnBook.Core/Layout/**`, `src/PrintTheDamnBook.Core/Text/**` (new),
`src/PrintTheDamnBook.Core/Imposition.cs`, `tests/PrintTheDamnBook.Core.Tests/**`,
`src/PrintTheDamnBook.App/Printing/**`, `src/PrintTheDamnBook.App/Views/CheckStepView.xaml*`, `src/PrintTheDamnBook.App/Views/PrintStepView.xaml*`,
`src/PrintTheDamnBook.App/ViewModels/**`, `src/PrintTheDamnBook.App/Services/**`,
`src/PrintTheDamnBook.App/Services/BookTextStore.cs` (new), `src/PrintTheDamnBook.App/Fonts/**`,
`src/PrintTheDamnBook.App/PrintTheDamnBook.App.csproj`, `docs/design/**`

**Why.** User request, 2026-10-26: people should be able to make a picture book like the user's Dino Reader books,
where the pictures and the story text are kept apart (`D:\ReadTheStupidText`, `.dino` files). Some pictures have
enough empty space to carry their text; others need the text on a page of its own. This slice adds the text model and
typed text; Slice 15 fills the same model from `.dino` files; Slice 16 adds the empty-space suggestion.

**Decisions** (locked with the user via RefineScope, 2026-10-26):

- Each picture page can have **story text** and a **placement**: *On the picture* (default) or *Own page*.
- **On the picture:** the text sits in a band over the picture. This slice builds only the **bottom band**, at most
  30% of the picture's height (the Dino Reader rule, `ReadTheStupidText/src/DinoReader.App/CLAUDE.md:59`), on a soft
  rounded panel in the Dino Reader colours (`ReadTheStupidText/docs/design/dino-reader/`) so it reads on any picture.
  Top and middle bands come in Slice 16.
- **The panel is one fixed style**, never colour-picked per page. Dino Reader's own values (looked up 2026-10-26):
  `TextCard` white at 92% opacity, ink `#2b2540`, corner radius 16, padding 14 x 10, margin 8, line height 1.5
  (`ReadTheStupidText/src/DinoReader.App/PictureTextPages.cs:17-24`, `ReadingTheme.cs:50`, `App.xaml:10`). Opacity,
  radius, padding and ink are Core constants. The mock's warm cream is a stand-in (see the Open point below).
- **Text pages are plain paper white** by default. An optional per-book setting under Print, More options, "Text
  pages: White / Tinted from cover" tints them with the cover's average colour lifted to OKLCH L >= 0.96. The tint is
  computed in Core on a pixel buffer and tested. There is no per-page background choice anywhere.
- The per-book setting is stored in `book.json`. `schemaVersion` stays 1 while `book.json` has not shipped; bump it if
  it has.
- **Reference picture** `docs/design/reference/blackberry-path.png` (1493x2000, 3:4): drawn with room for its words,
  the bottom ~30% plain (row brightness spread about 6 in 70-85% of the height, against 30-40 in the busy middle).
- **Own page:** the text gets its own page, **facing the picture: text on the left, picture on the right**. Core
  inserts blank pages where needed so every text page lands on a left page with its picture on the right of the same
  spread. Blanks still go before the back cover as now; spread-alignment blanks go where they are needed.
- **Font:** Andika, bundled with its OFL licence and listed in `FontLicences`. One fixed size per paper size,
  shrinking to fit when the text is too long for its band or page. The size and the shrink steps are Core constants
  with tests, like `PageNumbering.FontMm`.
- **Saving:** the text lives in `book.json` next to the images in the folder (page text, placement, band, keyed by
  image file name). Opening the folder again brings it back. This lifts "saving a book project file" out of scope for
  story text only; page order is still not saved.
- Covers can carry text too (the title on the front cover is how Slice 15 uses it). Left-to-right only.
- All layout maths (spread alignment, band rectangle, panel padding, line wrap box, shrink-to-fit) lives in Core and
  is unit-tested. The App only measures and draws text into rectangles Core gives it.

**Work.**

- [x] Design pass: imported to `docs/design/Story Text.dc.html` and `StoryCheck.dc.html`; Design subsection below.
- [x] Core, smallest end to end: `BookPage` gains optional `StoryText` (text + placement); `PageList` expands *Own
      page* into a text page plus alignment blanks; `Imposition` treats a text page like any page. Tests for spread
      alignment with 0, 1 and many text pages, odd and even counts, and the blanks-before-back-cover rule kept.
- [x] Core layout: the bottom-band rectangle and panel inside `HalfSheetLayout` (Fit and Fill, with page numbers on
      and off), the text-page box, and shrink-to-fit sizing. Tests for A4 and Letter.
- [x] App: bundle Andika (+ licence); `FaceRenderer` draws the panel and the text (outline geometry, like the page
      numbers) on automatic, manual and test-sheet paths, rotated with backs.
- [x] App: the text editor in the Check step per page (text box, *On the picture* / *Own page*), thumbnails and
      As printed tiles show the text, as in the Design subsection.
- [x] Core: `TextPageTint` (average cover colour lifted to OKLCH L >= 0.96) on a pixel buffer, tested with synthetic
      images; the "Text pages: White / Tinted from cover" setting in More options and in `book.json`.
- [x] App: `BookTextStore` reads and writes `book.json` in the folder (debounced save; a missing or broken file reads
      as no text and never blocks opening the folder). The JSON schema has a `schemaVersion`.

#### Design (Story Text mock, section 1a-1c, added 2026-10-26)

Three frames for this slice (1a on the picture, 1b own page, 1c As printed), live renders of `StoryCheck.dc.html`
(`docs/design/Story Text.dc.html`). Each shows the whole Check step. The mock agrees with the Decisions above except
where an Open point says so.

- **Layout.** The spreads area keeps its own scroll. A right-hand editor panel, about 316 px wide, on the surface card
  sits beside it. Folder-book heading stays "Check the order"; sub-line "Click a page to give it words. Drag to move
  it; a text page travels with its picture."
- **Nothing selected.** Empty state "Click a page to give it words".
- **Selected.** Top to bottom: title (Page N, Front cover or Back cover) with the file name under it; previous and next
  buttons; a live preview (one page, or text page and picture side by side for *Own page*); a "Story text" box typed in
  Andika; the fit line; the "On the picture" / "Own page" segmented switch; the save status; "Remove text".
- **Placement note** under the switch: "A soft panel over the picture, never more than 30% of it." / "Text on the
  left, picture on the right, facing each other when the book is open." / with a spacer: "Text on the left, picture
  on the right. I put a spacer page in front so they face each other."
- **Save status.** "Saved to book.json in the folder" (folder) or "Saved with this Dino book" (Slice 15); a saving
  state shows while the debounce runs.
- **Selecting.** Clicking a picture or its text page selects the picture. A text page travels with its picture when
  dragged.
- **Covers.** *Own page* is disabled; note "Covers keep their words on the picture."
- **Thumbnails and As printed.** Band text and text pages are drawn at true print scale (same Core rectangles and
  font sizes), so the Reading order and As printed tiles are the print. Text page tile label "Text for N" with the
  Phosphor `text-aa` icon. A spread-alignment blank is a dashed tile labelled "spacer", note "Keeps the text facing its
  picture."; normal filler stays "blank"; a Dino book's missing back cover is "blank back". The status line adds
  "N text pages, N spacer" when there are any.
- **Fit line** (Phosphor `check`; `arrows-in` when shrunk; `ph-fill ph-warning` in a warning colour when too long):
  "22 pt, fits the band." / "Shrunk to 18 pt so it fits the band." / "Too long for the band, even at 14 pt. Cut it down,
  or give it its own page." For *Own page* "its page" replaces "the band" and the last sentence is "Cut it down."
- **Shrink steps** (placeholders in the mock; Core owns them and its tests confirm them): band 22, 20, 18, 16, 14 pt;
  own page 28, 24, 20, 18, 16 pt.
- **Simplified in the mock:** pictures are generated placeholders; pt sizes are screen-scaled stand-ins.

**New strings** (English only today; every locale if one exists by then): the quoted copy above, plus "Story text",
"On the picture", "Own page", "Remove text", "Text for N", "spacer", "blank back".

**Open point: panel colours.** The Decisions say the Dino Reader colours; the mock uses a warm cream
(`oklch(0.97 0.025 85 / .93)` on ink `oklch(0.33 0.04 55)`) as a stand-in. Dino Reader's real values are the values
named in the Decisions (white at 0.92, ink `#2b2540`). **Default:** use the real Dino Reader values.

**Open point: panel opacity.** Dino Reader uses 0.92; the mock and the user's note say about 0.93. **Default:** 0.92, a
Core constant (Slice 16 then varies it).

**Open point: the More options row.** The mock does not show the "Text pages: White / Tinted from cover" row.
**Default:** a plain two-option select in Print, More options, styled like the other options.

**Verification.**

- `dotnet test` is green.
- `Book sample` with text typed on three pages: two *On the picture*, one *Own page*. Print to Microsoft Print to PDF;
  the own-page text faces its picture after folding, the band text never covers more than the bottom 30%, a long text
  shrinks rather than overflows.
- Close and reopen the folder: the text is back.
- `blackberry-path.png` with a bottom-band text: the text sits on the plain ground and does not cover the rabbits.
- Text pages print white by default and tinted when the setting is on.

*Changed while building:*

- Spreads do not scroll on their own (`MainWindow.xaml` is outside Touches); the editor panel is sticky beside them.
- Icon code points went into `CheckStepView.xaml` (`Themes/Icons.xaml` is outside Touches).
- The band is the panel plus its bottom margin, at most 30% of the picture height. Dino panel units (radius 16, padding 14x10, margin 8) scale from Dino's 360-wide card.
- A4 and Letter share the same pt steps. The More options row is a segmented two-option control. The preview border uses `Bg70Brush` (was 55%).
- Extras: Check chip "N pages have more words than fit"; As printed tiles are selectable; an unreadable or newer-schema `book.json` is never overwritten (the editor says so); pages added from outside the folder are keyed by full path (`BookTextJson.KeyFor`); `BookPage` equality is by reference.

*From review:* high pass 11 findings: applied 1, 2, 3, 4, 5, 7, 8; skipped 6 and 11 (performance, unmeasured) and 9 and 10 (style). Medium pass 10 findings: applied 1, 2, 5, 6, 7, 8; skipped 3 (a removed page's text returns on reopen, by design), 4 (performance), 9 and 10 (style). 236 tests pass.

*Desktop acceptance (2026-10-26):* all 10 items pass (Print to PDF, blackberry-path band on plain ground, tint, save on exit, Fill tiles match the PDF).

#### For CLAUDE.md

- Story text layout (band rectangle, shrink steps, text-page box, spread-alignment spacers, tint) lives in `Core/Text` and `Core/Layout`; the App only draws it. (Slice 14: docs/plan/slices/14-typed-text-pages.md)
- `book.json` is read and written only through `Services/BookTextStore`; never overwrite a file it could not read. (Slice 14: docs/plan/slices/14-typed-text-pages.md)
- Andika is the only story font; its licence is listed in `Services/FontLicences`. (Slice 14: docs/plan/slices/14-typed-text-pages.md)

#### For README.md

- Story text: type words for each page, on the picture or on its own page; saved in `book.json` in the folder.

#### Not yet verified

- Printing on Letter paper.
- A real paper print and fold.
- Performance with many story pages (re-layout on the UI thread on each typing pause).
