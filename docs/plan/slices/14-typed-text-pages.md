### Slice 14 — Story text: typed in the app, on the picture or on its own page

*Planned.* Needs a design pass for the Check-step text editor before it is built (see Design below).

**Touches:** `src/PrintTheDamnBook.Core/BookPage.cs`, `src/PrintTheDamnBook.Core/Ordering/**`,
`src/PrintTheDamnBook.Core/Layout/**`, `src/PrintTheDamnBook.Core/Text/**` (new),
`src/PrintTheDamnBook.Core/Imposition.cs`, `tests/PrintTheDamnBook.Core.Tests/**`,
`src/PrintTheDamnBook.App/Printing/FaceRenderer.cs`, `src/PrintTheDamnBook.App/Views/CheckStepView.xaml*`,
`src/PrintTheDamnBook.App/ViewModels/**`, `src/PrintTheDamnBook.App/Services/FontLicences.cs`,
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

- [ ] Design pass: a Check-step text editor (where the text box sits, the placement switch, how a text page shows in
      the thumbnail strip and in the As printed preview). Import it with `import-claude-design` into `docs/design/`;
      then write this slice's Design subsection with `design-to-plan`.
- [ ] Core, smallest end to end: `BookPage` gains optional `StoryText` (text + placement); `PageList` expands *Own
      page* into a text page plus alignment blanks; `Imposition` treats a text page like any page. Tests for spread
      alignment with 0, 1 and many text pages, odd and even counts, and the blanks-before-back-cover rule kept.
- [ ] Core layout: the bottom-band rectangle and panel inside `HalfSheetLayout` (Fit and Fill, with page numbers on
      and off), the text-page box, and shrink-to-fit sizing. Tests for A4 and Letter.
- [ ] App: bundle Andika (+ licence); `FaceRenderer` draws the panel and the text (outline geometry, like the page
      numbers) on automatic, manual and test-sheet paths, rotated with backs.
- [ ] App: the text editor in the Check step per page (text box, *On the picture* / *Own page*), thumbnails and
      As printed tiles show the text.
- [ ] App: `BookTextStore` reads and writes `book.json` in the folder (debounced save; a missing or broken file reads
      as no text and never blocks opening the folder). The JSON schema has a `schemaVersion`.

**Verification.**

- `dotnet test` is green.
- `Book sample` with text typed on three pages: two *On the picture*, one *Own page*. Print to Microsoft Print to PDF;
  the own-page text faces its picture after folding, the band text never covers more than the bottom 30%, a long text
  shrinks rather than overflows.
- Close and reopen the folder: the text is back.
