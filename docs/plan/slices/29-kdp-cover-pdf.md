### Slice 29 — Amazon KDP: cover PDF (back, spine, front in one wrap)

*Planned 2026-11-19 from the Scope Inbox doc
[2026-11-18-amazon-kdp-export.md](https://drive.google.com/file/d/16B6KoIGXtHRXaRS8VvQcopP5RyJhcUIM/view?usp=drivesdk)
(milestone 3).*

*Built 2026-11-19, merged as PR #76 (a3f59c1); not yet verified on KDP (upload and proof), see Not yet verified.*

**Touches:** `src/PrintTheDamnBook.Core/Kdp/KdpCoverSize.cs` (new), `src/PrintTheDamnBook.Kdp/Cover/**` (new),
`src/PrintTheDamnBook.Kdp/KdpExporter.cs`, `src/PrintTheDamnBook.Kdp/KdpCheckReport.cs`, `src/PrintTheDamnBook.Kdp/KdpFonts.cs`,
`src/PrintTheDamnBook.Cli/**` (new, from an earlier KDP slice), `tests/PrintTheDamnBook.Core.Tests/Kdp/**` (new, from an earlier KDP slice), `tests/PrintTheDamnBook.Kdp.Tests/**` (new, from an earlier KDP slice)

**Depends on:** Slice 27 (page count from `KdpPagePlan`). Can run alongside Slice 28 if their `src/PrintTheDamnBook.Cli/**`
edits are kept to separate files.

#### Why

KDP needs one cover PDF: back + spine + front, sized from its formula for the page count and paper, with bleed and a
clear barcode area (doc, milestone 3).

#### Decisions

- Approved (doc): title and credits from Core `BackCoverWording` (root rule: credit lines come only from it); barcode
  area left clear; acceptance is KDP's cover check passing.
- Decided by this planning session, 2026-11-19:
  - Core `KdpCoverSize` holds KDP's cover formula (spine width per page for premium colour paper, bleed, barcode box size
    and place) as constants, each with the KDP help page it came from. The builder reads the current values through
    `web-research` from KDP's cover help pages and cover calculator before coding, and unit tests the result against the
    calculator for 24, 32 and 40 pages.
  - No spine text: 24–40 pages is well under KDP's minimum for spine text (the builder records the exact minimum).
  - Front: the `.dino` cover picture, cropped and resampled by the same Kdp decoder path as Slice 27/28, with the title.
    Back: the Dino back-cover picture (`DinoBackCover.PictureFile`) with the `BackCoverWording` lines, plus a short line
    pointing to the Dino app; barcode box bottom right left empty.
- Decided by the night run, wave 2 (2026-11-19), because Slice 34 (the back-cover reader reads camelCase keys only) is
  parked and real store books write PascalCase: when the book's `BackCover` is null, the back half is drawn plain (the
  title only, no picture, no credit lines), the barcode box is still left clear, and the check report says
  "back cover: no Dino back cover read (picture and credits missing; Slice 34)". `cover.pdf` is still written. The cover
  code never reads `story.json` itself (root rule: .dino files are parsed only through `Core/Dino`). Once Slice 34 merges,
  real books get the picture and credits with no change here.

#### Work

- [x] `KdpCoverSize` with tests (against the published formula by hand; calculator values were not obtained, see Verification).
- [x] `KdpCoverDocument` (QuestPDF), fonts embedded; `cover.pdf` written by the same `kdp` command.
- [x] Check report gains the cover size, spine width and barcode box (a `## Cover` section); the CLI prints a cover line.

#### Verification

- Unit tests green on Windows and Linux CI; Freddy's `cover.pdf` page size matches the calculator for its page count.
- Calculator values were not obtained (2026-11-19): KDP's cover calculator page shows no 8.5 x 8.5 premium-colour entry and no table row exists for 24, 32 or 40 pages. Tried: web search, then reads of G201953020, G201857950, GVBQ3CMEQW3W2VL6 and the calculator page. The tests therefore check the code against KDP's published formula (premium colour 0.002347 in per page, bleed 0.125 in), worked by hand; the `[owner]` upload below is the real check of the size.
- `[owner]` Upload Freddy's `cover.pdf` with the interior at https://kdp.amazon.com/en_US/bookshelf, pass KDP's cover check,
  and order one printed proof; warnings and the proof's look are written here.

*Changed while building:*

- Core `KdpCoverSize` holds the KDP formula constants (each with its help-page source, read 2026-11-19), the panel order and barcode corner by reading direction (right-to-left books mirror them), and `HasSpineText`. Kdp `Cover/KdpCover` and `KdpCoverDocument` draw it (QuestPDF; Andika/Vazirmatn via `StoryFont`; right-to-left text via `KdpTextFit`). The exporter writes `cover.pdf`.
- A cover failure never stops the interior: `interior.pdf` and `check-report.md` are still written, the CLI exits 1, an older `cover.pdf` is kept as `cover-previous.pdf` (never deleted), and `kdp.json` is still written last. `TryBuildCover` catches everything except fatal exceptions (isolation boundary).
- Tests: 765 green (Core 621, App 77, Kdp 67); build, format and line-endings checks exit 0.
- Visual check (host, pypdfium2 render at 900 px): gingerbread en, lion en, lion fa (layout only; no free book has Farsi text). Front/back sides, barcode corner and title are right, no white strips, sizes match the formula: pass. PNGs in `D:/print-the-damn-book-worktrees/reviews/29-visual/`.
- Observed: QuestPDF writes the page as 17.3333 in for a 17.3345 in cover (whole-point rounding, 0.0012 in); the lion back picture's head is cut at the bottom edge (content crop, not a layout fault).

#### From review

Findings files (outside the repo): `reviews/pr-76-high.md`, `pr-76-medium.md`, `pr-76-check-medium.md`, `pr-76-check2-medium.md`, `pr-76-check3-medium.md`. Skips are listed below; check pass 3 (`pr-76-check3-medium.md`) has seven findings, none crash or data loss (the old cover is kept as `cover-previous.pdf`), and all seven are follow-ups, listed last. The skip quotes cite review files in `D:/print-the-damn-book-worktrees/reviews`, so the skip check runs with `--root D:/print-the-damn-book-worktrees`.

- Skipped #4 (`reviews/pr-76-medium.md`): "WriteAll is not atomic across three files" - each file is written atomically and kdp.json stays last, so a crash between files leaves the previous run's file, which the next run replaces.
- Skipped #5 (`reviews/pr-76-medium.md`): "src/PrintTheDamnBook.Kdp/Cover/KdpCover.cs:18, nit. User-facing warning" - the wording is the slice file's own Decision text for the missing back cover.
- Skipped #6 (`reviews/pr-76-medium.md`): "Touches lists src/PrintTheDamnBook.Kdp/KdpFonts.cs, which the change does not modify" - an unchanged file in Touches only widens the claim, it changes no code.
- Skipped #4 (`reviews/pr-76-check-medium.md`): "The back-cover credits box is not checked for fit" - after the fix to #2 an overflow becomes a reported cover failure (interior and report still written), and two credit lines fit the box on every free store book.
- Skipped #3 (`reviews/pr-76-check2-medium.md`): "HasSpineText uses >= 79 while the comment cites another KDP page" - 24-40 page books are far from 79 either way, and the 79-vs-more-than-79 conflict is already recorded.
- Skipped #4 (`reviews/pr-76-check2-medium.md`): "Blank title is not guarded" - every .dino has a title; the reader requires it.
- Skipped #6 (`reviews/pr-76-check2-medium.md`): "No test for the StaleCover.Kept branch (read-only file)" and the other missing branches - tests beyond the two added for the first two findings are follow-up coverage, not a defect.
- Skipped #7 (`reviews/pr-76-check-medium.md`): "With no back picture, BackText draws the book title at the top of the back cover" - a title-only back is this slice's own Decision for a book with no back cover read.
- Skipped #1 (`reviews/pr-76-check3-medium.md`): "KeepOlderCover renames the old cover.pdf to cover-previous.pdf before the interior and report moves succeed" - follow-up; the old cover is kept, so nothing is lost.
- Skipped #2 (`reviews/pr-76-check3-medium.md`): "TryBuildCover reports only ex.Message, dropping type and stack" - follow-up; diagnostics only.
- Skipped #3 (`reviews/pr-76-check3-medium.md`): "Fatal filter lists StackOverflowException and AccessViolationException, which cannot be caught anyway" - follow-up; a nit with no behaviour change.
- Skipped #4 (`reviews/pr-76-check3-medium.md`): "cover-previous.pdf is never mentioned in the report" - follow-up; the older cover is kept, not lost.
- Skipped #5 (`reviews/pr-76-check3-medium.md`): "On cover failure the CLI prints" and returns Failed, so scripts discard a valid interior - follow-up; the interior and report are still written.
- Skipped #6 (`reviews/pr-76-check3-medium.md`): "WriteAll moves interior, cover, report one at a time" - follow-up; each file is written atomically and `kdp.json` stays last.
- Skipped #7 (`reviews/pr-76-check3-medium.md`): "A cover failure is not added to Warnings (only CoverError)" - follow-up; the failure shows in the CLI exit code and the report.

Follow-ups for check pass 3 findings 1-7 are planned as [Slice 35](35-kdp-followups.md) item C.

#### For CLAUDE.md (lifted 2026-11-19)

- KDP cover size and the panel order and barcode side by reading direction live in Core `KdpCoverSize`; the Kdp project only draws it. A cover failure never stops `interior.pdf` and `check-report.md`, and an older `cover.pdf` is kept as `cover-previous.pdf`, never deleted. (Slice 29: docs/plan/slices/29-kdp-cover-pdf.md)

#### Not yet verified

- `[owner]` Upload Freddy's `cover.pdf` and interior at https://kdp.amazon.com/en_US/bookshelf, pass KDP's cover check (watch the 0.0012 in page-size rounding) and order a proof.
- `[owner]` A real Farsi book's cover (no free book has Farsi text; the lion fa check was layout only).
- Verified 2026-11-19: Linux CI `kdp-linux` passed on PR #76 (1m30s).
