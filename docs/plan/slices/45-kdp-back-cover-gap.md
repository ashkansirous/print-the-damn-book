### Slice 45 — Amazon KDP: the Dino block sits right under the credits on the back cover, and no white barcode box

*Planned 2026-11-25 (interactive session, owner present).*

**Touches:** `src/PrintTheDamnBook.Core/Kdp/KdpCoverText.cs`, `src/PrintTheDamnBook.Kdp/Cover/KdpCoverDocument.cs`,
`tests/PrintTheDamnBook.Core.Tests/`, `tests/PrintTheDamnBook.Kdp.Tests/`, `.claude/agents/kdp-crop-check.md`,
`.claude/skills/kdp-crop/`, `docs/plan/slices/45-kdp-back-cover-gap.md` (new)

**Migrations:** none

**Depends on:** Slice 41 (merged). Takes item L over from Slice 43 (not merged; its worktree `kdpx` holds the same uncommitted change).

#### Why

Owner, 2026-11-25, looking at the X Marks the Treasure! back cover: "why the agent decided to put these 2 text boxes afar?" The three
credit lines sit at the top left and the QR code with the Dino line about an inch and a half lower, with empty sky between them.

Cause (desk, read from the files): the agent did not place them far apart. Its saved boxes
(`s41/tmp/books/x-marks-the-treasure/words.txt`) are credits `0.08,0.09,0.54,0.18` and Dino `0.08,0.30,0.60,0.15`, only 0.03 apart.
The gap comes from inside the boxes:

1. `.claude/agents/kdp-crop-check.md` line 64 tells the agent to size the credits box for "about 4 to 6 short lines, about 1.5 in tall";
   X Marks has 3 lines, and `BackCredits` (`KdpCoverDocument.cs`) draws them at the top of the box, so its lower half stays empty.
2. `KdpTextBoxMaths.SplitDinoBlock` (`KdpCoverText.cs`) centres the QR code vertically in the Dino box, and `BackQr` centres the line,
   adding more empty space above them.

Owner: "fix it". Also outstanding from 2026-11-21 (Slice 43 item L, owner: "we have a problem with the white space you put for the isbn.
Dont know why you did that! if you left it alone, it would have been ok"): the white barcode box is still on every cover built from `main`.

#### Decisions (desk, 2026-11-25)

- The fix is in code, so all 16 books' saved `kdp.json` boxes stay as they are: no crop agent rerun, no other cover choice changes.
- When the chosen Dino block lies below the credits box and overlaps it horizontally (the same column), the block moves up so its top is
  one gap (`DinoUnderCreditsGapInches`, 0.25 in) below the bottom of the credit lines as drawn (their real height at the chosen size and
  spacing, not the box's). It keeps its height and left/right. It only ever moves up, into space the agent already chose as calm for
  the credits or between the two boxes; it never moves down. A Dino block placed elsewhere (another column, above the credits) is left alone.
- Inside a chosen Dino block the QR code sits at the block's top edge and the line starts level with the QR code's top (top-aligned, not
  centred). The bottom-corner block (no saved box) is unchanged.
- The agent sizes the credits box to the real number of credit lines (about 0.35 in per line plus spacing) instead of "4 to 6 lines";
  the kdp-crop skill tells it the line count for each book. This only matters for books cropped from now on.
- Item L from Slice 43: no white fill in the barcode spot; the barcode clearance stays, so no words or QR code go there.

#### Work

- [x] A. Core: `KdpTextBoxMaths` gets the move-up maths (block, credits text bottom, gap) with unit tests: same column moved up; another
  column left alone; above the credits left alone; never moves down; result stays inside the block's original horizontal span.
- [x] B. Kdp: `BackPlacements` measures the drawn credit lines' height and applies A to a chosen Dino block; `SplitDinoBlock` puts the QR
  at the block top and `BackQr` top-aligns the line. Tests in `tests/PrintTheDamnBook.Kdp.Tests/` for the X Marks boxes above: the
  Dino block's top is within 0.25 in (plus rounding) of the third credit line's bottom.
- [x] C. Remove the white `Barcode` layer from `KdpCoverDocument` (Slice 43 item L); keep `KdpCoverSize.Barcode` clearance checks; update
  the cover tests that expected the white box.
- [x] D. `.claude/agents/kdp-crop-check.md` and `.claude/skills/kdp-crop/`: credits box height from the given line count.
- E. (removed, owner 2026-11-25: "I told you we are in the fucking checking phase! do not update the fucking 16 books") No book is
  rebuilt by this slice. The build was stopped mid-way; code is not finished. Owner, same day: "you are only allowed to work on
  the fucking english version of the fucking x marks the fuckng treasure!" Any KDP work in this phase is X Marks the Treasure!, English only.
- [x] E2. (owner 2026-11-25: "fix it for the fucking x marks the fucking treasure for the fucking english language") Pilot: after review,
  rebuild X Marks the Treasure! English only, with its saved `kdp.json` from `kdpx/tmp/kdp-out/X Marks the Treasure! v16.hd-en/`, into
  `tmp/x-marks-en/` of this worktree, and check its back cover. Other books get the fix only after the owner checks the pilot and says so.

#### Verification

- `dotnet test` green (Core and Kdp tests), `dotnet format --verify-no-changes`.
- E: the 16 rebuilt covers, read from small renders (never full size).
- `[owner]` looks at the rebuilt back covers.

#### Not yet verified

- [owner] The owner looks at the rebuilt X Marks the Treasure! (English) cover: `tmp/x-marks-en/cover.pdf` in this worktree (built 2026-11-25, exit 0, no report errors, kdp.json unchanged). Other books get the fix only after the owner says so.

#### Built

Items A to D built 2026-11-25 (not committed). Core: `KdpTextBoxMaths.DinoUnderCreditsGapInches` and `MoveUnderCredits(block, creditsBox, creditsTextBottom)`; `SplitDinoBlock` puts the QR code at the block's top.
Kdp: `KdpCoverDocument.MovedUnderCredits` (credit text height from `KdpTextFit.TightHeightInches` per line plus spacing), `DinoPlacement.TopAligned` (chosen blocks only; the corner block is unchanged),
white `Barcode` layer removed. Agent and `kdp-crop` skill: credits box sized from the credit line count (3 or 4). No book was built.

Pilot (E2), 2026-11-25: X Marks the Treasure! English rebuilt from its saved `kdp.json` into `tmp/x-marks-en/`; exit 0, no report
errors (one warning: pictures at about 208 dpi, as before), `kdp.json` unchanged. Back cover read from a 700 px render: the QR code
and Dino line sit right under the three credit lines; no white box in the barcode spot. No other book or language was built.

## Review

- Pass 1 (high): `../reviews/s45-high.md`; findings 1-5 fixed, 6-7 skipped (wording, docs note).
- Pass 2 (medium): `../reviews/s45-medium.md`; finding 1 fixed (moved block keeps the saved Dino colours), 2-4 skipped (the block
  only moves up into the credits' calm area, so margin and barcode clearance only grow).
