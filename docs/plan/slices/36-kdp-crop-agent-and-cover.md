### Slice 36 — Amazon KDP: an agent picks every picture's crop, a cover without white boxes, tinted text pages

*Planned 2026-11-20 with the owner (interactive session, RefineScope). Includes Slice 34 (back-cover keys).*

**Touches:** `src/PrintTheDamnBook.Core/Kdp/`, `src/PrintTheDamnBook.Core/Dino/DinoBackCover.cs`,
`src/PrintTheDamnBook.Core/Text/TextPageTint.cs`, `src/PrintTheDamnBook.Kdp/`, `src/PrintTheDamnBook.Cli/`,
`tests/PrintTheDamnBook.Kdp.Tests/`, `tests/PrintTheDamnBook.Core.Tests/`,
`tests/PrintTheDamnBook.App.Tests/StoryFitAndBackCoverTests.cs`, `.claude/skills/kdp-build/`,
`.claude/skills/kdp-crop/` (new), `.claude/agents/kdp-crop-check.md` (new), `.claude/agents/kdp-visual-check.md`,
`CLAUDE.md`, `docs/plan/slices/36-kdp-crop-agent-and-cover.md`

**Migrations:** none

**Depends on:** nothing. Slice 35 overlaps (`src/PrintTheDamnBook.Kdp/`, `.claude/skills/kdp-build/`): run it after this one.

#### Why

Owner, 2026-11-20, on Freddy's first `cover.pdf`: "the cover is aweful! you have the wrong cut of the images. also, the
white background for the text makes it look horrible. also, shouldnt the cover contain written by anywhere? lastly, the
book should have a small qr code to the dinoreader's web page ... hear this story in Dino: Kids Books & Stories and
optionally speak on behalf of the characters."

Cause of the cut (desk, 2026-11-20): every Dino picture is 1792 x 2400 (3:4) and the trim is square, so about a quarter
of each picture is dropped. `KdpPictureCrop` (`Core/Kdp/KdpPictureCrop.cs:22-51`) keeps the top for every picture, and
`KdpCover.cs:38-39` uses it for both covers with no offset. Interior pictures are designed with their bottom ~30% free
(the text band), so keeping the top is right for them; the back cover is the exception, its characters sit at the
bottom ("the fucking back cover is the excception where the characters are sitting at the bottom of the fucking image,
so it should crop the top for them"). Slice 29 had already seen the Lion's back-cover head cut at the bottom and wrote it
off as "content crop, not a layout fault" (desk mistake). The cover title and the Dino line sit in white boxes
(`KdpCoverDocument.cs`); interior text pages are plain white and long lines wrap with one word alone
(`KdpTextFit.cs:38-46`, one `Span`). The back cover shows no credits because of Slice 34's key-case bug.

#### Decisions (owner, 2026-11-20)

- **Trim stays square, 8.5 x 8.5 in** (owner chose "b! I keep it square!"; the 8.25 x 11 option is dropped).
- **Check before cutting, by an agent:** "it is a good idea to check first and decide before cutting. Create a skill and
  an agent for that." The agent opens a **low-resolution, uncut** copy of every picture (interior pictures, front cover,
  back cover) and decides the best crop window for each, anywhere from top to bottom: "most probably it would crop the
  image as I designed them, but in some cases it might decide that the best crop is somewhat in the middle". No fixed
  per-kind default overrides its choice (owner rejected "tool default plus agent exceptions" and "flagged only").
- **Token cost:** the agent never sees full-size pictures. The tool writes one small sheet per book of every whole,
  uncut picture (about 200 px wide each) with the square window marked; the agent opens a 480 px uncut copy only for a
  picture it is unsure about (owner: "use the low-resolution ones ... or create an even lower res file if the same good
  conclusion can be taken using a smaller image to save token"; approved question 1).
- **Try both sizes:** run the agent over the 12 books in `D:\high-res-books` once with the 200 px sheet and once with
  480 px uncut copies, compare the windows it picks; if they agree, the sheet is the default, otherwise 480 px, and the
  differences are written here.
- **Cover title:** white letters with a soft dark shadow, no box, in the calmest part of the front picture; under it in
  smaller letters "Written by <story by> · Illustrated by <illustration>" from the book's back-cover data (approved
  question 3).
- **Back cover:** credit lines at the top in the same style; at the bottom, on the side away from the barcode, a QR
  code about 0.8 in wide opening `https://dinoreader.sirous.uk/` with the line "Hear this story in Dino: Kids Books &
  Stories, and speak as the characters yourself!"; the barcode area stays empty (approved question 4: "lets try it and
  see how it looks").
- **Interior text pages:** a soft tint from the facing picture (reuse Core `TextPageTint`), and each of the book's own
  lines stays on one line: the size shrinks until it fits, no wrapping (owner chose a).

#### Work

- [ ] **A — Slice 34's reader fix** (every line of [Slice 34](34-backcover-key-case.md) *Work* A and B).
- [ ] **B — Crop windows for every picture.** Core: a crop offset per picture key (story page number, `front`,
  `back`) in `KdpBookSettings`/`kdp.json`; `--offset front=<px>` and `--offset back=<px>` on the CLI; the covers use the
  saved offset. Without a saved choice the tool keeps today's top crop (the agent's choice is what changes it).
- [ ] **C — Crop sheet.** A `--crop-sheet` output: `crop/sheet.png` (every uncut picture about 200 px wide, the kept
  window outlined, labelled by key) and `crop/<key>.png` at 480 px wide, uncut, window outlined. Maths for the outline
  in Core.
- [ ] **D — `kdp-crop-check` agent and `kdp-crop` skill.** Agent (Sonnet, read-only) reads `crop/sheet.png`, opens a
  480 px copy only when unsure, returns one window per key as a pixel offset with a one-line reason. Skill runs the
  command with `--crop-sheet`, dispatches the agent, applies the offsets with `--offset`, reruns. `kdp-build` calls
  `kdp-crop` before its visual check.
- [ ] **E — Size trial.** Run D over the 12 books twice (sheet vs 480 px), record agreement and token use here, set the
  default.
- [ ] **F — Cover redesign.** No white boxes; title and "Written by ... · Illustrated by ..." on the front with a soft
  shadow; credits on the back; QR code (context7 for the QR library first) and the Dino line on the back; barcode area
  kept clear.
- [ ] **G — Text pages.** Tint from the facing picture; keep the book's line breaks; shrink so no line wraps; report a
  page whose lines do not fit at the smallest size.
- [ ] **H — Freddy rebuild.** `kdp-build` on `D:\high-res-books\Freddy and the Slipper v10.hd.dino`; send the owner a
  small picture of the cover and two text spreads.

#### Verification

- `dotnet test PrintTheDamnBook.slnx` green; Core tests for the crop key and outline maths, the line fit and the
  back-cover keys; Kdp tests for cover drawing (no white box, QR present and decodes to the site URL).
- E's comparison written in this file.
- `[owner]` The owner looks at Freddy's new cover and text pages (H) and says whether they look right.
