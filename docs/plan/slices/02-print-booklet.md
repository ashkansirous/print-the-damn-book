### Slice 02 — Print the booklet duplex

*Built 2026-10-22 (awaiting merge); real duplex print not yet verified.* 49 tests green. Desktop test 2026-10-22 done on the XPS harness, not on a duplex printer.

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

- [x] Core `HalfSheetLayout` with tests (fit, centring, margins, both paper sizes).
- [x] `PrinterService`: list queues, build ticket, render faces to a `FixedDocument`, send.
- [x] Printer picker and paper size (A4 / Letter) in the main window; Print, Print test sheet.
- [x] Separate cover job checkbox and the continue prompt.
- [x] Error reporting: printer offline, ticket not supporting duplex (show a clear message
      naming the printer and the option, do not silently print simplex).

**Verification.**

- `dotnet test` green.
- Hands-on on the user's duplex printer with the sample: sheet 1 front shows back cover on the
  left and cover on the right, back shows 1 left and 10 right, both faces upright; folding the
  three sheets reads cover, 1, 2, ..., 10, back cover. Recorded in this file when confirmed.

*Changed while building:* Core `Layout/` has `HalfSheetLayout.Fit/Place`, 5 mm margin, `PaperKind` A4/Letter and `Paper.MatchesSize` (+-2 mm). App `Printing/`: `PrinterService` (async queue list, fresh `PrintTicket` per job with Landscape + TwoSidedShortEdge + paper, validated against queue capabilities; non-duplex printers are refused with a message naming them, nothing printed), `FaceRenderer` (`FixedDocument`, one page per face at 300 dpi), `StaThread` (render and spool off the UI thread), `PrintViewModel` (pickers, separate cover job with "load cover paper" prompt, Print and Print test sheet, `IsBusy` disables buttons; job named after folder plus part suffix). `Services/ImageDecoder` is shared by thumbnails and print and honours EXIF orientation.

*From review:* 2 passes, 18 findings applied.

*Desktop test 2026-10-22:* printer list matches `Get-Printer`; Microsoft Print to PDF refused with the named message and 0 jobs; XPS harness gave 6 landscape A4 pages (1122.5x793.7 DIP) with faces Back cover|Cover, 1|10, 9|2, 3|8, 7|4, 5|6.

*Finding / open question:* the user's Brother HL-L3220CW (IPP class driver) reports no duplex capability, so the app refuses it; only the virtual HP Smart Printing reports short-edge duplex. Needs a user decision: install the Brother full driver, or add a manual-duplex fallback.

#### For CLAUDE.md

- All printing runs on `Printing/StaThread`; create `PrintQueue`/`LocalPrintServer` on that thread only. (Slice 02: docs/plan/slices/02-print-booklet.md)
- Decode images only through `Services/ImageDecoder` (single exception filter, EXIF orientation). (Slice 02: docs/plan/slices/02-print-booklet.md)

#### Not yet verified

- A real duplex print and fold: no duplex-capable physical printer is reported.
- An EXIF-rotated JPEG, checked by eye.
- Busy-disable of the buttons seen in the UI.
