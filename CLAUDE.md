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

- `src/PrintTheDamnBook.Core/` — net10.0, no WPF, flat files: `PageOrdering.cs`, `Imposition.cs`,
  `BookPage.cs`, `Sheet.cs`, `BookChecks.cs` (page order, imposition, warnings).
- `src/PrintTheDamnBook.App/` — net10.0-windows WPF, MVVM (CommunityToolkit.Mvvm):
  `MainWindow.xaml` and `App.xaml` at the project root, `ViewModels/` (`MainViewModel`,
  `PageViewModel`), `Services/` (`FolderImages`, `ThumbnailLoader`). Planned: `Printing/`
  (System.Printing, Slice 02) and `Settings/` (Slice 04).
- `tests/PrintTheDamnBook.Core.Tests/` — xUnit v3, covers everything in Core.
- `Book sample/` — the user's 12 sample pages, git-ignored; use it for hands-on checks.

## Rules

- **All imposition and layout maths lives in Core and is unit-tested.** The App never decides
  which page goes on which face or where it sits; it renders `Sheet[]` from Core.
  (Slice 01: docs/plan/slices/01-folder-to-sheet-preview.md)
- Sheet `k` of `N` padded pages: front `left = p[N-1-2k]`, `right = p[2k]`; back
  `left = p[2k+1]`, `right = p[N-2-2k]`. Blanks go before the back cover. Changing this is a
  slice, not a tweak.
- Printing always sets orientation, duplex and paper size on the `PrintTicket` itself; never
  rely on the driver's defaults. If the printer rejects duplex, tell the user, don't print
  simplex. (Slice 02: docs/plan/slices/02-print-booklet.md)
- No Dockerfile, no container image: desktop app, no backend. Releases are a self-contained
  exe built by `release.yml` (Slice 05).
- Files are LF (`.gitattributes`, `.editorconfig`); don't convert line endings.
  (Slice 01: docs/plan/slices/01-folder-to-sheet-preview.md)
- Thumbnail decoding happens off the UI thread in `LoadFolderAsync` (`MainViewModel.cs`); keep
  it there. (Slice 01: docs/plan/slices/01-folder-to-sheet-preview.md)
