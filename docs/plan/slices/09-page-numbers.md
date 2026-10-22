### Slice 09 — Optional page numbers, below or above

*Built 2026-10-22, awaiting merge; not yet verified on a real printed page.* Comes after Slice 07, because its control lives in the new Print step.

**Touches:** `src/PrintTheDamnBook.Core/Layout/**`, `tests/PrintTheDamnBook.Core.Tests/Layout*`,
`tests/PrintTheDamnBook.Core.Tests/PageNumber*`, `src/PrintTheDamnBook.App/Printing/**`,
`src/PrintTheDamnBook.App/Views/**`, `src/PrintTheDamnBook.App/ViewModels/**`

**Why.** User request, 2026-10-22: an option to print page numbers on the pages, below or above. It is completely optional.

**Decisions** (recommended defaults; the user can veto any):

- **Page numbers: Off (default) / Below / Above**, in the Print step's More options. The choice is remembered with
  the other settings in Slice 4's `PrintSettings`; a missing value reads as Off.
- Numbers follow the **position in the book**, not the file name, so they renumber after a reorder.
  Only interior pages are numbered: the Cover, the Back cover and blank padding pages get no number, and the first
  interior page is 1.
- The number is small and centred, in the margin just below or above the page image inside its half-sheet. It never
  covers the artwork. In Fill mode the image shrinks just enough to leave room for it. The size and offset scale with
  the paper size, and those values live in Core with tests.
- Manual two-sided printing, the test sheet and the sheet preview all show the numbers the same way.

**Work.**

- [x] Core: `PageNumbering` (which pages get which number) and the number's rectangle in
      `HalfSheetLayout`, with tests for reorder, blanks, covers, Fit and Fill, A4 and Letter.
- [x] Printing: `FaceRenderer` draws the number, and the settings model gains the option.
- [x] UI: the Off / Below / Above segmented control; the preview shows the numbers.

**Verification.**

- `dotnet test` is green.
- Print `Book sample` with Below and then with Above to Microsoft Print to PDF. The interior pages show the numbers 1–10,
  the covers and blanks show none, and no number covers the artwork.

#### Design (Redesign Directions + BookWizard mock, sections 1b and 1d, added 2026-10-22)

None of the 12 mock frames (1a-1d, 0a, 2a-2f) shows a page number, a page-number control or a number drawn on a page. So there is no design for this slice; the mock neither agrees nor disagrees with the plan. What it does settle for the surrounding UI is below.

- **Printed number.** No type or colour token applies. Nocturne is a screen theme: `--color-text` (#e9e9ed) would print as near-white. The numbers go on the paper, so size, font and colour are Core constants chosen by the slice (default in the Decisions: small, centred, scaled to the paper). Default: dark grey on white, in the app's printing font, with no Nocturne token. The thumbnail labels in the mock ("1", "2", 12 px `--color-neutral-300`, `BookWizard.dc.html` line 169-176) are screen captions for the file order, not printed numbers.
- **Control.** Page numbers join the collapsed More options on the Print step (Slice 07). Use the Nocturne `.seg` segmented control (`styles.css` lines 190-200): "Off" / "Below" / "Above", selected segment in `--color-accent` with a 1 px inset accent ring, divider `--color-divider`, radius `--radius-md`. It sits under a `.field` label "Page numbers" (the field label style, `styles.css` line 160-165). The Print step's Paper control (`BookWizard.dc.html` line 234) is the model: same `.seg`, 14 px text.
- **Sheet preview.** The mock's As printed tiles (84×119) and the stack preview (264×187) show only the page image and a text label; there is no room or style for a printed number. Default: draw the number on the tile only if the Slice 09 preview is the same bitmap the printer uses; otherwise leave the tile unchanged.

New strings for both locales: "Page numbers", "Off", "Below", "Above".

**Open point: No design for the number.** Plan: small, centred, in the margin. Mock: nothing. Default: build as the Decisions say, using Core constants, and have the user check one print.

*Built:* Core `Layout/PageNumbering` (`PageNumberPosition` Off/Below/Above; numbers by position in the padded book; covers, duplicate covers and blanks are unnumbered but blanks still count; `MinEdgeDistanceMm` 5; strip size scales with sheet height, about 6 mm on A4), `HalfSheetLayout.Split/Resolve` with a number strip, `PageNumberSetup` in `JobLayout`, `PrintSettings.PageNumbers` (missing or invalid reads as Off). `FaceRenderer` draws dark-grey (#444) numbers as outline geometry centred by ink bounds, rotated with backs, on the automatic, manual and test-sheet paths. Off/Below/Above `.seg` in More options; As printed tiles show a small number badge when numbers are on. 165 tests.

*Changed while building:* every page (covers and blanks included) reserves the strip, so all pages print at the same size. Removed dead preview code (`Preview`, `PreviewInset`, `ImageStretch`) left over from Slice 7.

*From review:* 2 passes, 15 findings applied or answered. Renders of `Book sample` at margins 5 and 0, Below and Above, checked by eye.

#### For CLAUDE.md

- Page numbers are computed only in Core `PageNumbering` from the padded page list; the renderer never derives numbers itself. (Slice 9: docs/plan/slices/09-page-numbers.md)

#### For README.md

- Optional page numbers (Off / Below / Above) under More options.

#### Not yet verified

- A real printed page with numbers (print to PDF or paper).
