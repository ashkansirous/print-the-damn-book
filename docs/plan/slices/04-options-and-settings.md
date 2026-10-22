### Slice 04 — Options: fill or fit, flip edge, remembered printer

*In progress (wave 2026-10-22), draft PR.*

**Touches:** `src/PrintTheDamnBook.App/Settings/**`, `src/PrintTheDamnBook.App/Views/OptionsPanel.xaml`,
`src/PrintTheDamnBook.App/Views/OptionsPanel.xaml.cs`, `src/PrintTheDamnBook.App/Printing/**`,
`src/PrintTheDamnBook.Core/Layout/**`, `tests/PrintTheDamnBook.Core.Tests/Layout*`,
`src/PrintTheDamnBook.App/MainWindow.xaml`, `src/PrintTheDamnBook.App/MainWindow.xaml.cs`,
`src/PrintTheDamnBook.App/ViewModels/MainViewModel.cs`, `src/PrintTheDamnBook.App/Views/PageResources.xaml`

**Why.** Two drivers interpret the duplex flip differently, and some books look better edge
to edge. These are the knobs the user will reach for after the first print, and they should
stay set between runs.

**Decisions.**

- Options panel: scaling (Fit, default / Fill and crop), flip edge (Short edge, default / Long
  edge), margin in mm, paper size, printer. Persisted as JSON under
  `%LOCALAPPDATA%\PrintTheDamnBook\settings.json`; missing or corrupt file falls back to
  defaults without crashing.
- Fill mode: `HalfSheetLayout.Fill` scales the page to cover its half and crops equally on the
  overflowing axis; the preview shows the crop.
- Long-edge flip: the ticket uses `Duplexing.TwoSidedLongEdge` and every back face is
  rotated 180 degrees when rendered, so left/right and up/down still come out right. The
  page assignment from Core never changes; only the rendering does. Unit test the rotation
  decision; verify on paper with the test sheet.

- Added 2026-10-22 (after Slice 6): the settings also remember Slice 6's Two-sided choice
  (Automatic/Manual), *Reverse order of backs* and *Rotate backs 180°*, per printer name. Long-edge flip
  applies to Automatic only; in Manual, the rotate option already covers it.

**Work.**

- [ ] Core `HalfSheetLayout.Fill` and tests.
- [ ] Settings model, load/save, defaults, used by the printer service and preview.
- [ ] Options panel bound to the settings; test sheet respects every option.

**Verification.**

- `dotnet test` green.
- Manual: switch to Fill, preview shows thin top/bottom crop on the sample pages; switch flip
  to Long edge and print the test sheet, both faces come out upright.
