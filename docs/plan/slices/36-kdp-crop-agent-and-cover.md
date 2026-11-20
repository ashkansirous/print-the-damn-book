### Slice 36 — Amazon KDP: an agent picks every picture's crop, a cover without white boxes, tinted text pages

*Planned 2026-11-20 with the owner (interactive session, RefineScope), includes Slice 34 (back-cover keys). Built 2026-11-20 (A-G), merged as PR #85 (5b3eaf7); H (Freddy rebuild) done, the owner's verdict is pending.*

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

- [x] **A — Slice 34's reader fix** (every line of [Slice 34](34-backcover-key-case.md) *Work* A and B).
- [x] **B — Crop windows for every picture.** Core: a crop offset per picture key (story page number, `front`,
  `back`) in `KdpBookSettings`/`kdp.json`; `--offset front=<px>` and `--offset back=<px>` on the CLI; the covers use the
  saved offset. Without a saved choice the tool keeps today's top crop (the agent's choice is what changes it).
- [x] **C — Crop sheet.** A `--crop-sheet` output: `crop/sheet.png` (every uncut picture about 200 px wide, the kept
  window outlined, labelled by key) and `crop/<key>.png` at 480 px wide, uncut, window outlined. Maths for the outline
  in Core.
- [x] **D — `kdp-crop-check` agent and `kdp-crop` skill.** Agent (Sonnet, read-only) reads `crop/sheet.png`, opens a
  480 px copy only when unsure, returns one window per key as a pixel offset with a one-line reason. Skill runs the
  command with `--crop-sheet`, dispatches the agent, applies the offsets with `--offset`, reruns. `kdp-build` calls
  `kdp-crop` before its visual check.
- [x] **E — Size trial.** Run D over the 12 books twice (sheet vs 480 px), record agreement and token use here, set the
  default. Done 2026-11-20, default 480 px (see *Trial 2026-11-20* below).

##### Trial 2026-11-20

16 books, 265 pictures, each decided once from the 200 px sheet and once from the 480 px copies. The two agree within 60 source px on
238 of 265; the 27 differences are nearly all pictures where the 480 px copy saw small things cut at the bottom edge (feet, a cat, bed or
chair legs, a hedgehog) that the sheet missed without marking them unsure. Token use: the sheet run about 109k agent tokens for all 16
books; the 480 px run about 592k for 13 books (6 agents). **Decision: the sheet agrees only on most pictures and misses exactly the
faults the crop agent exists to catch, so by the rule "if they agree the sheet is the default, otherwise 480 px" the default is 480 px.**
The agent opens `crop/<key>.png` for every key (the sheet is an overview only), the 480 px copies carry the same label as the sheet
(`<key> y=<applied>/<max>`), and one agent runs per book. A first 480 px run put several books on one agent, hit the per-agent image
limit (about 250 reads) and wrote guesses for 13 books; those were discarded and redone one or two books per agent. Hence the rules in
`kdp-crop-check` (never decide a picture not seen; report `NOT SEEN`) and `kdp-crop` (one agent per book; never apply a result that does
not list every key as seen).

The 27 keys where the sheet and the 480 px copy differ by more than 60 px (sheet offset vs 480 px offset):

- freddy-and-the-slipper: 4 (0 vs 146), 5 (0 vs 146)
- goldilocks-and-the-three-bears: 12 (0 vs 91)
- jack-and-the-beanstalk: 7 (0 vs 100)
- little-red-riding-hood: 3 (120 vs 200), 9 (0 vs 105), 11 (0 vs 115), 14 (0 vs 110)
- love-from-seagull-island: 13 (0 vs 120)
- the-elves-and-the-shoemaker: 3 (0 vs 187), 6 (0 vs 225), 9 (400 vs 261), 10 (0 vs 411), 13 (0 vs 299), 14 (0 vs 373), 15 (0 vs 131),
  front (200 vs 373), back (450 vs 582)
- the-gingerbread-man: 7 (0 vs 75)
- the-little-red-hen: front (290 vs 430)
- the-town-mouse-and-the-country-mouse: 11 (40 vs 112), 15 (0 vs 112), front (0 vs 380)
- x-marks-the-treasure: 6 (400 vs 112), 8 (0 vs 170), 11 (0 vs 95), 13 (0 vs 95)
- [x] **F — Cover redesign.** No white boxes; title and "Written by ... · Illustrated by ..." on the front with a soft
  shadow; credits on the back; QR code (context7 for the QR library first) and the Dino line on the back; barcode area
  kept clear.
- [x] **G — Text pages.** Tint from the facing picture; keep the book's line breaks; shrink so no line wraps; report a
  page whose lines do not fit at the smallest size.
- [ ] **H — Freddy rebuild.** `kdp-build` on `D:\high-res-books\Freddy and the Slipper v10.hd.dino`; send the owner a
  small picture of the cover and two text spreads. Rebuilt 2026-11-20 into `tmp/freddy-final` (34 pages) with the 480 px
  agent crops (4 = 146, 5 = 146, back = 582); small renders shown to the owner the same day. Left unticked until the
  owner says the cover and text spreads look right.

#### Verification

- `dotnet test PrintTheDamnBook.slnx` green; Core tests for the crop key and outline maths, the line fit and the
  back-cover keys; Kdp tests for cover drawing (no white box, QR present and decodes to the site URL).
- E's comparison written in this file.
- `[owner]` The owner looks at Freddy's new cover and text pages (H) and says whether they look right.

#### Built

- Tests: 855 pass (Core, App, Kdp). The QR code is made with QRCoder 1.9.0 `PngByteQRCode` (managed, no System.Drawing, so
  `kdp-linux` still builds); ZXing.Net is a test-only package and decodes it to `https://dinoreader.sirous.uk/`.
- Work E's trial changed the plan: the default is 480 px copies, one agent per book (see *Trial 2026-11-20*).

#### Settled after building

- Back-cover and front text colours: replaced by Slice 37 (the crop agent picks text and shadow colours per cover).
- Text sizes stop at 14 pt, so Freddy pages 12 and 24 keep one wrapped line: owner, 2026-11-20, "14 pt is ok."

#### For CLAUDE.md (lifted 2026-11-20)

Already added in this branch's `CLAUDE.md` diff (two Slice 36 rules: the crop agent and its `kdp.json` offsets; cover words,
QR library and text-page tint). Nothing further to lift.

#### Not yet verified

- [owner] The owner looks at the Freddy cover and text spreads (H) and says whether they look right.
- [owner] A printed proof of the cover and interior.

#### From review

*Findings files:* `reviews/pr-85-high.md`, `reviews/pr-85-medium.md`, `reviews/pr-85-check.md`, `reviews/pr-85-check2.md`. Findings not listed below as skipped were not skipped (per the caller's list).

- Skipped #6 (`reviews/pr-85-high.md`): "draws every text block 17 times" - nit, the shadow stays as repeated text copies.
- Skipped #7 (`reviews/pr-85-high.md`): "recursively deletes `<out>/crop` on every run" - nit, same as the previews folder: removed on runs without `--crop-sheet`.
- Skipped #1 (`reviews/pr-85-medium.md`): "Every kdp run recursively deletes" - nit, same reason as high #7.
- Skipped #2 (`reviews/pr-85-medium.md`): "A language with no entry in the wording table" - nit, an unlisted language falls back to the back-cover wording.
- Skipped #4 (`reviews/pr-85-medium.md`): "Front title and credit are always `Backdrop.Dark`" - nit, the front stays white by owner decision.
- Skipped #4 (`reviews/pr-85-check2.md`): "Front credit that fits at no size throws in FrontWords" - nit, the cover stops, the interior is still written.
- Skipped #5 (`reviews/pr-85-check2.md`): "still measures all lines as one string in one font" - nit, same as check #3.
- Skipped #6 (`reviews/pr-85-check2.md`): "UnsavedCoverOffsetWarnings warns only when the cover is null" - nit, the cover offset is not saved when `MaxOffsetPx` is 0.
- Skipped #3 (`reviews/pr-85-check.md`): "ChooseBackCreditPt measures all back credit lines as one string in one font" - skipped by the caller's triage (nit).
- Skipped #5 (`reviews/pr-85-check.md`): "step 4 says an answer of 0 for a saved key clears it, reachable only on redo-all" - skipped by the caller's triage (nit).
- Skipped #7 (`reviews/pr-85-check.md`): "English retold books lose the retelling credit on the front; DinoLine is English-only" - skipped by the caller's triage (nit).
- Applied #2 only in part: the sheet label already shows the clamped offset (`KdpPictureCrop` clamps; label reads `crop.Source.Y`); the real gap was pictures beyond the story page count, now capped.
