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

| Slice | Title | Status | PR | Issue | File |
| --- | --- | --- | --- | --- | --- |
| 1 | Open a folder and preview the imposed sheets | built | #2 | | [01](docs/plan/slices/01-folder-to-sheet-preview.md) |
| 2 | Print the booklet duplex | built | #3 | | [02](docs/plan/slices/02-print-booklet.md) |
| 3 | Reorder, add and remove pages | built | #4 | | [03](docs/plan/slices/03-reorder-pages.md) |
| 4 | Options: fill or fit, flip edge, remembered printer | planned | | | [04](docs/plan/slices/04-options-and-settings.md) |
| 5 | Release build on GitHub | built | #5 | | [05](docs/plan/slices/05-release-pipeline.md) |
| 6 | Manual two-sided printing (before Slice 4) | built | #8 | | [06](docs/plan/slices/06-manual-duplex.md) |

Notes: _(none yet; `docs/plan/notes/YYYY-MM-DD-<slug>.md`, one line each)_

## Outstanding work

- Slice 2: a real duplex print and fold; an EXIF-rotated JPEG checked by eye; busy-disable of the buttons seen in the UI ([slice 02](docs/plan/slices/02-print-booklet.md)).
- Slice 2: the user's Brother HL-L3220CW has no duplex; resolved by Slice 6 (manual two-sided), see the next item ([slice 06](docs/plan/slices/06-manual-duplex.md)).
- Slice 6: test sheet and a full booklet on the user's Brother HL-L3220CW, then fold (needs paper; ask the user) ([slice 06](docs/plan/slices/06-manual-duplex.md)).
- Slice 3: Remove buttons are absent from the UI Automation tree (needs `AutomationProperties`); decide whether Cover and Back cover should be removable ([slice 03](docs/plan/slices/03-reorder-pages.md)).
- Slice 5: `release.yml` has never run on GitHub (needs a first tag, e.g. `v0.1.0`); app icon is missing ([slice 05](docs/plan/slices/05-release-pipeline.md)).
- Slice 1: visual check by eye of thumbnail aspect ratio and scrolling on the sample folder ([slice 01](docs/plan/slices/01-folder-to-sheet-preview.md)).

## Out of Scope

- PDF or WebP input (images only; PDF is the first thing to add if a generator produces it).
- Multiple signatures or perfect binding: one folded stack per book.
- Saving a book project file: reopen the folder instead.
- Trim marks, bleed, crop marks: pages are fit inside the half-sheet, nothing is cut.
- Printing through another app's dialog, or a Dockerfile / GHCR image (desktop app, no backend).
- macOS or Linux builds.
- Right-to-left books (the imposition would mirror left and right; not needed for the sample).
