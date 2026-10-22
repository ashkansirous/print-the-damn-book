### Slice 04 — Options: fill or fit, flip edge, remembered printer

*Built 2026-10-22, merged in PR #10. Desktop-tested with the PDF printer only; real duplex printer not yet tested.*

**Touches:** `src/PrintTheDamnBook.App/Settings/**`, `src/PrintTheDamnBook.App/Views/OptionsPanel.xaml`,
`src/PrintTheDamnBook.App/Views/OptionsPanel.xaml.cs`, `src/PrintTheDamnBook.App/Views/PageResources.xaml`,
`src/PrintTheDamnBook.App/Printing/**`, `src/PrintTheDamnBook.App/MainWindow.xaml`,
`src/PrintTheDamnBook.App/MainWindow.xaml.cs`, `src/PrintTheDamnBook.Core/Layout/**`,
`tests/PrintTheDamnBook.Core.Tests/Layout*`

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

- [x] Core `HalfSheetLayout.Fill` and tests.
- [x] Settings model, load/save, defaults, used by the printer service and preview.
- [x] Options panel bound to the settings; test sheet respects every option.

**Verification.**

- `dotnet test` green.
- Manual: switch to Fill, preview shows thin top/bottom crop on the sample pages; switch flip
  to Long edge and print the test sheet, both faces come out upright.

*Changed while building:* the settings model lives in Core (`Core/Layout/PrintSettings`, `PrintSettingsJson`) so it is testable; `HalfSheetLayout` gained `Fill`/`Resolve`/`Preview`/`TryParseMargin` (accepts `.` or `,`), plus `LayoutOptions` and `AutomaticDuplexPlan` (long edge rotates backs 180 degrees). App side: `Settings/SettingsStore`, `Views/OptionsPanel`, debounced serialised saves with a `SettingsStatus` line, flush on close. Per-printer Two-sided/Reverse/Rotate; `SeparateCoverJob` is global; the last printer is kept even when not installed. Flip edge is shown only in Automatic. A probe-forced Manual is never saved, and switching to Automatic re-probes. Missing or corrupt JSON gives defaults; saves are atomic. 138 tests.

*From review:* two passes, 17 findings applied.

*Desktop test 2026-10-22 (PDF printer only):* options panel, Fill fills the halves in the PDF, settings persist across restart, corrupt JSON falls back to defaults, PDF printer goes to Manual with a reason. Margin "3,5" bug found and fixed.

#### For CLAUDE.md (lifted 2026-10-22)

- Settings live in `Core/Layout/PrintSettings` (JSON at `%LOCALAPPDATA%\PrintTheDamnBook\settings.json`). Only a user-chosen Two-sided mode is persisted, never a probe-forced one. (Slice 4: docs/plan/slices/04-options-and-settings.md)

#### For README.md (lifted 2026-10-22)

- Options (scaling, flip edge, margin, paper, printer, two-sided) are remembered between runs.

#### Not yet verified

- Long-edge flip test sheet on a real duplex printer (none available).
- Explicit Manual choice saved on a duplex-capable printer.
