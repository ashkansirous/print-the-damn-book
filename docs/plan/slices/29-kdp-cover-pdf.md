### Slice 29 — Amazon KDP: cover PDF (back, spine, front in one wrap)

*Planned 2026-11-19 from the Scope Inbox doc
[2026-11-18-amazon-kdp-export.md](https://drive.google.com/file/d/16B6KoIGXtHRXaRS8VvQcopP5RyJhcUIM/view?usp=drivesdk)
(milestone 3).*

*In progress (wave 2026-11-19), draft PR.*

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

- [ ] `KdpCoverSize` with tests against KDP's calculator values.
- [ ] `KdpCoverDocument` (QuestPDF), fonts embedded; `cover.pdf` written by the same `kdp` command.
- [ ] Check report gains the cover size, spine width and barcode box.

#### Verification

- Unit tests green on Windows and Linux CI; Freddy's `cover.pdf` page size matches the calculator for its page count.
- `[owner]` Upload Freddy's `cover.pdf` with the interior at https://kdp.amazon.com/en_US/bookshelf, pass KDP's cover check,
  and order one printed proof; warnings and the proof's look are written here.
