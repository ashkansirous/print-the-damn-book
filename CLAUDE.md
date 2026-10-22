# print-the-damn-book

Windows desktop app (C#, latest .NET, WPF) that imposes a folder of page images into a
saddle-stitch booklet and prints it duplex. Plan and status: `plan.md`; one file per slice in
`docs/plan/slices/`. Shared rules (trunk `main`, worktrees, review gate, agents) come from
`~/.claude/CLAUDE.md`.

## Commands

```powershell
dotnet build PrintTheDamnBook.slnx
dotnet test PrintTheDamnBook.slnx
dotnet format PrintTheDamnBook.slnx --verify-no-changes   # CI enforces; run before pushing
dotnet run --project src/PrintTheDamnBook.App
```

## Architecture map

- `src/PrintTheDamnBook.Core/` — net10.0, no WPF: `PageOrdering.cs`, `Imposition.cs`,
  `BookPage.cs`, `Sheet.cs`, `BookChecks.cs` (page order, imposition, warnings); `Layout/`
  (`HalfSheetLayout.cs`, `LayoutTypes.cs`: fit-in-half-sheet maths); `Ordering/` (`PageList.cs`,
  the page order).
- `src/PrintTheDamnBook.App/` — net10.0-windows WPF, MVVM (CommunityToolkit.Mvvm):
  `MainWindow.xaml` and `App.xaml` at the project root, `ViewModels/` (`MainViewModel`,
  `PageViewModel`), `Views/` (incl. `OptionsPanel`), `Services/` (`FolderImages`, `ThumbnailLoader`, `ImageDecoder`),
  `Printing/` (System.Printing, on `StaThread`), `Settings/` (`SettingsStore`).
- `tests/PrintTheDamnBook.Core.Tests/` — xUnit v3, covers everything in Core.
- `Book sample/` — the user's 12 sample pages, git-ignored; use it for hands-on checks.

## Rules

- **Design:** read every visual value (colors, sizes, spacing, copy) from `docs/design/` (see its README); never guess them and never call `DesignSync` for them.
- **All imposition and layout maths lives in Core and is unit-tested.** The App never decides
  which page goes on which face or where it sits; it renders `Sheet[]` from Core.
  (Slice 01: docs/plan/slices/01-folder-to-sheet-preview.md)
- Sheet `k` of `N` padded pages: front `left = p[N-1-2k]`, `right = p[2k]`; back
  `left = p[2k+1]`, `right = p[N-2-2k]`. Blanks go before the back cover. Changing this is a
  slice, not a tweak.
- Printing always sets orientation, duplex and paper size on the `PrintTicket` itself; never
  rely on the driver's defaults. (Slice 02: docs/plan/slices/02-print-booklet.md)
- Automatic printing needs a validated `TwoSidedShortEdge`. Manual sends two explicit `OneSided`
  jobs (fronts, reload, backs) and refuses a validated `TwoSided*`. Never silently print simplex.
  The two-pass flow lives in `Printing/ManualDuplexRunner.cs`; order and rotation maths in
  `Core/Layout/ManualDuplexPlan.cs`. (Slice 6: docs/plan/slices/06-manual-duplex.md)
- Settings live in `Core/Layout/PrintSettings` (JSON at `%LOCALAPPDATA%\PrintTheDamnBook\settings.json`). Only a user-chosen Two-sided mode is persisted, never a probe-forced one.
  (Slice 4: docs/plan/slices/04-options-and-settings.md)
- No Dockerfile, no container image: desktop app, no backend. Releases are a self-contained
  exe built by `release.yml` (Slice 05).
- Files are LF (`.gitattributes`, `.editorconfig`); don't convert line endings.
  (Slice 01: docs/plan/slices/01-folder-to-sheet-preview.md)
- Thumbnail decoding happens off the UI thread in `LoadFolderAsync` (`MainViewModel.cs`); keep
  it there. (Slice 01: docs/plan/slices/01-folder-to-sheet-preview.md)
- All printing runs on `Printing/StaThread`; create `PrintQueue`/`LocalPrintServer` on that
  thread only. (Slice 02: docs/plan/slices/02-print-booklet.md)
- Decode images only through `Services/ImageDecoder` (single exception filter, EXIF
  orientation). (Slice 02: docs/plan/slices/02-print-booklet.md)
- `PageList` (`src/PrintTheDamnBook.Core/Ordering/`) is the single source of page order; App
  items carry the `BookPage` identity, never positions. (Slice 03: docs/plan/slices/03-reorder-pages.md)
- The assembly and exe name is `PrintTheDamnBook`, not `PrintTheDamnBook.App`; use it in pack
  URIs and settings paths. (Slice 05: docs/plan/slices/05-release-pipeline.md)
- Releases are cut by pushing a `vX.Y.Z` tag on a `main` commit; `release.yml` does the rest.
  (Slice 05: docs/plan/slices/05-release-pipeline.md)
