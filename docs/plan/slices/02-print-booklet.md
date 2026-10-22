### Slice 02 — Print the booklet duplex

*In progress (wave 2026-10-22), draft PR.*

**Touches:** `src/PrintTheDamnBook.App/Printing/**`, `src/PrintTheDamnBook.App/ViewModels/MainViewModel.cs`,
`src/PrintTheDamnBook.App/MainWindow.xaml`, `src/PrintTheDamnBook.App/MainWindow.xaml.cs`,
`src/PrintTheDamnBook.Core/Layout/**`, `tests/PrintTheDamnBook.Core.Tests/Layout*`

**Why.** The point of the app: one click sends the sheets to the printer with duplex and
orientation already set, nothing to pick in a driver dialog.

**Decisions.**

- Printing uses `System.Printing`: enumerate `PrintQueue`s for a picker (default printer
  preselected), build a `PrintTicket` with `PageOrientation.Landscape`,
  `Duplexing.TwoSidedShortEdge` (short-edge flip keeps both faces upright on a landscape
  sheet) and the chosen `PageMediaSize` (A4 default, Letter selectable). Pages are drawn into a
  `FixedDocument` (one `FixedPage` per face, two `Image`s each) and written with
  `XpsDocumentWriter`. Exact API shapes confirmed via `library-docs` (context7) before coding.
- Layout maths in Core: `HalfSheetLayout.Fit(imageSize, halfSheetSize, margin)` returns the
  rectangle for a page scaled to fit inside its half with aspect kept and centred. Margin
  default 5 mm. Unit-tested with the 896x1200 sample size on A4 and Letter.
- Faces print in order sheet 1 front, sheet 1 back, sheet 2 front, ... as one duplex job, so
  the printer pairs them.
- "Print test sheet" prints sheet 1 only, same ticket, so the user can confirm the flip edge
  on their driver once without wasting the whole stack.
- "Print cover sheet as a separate job" checkbox (default off): sheet 1 goes as its own job,
  then the rest, so card stock can be loaded in between. The app shows a message between the
  two jobs ("Load the cover paper, then continue").

**Work.**

- [ ] Core `HalfSheetLayout` with tests (fit, centring, margins, both paper sizes).
- [ ] `PrinterService`: list queues, build ticket, render faces to a `FixedDocument`, send.
- [ ] Printer picker and paper size (A4 / Letter) in the main window; Print, Print test sheet.
- [ ] Separate cover job checkbox and the continue prompt.
- [ ] Error reporting: printer offline, ticket not supporting duplex (show a clear message
      naming the printer and the option, do not silently print simplex).

**Verification.**

- `dotnet test` green.
- Hands-on on the user's duplex printer with the sample: sheet 1 front shows back cover on the
  left and cover on the right, back shows 1 left and 10 right, both faces upright; folding the
  three sheets reads cover, 1, 2, ..., 10, back cover. Recorded in this file when confirmed.
