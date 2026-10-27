# Plan: print-the-damn-book

A Windows desktop app that turns a folder of page images (`Cover`, `1`..`N`, `Back cover`)
into a saddle-stitch booklet and sends it to a duplex printer in the right order, two pages
per landscape sheet, so the printed stack only needs folding in the middle.

The one rule: **the app, not the user, decides which page goes where.** All imposition maths
lives in the Core library and is unit-tested; the UI only shows and prints what Core computed.

Decisions locked on 2026-10-22 (RefineScope): C# on the latest GA .NET (10, LTS; .NET 11 is
RC2 until its November 2026 GA, when `global.json` rolls forward) with WPF, single
self-contained exe; A4 landscape default with Letter selectable; fit each page in its half of
the sheet, aspect kept; single folded stack with a warning above 6 sheets; JPG and PNG input
only; folder load with natural sort and drag reorder, no project file; blanks inserted before
the back cover; direct printing with app-set duplex (short edge) and a test-sheet button;
optional separate job for the cover sheet; sheet preview before printing; GitHub Actions
builds the exe on release, no Dockerfile.

## Status

Slices 7 to 11 merged 2026-10-22. Slices 14-16 merged 2026-10-26/27 (wave done). The Microsoft Store submission was dropped by the user on 2026-10-22: the MSIX tooling from Slice 11 stays in place but dormant (it skips without the `MSIX_*` variables). 12 and 13 are parked.

The repo is public (2026-10-22), so the site and the app download from public GitHub releases. The landing site is live at https://print-the-damn-book.sirous.uk (deployed 2026-10-22 via local wrangler, all pages 200).

| Slice | Title | Status | PR | Issue | File |
| --- | --- | --- | --- | --- | --- |
| 12 | Android app, .NET MAUI (parked) | parked | | | [12](docs/plan/slices/12-android-app.md) |
| 13 | iOS app, .NET MAUI (parked) | parked | | | [13](docs/plan/slices/13-ios-app.md) |

Notes:

- [slices-done.md](docs/plan/notes/slices-done.md): finished slice rows (1 to 11, 14 to 16).

## Outstanding work

- Slice 15: memory not returned after a very large book (about 1.6 GB held after a 420-page stress book is replaced, also on main for folder opens; needs a profiler); about 3 in 18 cancelled opens leave a cache folder behind (cleaned on reopen); drop-zone drag checked with a real Explorer drag only once ([slice 15](docs/plan/slices/15-open-dino-books.md)).
- Slice 16: pre-print status and Cancel on a large folder (wait over 500 ms); suggestion quality on real noisy photos, thresholds tuned only on synthetic images ([slice 16](docs/plan/slices/16-text-band-suggestion.md)).
- Slice 14: a real paper print and fold; performance with many story pages (re-layout on the UI thread on each typing pause) ([slice 14](docs/plan/slices/14-typed-text-pages.md)).
- Letter paper verified 2026-10-27 for Slices 14-16 (Print to PDF: page size, bands within 30%, own page, shrink, Dino cover title). Seen then: three-little-pigs.dino warns "5 pages have more words than fit" on Letter (Dino pages default to the bottom band, so long text is cut at 14 pt; consider Own page or a smaller minimum for Dino books); "Print test sheet" on a Dino book printed page 1, not the cover (not investigated).
- Slice 9: a real printed page with numbers (print to PDF or paper) ([slice 09](docs/plan/slices/09-page-numbers.md)).
- Slice 7 code follow-ups from Slice 11 review: `SnapLayoutHook` hover state not cleared without non-client mouse tracking; `NocturneDialog` CoverOwner sizing / null owner; `ClearStatusUnlessBusy` doesn't reset `PrintStatusKind`; `TestSheetSent` mid-send check only covers RotateBacks ([slice 11](docs/plan/slices/11-microsoft-store.md)).
- Site CI deploy: the user creates a Cloudflare API token (Workers edit) in the dashboard and sets it as the `CLOUDFLARE_API_TOKEN` repo secret ([slice 10](docs/plan/slices/10-landing-site.md)).
- First release tag `v0.1.0` is not yet cut; it unblocks Slice 5's `release.yml` and the site download link ([slice 05](docs/plan/slices/05-release-pipeline.md)).
- Slice 7: focus ring on the A4/Letter radios and switches, done-step footer wrap, the stuck drag tooltip (not reproduced), snap layout on a high-DPI monitor, all seen on screen ([slice 07](docs/plan/slices/07-wizard-redesign.md)).
- Slice 8: the icon by eye in Explorer, title bar and taskbar, light and dark. The screenshot re-shoot from the real app is carried by Slice 11 ([slice 08](docs/plan/slices/08-icon-and-store-assets.md)).
- Slice 10: set the `CLOUDFLARE_API_TOKEN` repo secret (Workers-edit token from the Cloudflare dashboard); until then the CI deploy skips. First deploy and custom domain are live (done 2026-10-22). Still open: Lighthouse run, download link once the first release exists ([slice 10](docs/plan/slices/10-landing-site.md)).
- Slice 2: a real duplex print and fold; an EXIF-rotated JPEG checked by eye; busy-disable of the buttons seen in the UI ([slice 02](docs/plan/slices/02-print-booklet.md)).
- Slice 2: the user's Brother HL-L3220CW has no duplex; resolved by Slice 6 (manual two-sided), see the next item ([slice 06](docs/plan/slices/06-manual-duplex.md)).
- Slice 6: test sheet and a full booklet on the user's Brother HL-L3220CW, then fold (needs paper; ask the user) ([slice 06](docs/plan/slices/06-manual-duplex.md)).
- Slice 3: Remove buttons are absent from the UI Automation tree (needs `AutomationProperties`); decide whether Cover and Back cover should be removable ([slice 03](docs/plan/slices/03-reorder-pages.md)).
- Slice 5: `release.yml` has never run on GitHub (needs a first tag, e.g. `v0.1.0`); app icon is missing ([slice 05](docs/plan/slices/05-release-pipeline.md)).
- Slice 4: long-edge flip test sheet on a real duplex printer (none available); an explicit Manual choice saved on a duplex-capable printer ([slice 04](docs/plan/slices/04-options-and-settings.md)).
- Slice 1: visual check by eye of thumbnail aspect ratio and scrolling on the sample folder ([slice 01](docs/plan/slices/01-folder-to-sheet-preview.md)).

## Out of Scope

- PDF or WebP input (images only; PDF is the first thing to add if a generator produces it).
- Multiple signatures or perfect binding: one folded stack per book.
- Saving a book project file: reopen the folder instead. (Exception from Slice 14: story text is saved in `book.json` in the folder; page order is not.)
- Trim marks, bleed, crop marks: pages are fit inside the half-sheet, nothing is cut.
- Printing through another app's dialog, or a Dockerfile / GHCR image (desktop app, no backend).
- macOS or Linux builds. (Android and iOS are parked slices 12–13, not out of scope.)
- A light theme for the redesign (Nocturne is dark only for now).
- Right-to-left books (the imposition would mirror left and right; not needed for the sample). Farsi Dino books stay out too (Slice 15).
