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
  `PageViewModel`), `Themes/` (`Nocturne.xaml` tokens, `Controls.xaml`, `Icons.xaml`), `Views/` (wizard step views `OpenStepView`, `CheckStepView`, `PrintStepView`, `DoneStepView`; `NocturneDialog`), `Services/` (`FolderImages`, `ThumbnailLoader`, `ImageDecoder`, `FontLicences`),
  `Printing/` (System.Printing, on `StaThread`), `Settings/` (`SettingsStore`).
- `tests/PrintTheDamnBook.Core.Tests/` — xUnit v3, covers everything in Core.
- `tests/PrintTheDamnBook.App.Tests/` — xUnit v3, net10.0-windows, App view-model tests (`InternalsVisibleTo`).
- `tools/store-assets/` — generates the Store/MSIX images from `docs/design`.
- `site/` — Cloudflare Workers static landing site (`wrangler.jsonc`, `public/`).
- `Book sample/` — the user's 12 sample pages, git-ignored; use it for hands-on checks.

## Rules

- **Design:** read every visual value (colors, sizes, spacing, copy) from `docs/design/` (see its README); never guess them and never call `DesignSync` for them.
- **All imposition and layout maths lives in Core and is unit-tested.** The App never decides
  which page goes on which face or where it sits; it renders `Sheet[]` from Core.
  (Slice 01: docs/plan/slices/01-folder-to-sheet-preview.md)
- Sheet `k` of `N` padded pages, left-to-right: front `left = p[N-1-2k]`, `right = p[2k]`; back
  `left = p[2k+1]`, `right = p[N-2-2k]`. Right-to-left: front `left = p[2k]`, `right = p[N-1-2k]`;
  back `left = p[N-2-2k]`, `right = p[2k+1]`. Blanks go before the back cover in both. Changing
  this is a slice, not a tweak. (Slice 17: docs/plan/slices/17-right-to-left-books.md)
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
- UI tokens live in `src/PrintTheDamnBook.App/Themes/Nocturne.xaml` (from `docs/design/nocturne/styles.css`): use the brushes, never raw hex. Every focusable control uses the FocusRing style and has `AutomationProperties.Name`. Prompts go through `IPrintPrompts` / `NocturneDialog`, never `MessageBox`. (Slice 7: docs/plan/slices/07-wizard-redesign.md)
- Store and MSIX images are generated by `tools/store-assets` from `docs/design`. Re-run `npm run render` there after a design refresh; never hand-edit the PNGs in `assets/store/` or `src/PrintTheDamnBook.App/Assets/`. (Slice 8: docs/plan/slices/08-icon-and-store-assets.md)
- The site lives in `site/` (`wrangler.jsonc` and `public/`). `site/public/_headers` sets the CSP to `default-src 'none'`: add any new script, style, font, image or connect source there. Deploy from this PC with the logged-in wrangler (`cd site && npx --yes wrangler@4 deploy`) after any `site/` change; CI's deploy skips because no `CLOUDFLARE_API_TOKEN` secret exists (none was ever made, and none is needed). Auto mode denies this deploy from a session (Production Deploy), so the user runs it with `!` unless they add an allow rule; never list a missing token as a blocker. (Slice 10: docs/plan/slices/10-landing-site.md)
- Page numbers are computed only in Core `PageNumbering` from the padded page list; the renderer never derives numbers itself. (Slice 9: docs/plan/slices/09-page-numbers.md)
- Store releases come from stable `vX.Y.Z` tags only (MSIX version X.Y.Z.0; tags only ever increase). MSIX identity lives in repo variables `MSIX_*`, never in `packaging/msix/AppxManifest.xml`. (Slice 11: docs/plan/slices/11-microsoft-store.md)
- After UI changes, re-run `tools/store-assets` (render + `capture-app.ps1`) so Store screenshots match the app. (Slice 11: docs/plan/slices/11-microsoft-store.md)
- Story text layout (band rectangle, shrink steps, text-page box, spread-alignment spacers, tint) lives in `Core/Text` and `Core/Layout`; the App only draws it. (Slice 14: docs/plan/slices/14-typed-text-pages.md)
- `book.json` is read and written only through `Services/BookTextStore`; never overwrite a file it could not read. (Slice 14: docs/plan/slices/14-typed-text-pages.md)
- Andika is the story font; Vazirmatn joins it for Arabic-script text. Both licences are listed in `Services/FontLicences`. (Slice 14: docs/plan/slices/14-typed-text-pages.md; Slice 17: docs/plan/slices/17-right-to-left-books.md)
- Reading direction and page-number digits are decided only in Core (`TextDirection`, `StoryFont`, `BookDirectionChoice`, `PageNumbering`); the App never derives them. (Slice 17: docs/plan/slices/17-right-to-left-books.md)
- Launch arguments are parsed only by Core `HandoverArguments`. (Slice 19: docs/plan/slices/19-open-from-dino-reader.md)
- Open-with registration writes only the three HKCU keys via Core `OpenWithRegistrar` (plus the Applications `shell\open\command`), never `UserChoice`. (Slice 19: docs/plan/slices/19-open-from-dino-reader.md)
- The Dino cache (`Services/DinoBookCache.cs`) is content-addressed and shared by windows: extract to a temp folder and rename under the `Local\` mutex; sweep only when no other instance runs. (Slice 19: docs/plan/slices/19-open-from-dino-reader.md)
- Band rectangles, `BandSuggestion` scoring and panel opacity live in Core (`Text/`, `Layout/`); the App only decodes via `Services/ImageDecoder` and hands over pixels off the UI thread. A user's band pick is never re-suggested over. (Slice 16: docs/plan/slices/16-text-band-suggestion.md)
- .dino files are untrusted: parse only through `Core/Dino` (`DinoBookReader`) with its caps and name checks; extracted books live in the per-hash cache (`DinoBookCache`), never next to the .dino. (Slice 15: docs/plan/slices/15-open-dino-books.md)
- Store network access goes only through `Services/DinoStoreClient` with the `DinoStoreCatalog` allow-list: plain GETs, no identifiers. Downloaded files go through `DinoBookReader` like any .dino. The site's privacy text (`site/public/privacy.html`) and the dialog footer must stay in step. (Slice 18: docs/plan/slices/18-dino-store.md)
- Store failure kinds are typed (`DinoDiskException`, `DinoNetworkException`); never classify a failure from a bare `IOException` or message text. (Slice 20: docs/plan/slices/20-dino-open-hardening.md)
