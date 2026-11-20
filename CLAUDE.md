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
dotnet run --project src/PrintTheDamnBook.Cli -- kdp <book.dino> --language en --out <folder>   # KDP interior.pdf + check-report.md (Windows or Linux)
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
- `src/PrintTheDamnBook.Core/Kdp/` — KDP layout maths (trim, bleed, page plan, crop).
- `src/PrintTheDamnBook.Kdp/` — KDP PDF drawing and image decoding (`KdpImageDecoder`), cross-platform.
- `src/PrintTheDamnBook.Cli/` — command-line `kdp` command; only parses arguments and calls Kdp.
- `.claude/skills/kdp-build/` and `.claude/agents/kdp-visual-check.md` — the repo's own KDP build skill (runs the `kdp` command with `--previews`, reruns with `--offset`) and its read-only, Sonnet-pinned visual-check agent (reads the contact sheet and small page PNGs, never full-size pictures). (Slice 30: docs/plan/slices/30-kdp-build-skill.md)
- `kdp/listings/<book>/<lang>.md` — KDP listing text per book; `.claude/skills/kdp-listing/` writes and checks it (`check-listing.mjs`).
- `tests/PrintTheDamnBook.Kdp.Tests/` — xUnit v3 for Kdp (runs on Linux CI).
- `tests/PrintTheDamnBook.Core.Tests/` — xUnit v3, covers everything in Core.
- `tests/PrintTheDamnBook.App.Tests/` — xUnit v3, net10.0-windows, App view-model tests (`InternalsVisibleTo`).
- `tools/kdp-robot/` — Playwright robot (plain ESM `.mjs`, `node --test`) that saves a book as a KDP draft; `kdp/catalog.json` is the per-book catalog it reads, `kdp/state/` what KDP held after each run.
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
- Desktop checks: device-test agents run `tools/desktop-vm/Invoke-DesktopTest.ps1` first and add a missing check as a new script in `tools/desktop-test/`, never a throw-away. The VM is the shared `rtst-desktop-test` (lock `Global\rtst-desktop-test-vm`), never a new one; details in those folders' `CLAUDE.md`. (Slice 21: docs/plan/slices/21-desktop-test-harness.md)
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
- Fit-to-page moves (text that doesn't fit goes to its own page) are decided only in Core `StoryPlacement`; a page the user placed by hand (`ChosenByUser`, legacy Own page counts) is never moved. (Slice 23: docs/plan/slices/23-fit-and-back-cover.md)
- A Dino back cover's credit lines come only from Core `BackCoverWording`; names and labels are never built in the App. (Slice 23: docs/plan/slices/23-fit-and-back-cover.md)
- Store failure kinds are typed (`DinoDiskException`, `DinoNetworkException`); never classify a failure from a bare `IOException` or message text. (Slice 20: docs/plan/slices/20-dino-open-hardening.md)
- A Dino page counts as user-edited only when its `StoryLines.Clean` text matches the book's source in none of its languages; never compare raw strings. (Slice 25: docs/plan/slices/25-dino-language-switch-pages.md)
- The KDP cut-line flag and crop offset maths live in Core `Kdp/` (`CutLineCheck`, `KdpPictureCrop`, `KdpBookSettings`); the Kdp project only decodes and hands over pixels. (Slice 28: docs/plan/slices/28-kdp-print-ready-pictures.md)
- `kdp.json` in the output folder is the owner's per-page offset record, keyed to the book id. It is written last (after the PDF, report and previews) and never overwritten when unreadable or from another book. (Slice 28: docs/plan/slices/28-kdp-print-ready-pictures.md)
- KDP layout maths (trim, bleed, page plan, crop) lives in Core `Kdp/`; drawing and image decoding only in `PrintTheDamnBook.Kdp` (`KdpImageDecoder`); the CLI `PrintTheDamnBook.Cli` only parses arguments and calls Kdp. (Slice 27: docs/plan/slices/27-kdp-interior-pdf.md)
- KDP cover size and the panel order and barcode side by reading direction live in Core `KdpCoverSize`; the Kdp project only draws it. A cover failure never stops `interior.pdf` and `check-report.md`, and an older `cover.pdf` is kept as `cover-previous.pdf`, never deleted. (Slice 29: docs/plan/slices/29-kdp-cover-pdf.md)
- Kdp fonts link the App's `Fonts/` files as embedded resources; never add a second copy. (Slice 27: docs/plan/slices/27-kdp-interior-pdf.md)
- The `kdp-listing` skill writes listings under `kdp/listings/<book>/<lang>.md`; `check-listing.mjs` must pass before a listing is pasted into KDP; every KDP limit cites an Amazon help page with its date. (Slice 31: docs/plan/slices/31-kdp-listing-pack.md)
- `kdp-build` skill and `kdp-visual-check` agent live in `.claude/`. Previews come only from the interior's own drawing (`KdpInterior.DrawingFor`), and a preview failure never stops the PDFs, report or `kdp.json`. (Slice 30: docs/plan/slices/30-kdp-build-skill.md)
- Every KDP picture's crop window is chosen by the `kdp-crop` skill (`kdp-crop-check` agent) from `--crop-sheet`'s small uncut copies before a book is cut; the choice is a pixel offset per key (story page number, `front`, `back`) in `kdp.json`, which gains a `covers` object (older files still read). Never open a full-size picture to judge a crop. The crop sheet is a side output like the previews and never stops the PDFs. (Slice 36: docs/plan/slices/36-kdp-crop-agent-and-cover.md)
- Cover words sit straight on the pictures (no white boxes): white letters with a faint dark shadow made of offset copies in `KdpCoverDocument` (QuestPDF has no text shadow), on a pale back picture ink with a white halo; the title goes in the calmest band (Core `KdpCalmBand`). The QR code is QRCoder's `PngByteQRCode` only (the `QRCode` renderer needs System.Drawing and breaks `kdp-linux`). Interior text pages are tinted from their picture (Core `TextPageTint`) and each own line stays on one line (`KdpTextFit.LinesStayWhole`). (Slice 36: docs/plan/slices/36-kdp-crop-agent-and-cover.md)
- Linux CI (`kdp-linux`) builds and tests the Core, Kdp, Cli and Kdp.Tests projects by path, never the `.slnx`, and runs only Kdp.Tests plus the Core `Kdp` test namespace. (Slice 27: docs/plan/slices/27-kdp-interior-pdf.md)
- KDP (kdp.amazon.com) is changed only through `tools/kdp-robot` (plain ESM `.mjs`, `node --test`, run by the `kdp-publish` skill). It never presses Publish (`src/guard.mjs` refuses it and allows only a short list of buttons), reads KDP's live page before changing anything, finds fields by label and role in one selector map per page (`src/pages/`), and stops on a missing field, sign-in or captcha. `kdp/catalog.json` holds only what no other file holds; `kdp/state/<book>/<lang>.json` records what KDP held after each run. The browser profile lives outside the repo. (Slice 38: docs/plan/slices/38-kdp-robot-draft.md)
