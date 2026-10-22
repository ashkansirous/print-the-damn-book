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

Slices 7, 8 and 10 merged 2026-10-22. Next: 9 (needs 7's Print step) and 11 (needs 8's assets and 10's privacy page). 12 and 13 are parked.

The repo is public (2026-10-22), so the site and the app download from public GitHub releases. The landing site is live at https://print-the-damn-book.sirous.uk (deployed 2026-10-22 via local wrangler, all pages 200).

| Slice | Title | Status | PR | Issue | File |
| --- | --- | --- | --- | --- | --- |
| 1 | Open a folder and preview the imposed sheets | built | #2 | | [01](docs/plan/slices/01-folder-to-sheet-preview.md) |
| 2 | Print the booklet duplex | built | #3 | | [02](docs/plan/slices/02-print-booklet.md) |
| 3 | Reorder, add and remove pages | built | #4 | | [03](docs/plan/slices/03-reorder-pages.md) |
| 4 | Options: fill or fit, flip edge, remembered printer | built | #10 | | [04](docs/plan/slices/04-options-and-settings.md) |
| 5 | Release build on GitHub | built | #5 | | [05](docs/plan/slices/05-release-pipeline.md) |
| 6 | Manual two-sided printing (before Slice 4) | built | #8 | | [06](docs/plan/slices/06-manual-duplex.md) |
| 7 | Redesign: Open → Check → Print wizard (Nocturne) | built | #15 | | [07](docs/plan/slices/07-wizard-redesign.md) |
| 8 | App icon and Store Kit assets | built | #16 | | [08](docs/plan/slices/08-icon-and-store-assets.md) |
| 9 | Optional page numbers, below or above (after 7) | planned | | | [09](docs/plan/slices/09-page-numbers.md) |
| 10 | Landing site at print-the-damn-book.sirous.uk | built | #17 | | [10](docs/plan/slices/10-landing-site.md) |
| 11 | Microsoft Store: MSIX, listing, submission (after 8, 10) | planned | | | [11](docs/plan/slices/11-microsoft-store.md) |
| 12 | Android app, .NET MAUI (parked) | parked | | | [12](docs/plan/slices/12-android-app.md) |
| 13 | iOS app, .NET MAUI (parked) | parked | | | [13](docs/plan/slices/13-ios-app.md) |

Notes: _(none yet; `docs/plan/notes/YYYY-MM-DD-<slug>.md`, one line each)_

## Outstanding work

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
- Saving a book project file: reopen the folder instead.
- Trim marks, bleed, crop marks: pages are fit inside the half-sheet, nothing is cut.
- Printing through another app's dialog, or a Dockerfile / GHCR image (desktop app, no backend).
- macOS or Linux builds. (Android and iOS are parked slices 12–13, not out of scope.)
- A light theme for the redesign (Nocturne is dark only for now).
- Right-to-left books (the imposition would mirror left and right; not needed for the sample).
