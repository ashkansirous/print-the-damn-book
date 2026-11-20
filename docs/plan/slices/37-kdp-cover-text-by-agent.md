### Slice 37 — Amazon KDP: the crop agent also places the cover text and picks its colours

*Built 2026-11-20, merged as PR #88 (2b111ea), 904 tests pass. The owner has not yet looked at the 16 covers; KDP's acceptance of the transparent shadow is not yet verified.*

**Touches:** `src/PrintTheDamnBook.Core/Kdp/`, `src/PrintTheDamnBook.Kdp/`, `src/PrintTheDamnBook.Cli/`,
`tests/PrintTheDamnBook.Kdp.Tests/`, `tests/PrintTheDamnBook.Core.Tests/`, `.claude/skills/kdp-crop/`,
`.claude/agents/kdp-crop-check.md`, `.claude/skills/kdp-build/`, `CLAUDE.md`,
`docs/plan/slices/37-kdp-cover-text-by-agent.md`

**Migrations:** none

**Depends on:** Slice 36 (merged, PR #85). Slice 35 overlaps (`src/PrintTheDamnBook.Kdp/`): run it after this one.

#### Why

Owner, 2026-11-20, on two close-ups of Slice 36's Freddy cover (front title with its thick black shadow; back credits
over the kitchen's drawing lines): "look how horrible they look! it is bad! the agent that is deciding where to cut on
the images, should come back with more information about the front and back than just the cut position. it should also
say where there is empty space to write text and what color should be the text and what color should be the shadow of
it. for example in the case, I think the top text for the back cover on top could have been decided to go a bit to the
right to have less overlap with the drawing lines. or the book title and written by should have another shadow color.
aside these updates, drop the illustration and written by from the front cover. it is enough that it is written in the
back."

Cause (desk): Slice 36 places cover text with a fixed rule (`KdpCalmBand`, a calm horizontal band; back credits at the
top on the barcode-free side) and one colour pair (white with a dark shadow on the front, dark with a white halo on a
light back). The shadow is drawn as many hard offset copies of the text (Slice 36 high-review #6, skipped then), which
makes the thick black outline in the owner's close-up.

#### Decisions (owner, 2026-11-20)

- **Front cover shows the title only.** The "Written by ... · Illustrated by ..." line is dropped from the front; the
  credits stay on the back.
- **The crop agent decides the cover text, not a fixed rule.** For the front and the back cover, `kdp-crop-check`
  returns, besides the crop offset: where the empty space for the text is (a box inside the kept window), the text
  colour and the shadow colour. Front: one box, for the title. Back: one box, for the credit lines (it may move them
  sideways, e.g. to the right of drawing lines). The QR code and its line stay in the barcode-free bottom corner; the
  agent picks their text and shadow colours.
- The agent decides from the same 480 px uncut copies as the crop (Slice 36's trial: 480 px).
- **New Dino line** (owner, 2026-11-20: "change the text in the back to be ..."): on the back cover next to the QR code,
  exactly "Listen to this story in Dino: Kids Books & Stories. Want to join in? Pick a character and speak their lines
  yourself!" (replaces "Hear this story in Dino: Kids Books & Stories, and speak as the characters yourself!").
- **Last interior page** (owner: "in the last page of the interior, we have the same text. change it to ... and add the
  qr code"): the same line replaces the "Hear this story in" text (`KdpFrontMatterWording.AboutHeading` + `AboutLine`),
  with the same QR code to `https://dinoreader.sirous.uk/` beside it.
- **All books** (owner: "update the skills. these should be applied to all books."): the `kdp-crop` and `kdp-build`
  skills apply the cover text choices, wording and QR to every book; after the build, all 16 books in
  `D:\high-res-books` are rebuilt through `kdp-crop` and `kdp-build`, one crop agent per book.

#### Decisions (desk, 2026-11-20)

- The agent's text choices are applied through the `kdp` command and saved in `kdp.json` under each cover key with the
  crop offset (the tool writes `kdp.json`, never the skill). A cover with no saved text choice uses Slice 36's rule as
  the fallback.
- The shadow is soft (blurred, drawn once beneath the vector text), not stacked hard copies.
- The text box is given in fractions of the trim panel (left, top, width, height), so it survives a crop change; the
  tool still shrinks the text to fit the box and keeps it inside the safe margin and clear of the barcode.

#### Work

- [x] **A — Front title only.** Remove the front credit line; the title alone in its box.
- [x] **B — Text choices in `kdp.json` and on the CLI.** Per cover key: text box, text colour, shadow colour (hex),
  next to the crop offset; old `kdp.json` files still read. CLI options to set and clear them. Core holds the box
  maths (fractions to points, safe margin, barcode clearance) with tests.
- [x] **C — Soft shadow.** One blurred shadow beneath the vector text in the chosen colour; test that the text layer
  holds each line once.
- [x] **D — Agent and skill.** `kdp-crop-check` returns, for `front` and `back`, the text box(es) and the two colours
  with a one-line reason each; `kdp-crop` applies them with the CLI and reruns; the agent's never-decide-unseen rule
  covers them too.
- [x] **F — New Dino line and interior QR.** The new line on the back cover and the last interior page (one Core
  constant for both), and the QR code on that page; tests decode it.
- [x] **E — Rebuild all 16 books.** Run `kdp-crop` and `kdp-build` on every book in `D:\high-res-books` (Freddy first);
  show the owner small renders of Freddy's front title and back credits next to Slice 36's, then a contact sheet of all
  16 covers.

#### Verification

- `dotnet test PrintTheDamnBook.slnx` green; Core tests for the box maths; Kdp tests for a saved text choice being drawn
  where and in the colours saved, the soft shadow, and the front with no credit line.
- `[owner]` The owner looks at Freddy's new front title and back credits (E) and says whether they look right.

#### Built (2026-11-20)

- A to D and F built; `dotnet test` 904 pass. The shadow is the words rendered once in the shadow colour by QuestPDF
  `GenerateImages`, thickened and blurred with SkiaSharp, and placed as one transparent PNG under the vector text
  (`KdpSoftShadow`). CLI: `--text <front|back>=l,t,w,h,#text,#shadow` (or `=clear`) and `--dino-text
  back=#text,#shadow` (or `=clear`). New constant `KdpFrontMatterWording.DinoLine` on the back cover and on the last
  interior page, which carries a 1.1 in QR code.
- E done 2026-11-20: one crop agent per two books (Freddy alone), every picture seen, none failed. All 16 books rebuilt
  with the agents' choices, all exit 0, no fallback. Outputs in `tmp/books/<book>/` of the slice worktree (local, not
  kept). A contact sheet of the 16 covers was shown to the owner.
- The Little Red Hen front had no empty space (the agent answered no box), so the fallback rule placed the title.
- Agents marked unsure: Jack (key 13 and front text); Red Riding Hood (back Dino); Elves (key 15 and front); X Marks
  (key 6 and front); Dino colours on Good Enough, Seagull, Pigs and Tortoise.

*Changed while building:* none recorded in the brief.

*Decided by the owner (2026-11-20, after the 16-cover sheet):*

- The back Dino line sitting over the characters' feet on about 8 books is fine: "Is the Dino line over the characters'
  feet on the back covers OK? yes."
- Text sizes stop at 14 pt; Freddy pages 12 and 24 keep their one wrapped line: "14 pt is ok."

*From review:* read `D:/print-the-damn-book-worktrees/reviews/pr-88-high.md`, `pr-88-medium.md` and `pr-88-check.md`
(all three in that folder). Applied: high 1, 2, 3, 7; medium 1, 3, 5, 7; check 1, 2, 7.

- Skipped #4 (`reviews/pr-88-high.md`): "gets no right-to-left content direction" on the last interior page: other languages are parked.
- Skipped #5 (`reviews/pr-88-high.md`): "KDP can warn on or flatten transparency in uploaded PDFs": the printed proof and KDP's previewer decide; added to *Not yet verified*.
- Skipped #6 (`reviews/pr-88-high.md`): "one-line methods" (`LookOf`, `Note`, `DefaultCreditsBox`): style only, no behaviour change.
- Skipped #2 (`reviews/pr-88-medium.md`): "Look/box decisions, Notes and shadow PNG renders" inside the QuestPDF compose callback: no behaviour change seen.
- Skipped #4 (`reviews/pr-88-medium.md`): "no right-to-left mirroring" on the About page: other languages are parked.
- Skipped #6 (`reviews/pr-88-medium.md`): "Garbled doc comment" and dead code: nits, no behaviour change.
- Skipped #3 (`reviews/pr-88-check.md`): "ClearOf keeps only the larger leftover rectangle" (a box reshaped by clearance has no note): the half-height rule now falls back with a warning in the big cases.
- Skipped #4 (`reviews/pr-88-check.md`): "Look/box decisions, Note(...) and shadow PNG renders" run inside the Layers callback: no behaviour change seen.
- Skipped #5 (`reviews/pr-88-check.md`): "no RTL handling" on the last-page About row: other languages are parked.
- Skipped #6 (`reviews/pr-88-check.md`): "One-line methods break the 3-30 line rule": style only, no behaviour change.

#### For CLAUDE.md

- The Slice 37 rule is already in the root `CLAUDE.md` (the Slice 36 cover-words bullet, now citing Slice 37), written by
  the builder; the index pass lifts nothing more. (Slice 37: docs/plan/slices/37-kdp-cover-text-by-agent.md)

#### Not yet verified

- [owner] The owner looks at the 16 rebuilt covers (contact sheet) and says whether they look right.
- [owner] KDP accepts the transparent soft shadow (its previewer and a printed proof).
