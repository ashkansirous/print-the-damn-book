### Slice 41 — Amazon KDP: "More books" on the last page, and the crop agent places the back-cover words and the Dino QR in the empty space

*Built 2026-11-20, merged as PR #93 (f27653f), 977 tests pass. Item D done 2026-11-20: all 16 books rebuilt (exit 0, no fallback). Verified: the owner accepted the 16 covers and last pages ("1- all good.", 2026-11-20).*

**Touches:** `src/PrintTheDamnBook.Core/Kdp/`, `src/PrintTheDamnBook.Kdp/`, `src/PrintTheDamnBook.Cli/`,
`tests/PrintTheDamnBook.Kdp.Tests/`, `tests/PrintTheDamnBook.Core.Tests/`, `.claude/skills/kdp-crop/`,
`.claude/agents/kdp-crop-check.md`, `.claude/skills/kdp-build/`,
`docs/plan/slices/41-kdp-more-books-and-back-placement.md`

**Migrations:** none

**Depends on:** Slice 37 (merged, PR #88).

#### Why

Owner, 2026-11-20, asking for an "other books by this author" page; offered five layouts (own last page, own page
before the last, or merged with the Dino line and QR code on the last page with the Dino line at the bottom, top or
middle). Three mock pages of Freddy's last page were shown (options 3, 4, 5). Owner: "I am ok with option 3. 1 thing to
notice is that his page is not big, so it should only take 15 books and those books are the other books. so when I add
another 5 books, these pages should not grow bigger, but you should just select random other books to put in here."

Same message, on the back covers of The Gingerbread Man and The Lion and the Mouse: "it seems on the-gingerbread-man the
agent picked the wrong place to put the text. fix the agent. clearly, the space in the middle of the page is free and
can contain the text instead of on top. I dont know if we can move the qr code and the text to the middle as well or
not, but it is better if it would have done that. I think the other books can use the same logic as well. like why would
we put the text and the QR code right on lion and mouse while there is big empty space right above them?"

Cause (desk): Slice 37's agent brief steers the back credits to the top of the window, and the Dino line with its QR
code is fixed in the barcode-free bottom corner (`KdpTextBoxMaths.QrStrip`); the agent only picks its colours.

#### Decisions (owner, 2026-11-20)

- **Option 3:** the last interior page shows "More books by ..." with the other books' covers on top, and the Dino line
  with its QR code at the bottom (as on the mock `option-3-books-top.png`). The book gets no extra page.
- **At most 15 other books, never this book.** With more than 15 other books the page does not grow: 15 are picked at
  random from the others.
- **The agent places the back-cover credits in the empty space**, e.g. the middle when the middle is free, not the top
  by default.
- **The Dino line and QR code move too**: the agent places them in empty space (e.g. above the lion and mouse), not
  over characters.
- The same logic applies to every book.
- **The rule covers the front title too** (owner, on The Town Mouse and the Country Mouse front cover, title over the
  two robins: "this seems like the wrong place to put the text. probably top left would have been a better place cause
  text on top of trees is better than on top of characters/animals"). It applies to the FRONT title as well as the back
  blocks: text may sit over scenery (trees, sky, foliage, background) but never over any character or animal, however
  small (birds included).

#### Decisions (desk, 2026-11-20)

- The pick of 15 is random but **seeded by the book's id**, so a rebuild gives the same page (a KDP re-upload doesn't
  change it by chance). The order on the page is the seeded order.
- Covers on the page are each other book's front picture cut and titled as on that book's own cover: its saved front
  offset and front text choice from that book's `kdp.json` when found, else the Slice 36 fallback rule. Grid of 5 per
  row, at most 3 rows, as on the mock. The heading names the author line of the other books (mock: "More books by
  Ashkan Sirous"); its wording is one Core constant/builder, never built in the drawing code.
- The other books come from a folder of `.dino` files (CLI option, default for the skill: `D:\high-res-books`) plus the
  output root holding each book's folder (for their `kdp.json`); no folder given means the last page stays as today.
- The back Dino block gets a box like the credits: `kdp.json` `covers.back.dino.box` (fractions of the trim panel), tied
  to the crop offset it was chosen for (`forOffsetPx`, as in Slice 37). The box must stay inside the safe margin, clear
  of the barcode area and not overlap the credits box; otherwise it falls back to today's bottom corner with a warning.
  Older `kdp.json` files without a Dino box keep today's placement. CLI: `--dino-text back=l,t,w,h,#text,#shadow`
  (the colour-only form still reads).
- The agent's brief: put each block in the largest calm area that fits it, centred in that area, rather than hugging
  the window's top or bottom; never over a face or character; keep the QR code at a size a phone scans (today's size).

#### Work

- [x] **A — Dino box on the back cover.** `kdp.json`, CLI and Core box maths for a placed Dino block (QR plus line),
  with the clearances above and the fallback; tests.
- [x] **B — "More books" on the last page.** Seeded pick of at most 15 other books (Core, tested: never this book,
  stable per book id, at most 15); the last interior page draws heading and covers on top, the Dino line and QR at the
  bottom; page count unchanged; tests (count, order stable, this book absent, QR still decodes).
- [x] **C — Agent and skills.** `kdp-crop-check` returns the back credits box and the Dino box placed in the empty
  space (rules above); `kdp-crop` applies them; `kdp-build` passes the other-books folder.
- [x] **D — Rebuild all 16 books.** Re-run the crop agent's back-cover choice for every book (and re-decide the front
  text box too), rebuild, show the owner
  the 16 back covers and Freddy's last page.

#### Verification

- `dotnet test PrintTheDamnBook.slnx` green; Core tests for the pick and the Dino box maths; Kdp tests for the last
  page and a saved Dino box drawn where saved.
- `[owner]` The owner looks at the 16 back covers and Freddy's last page.

#### Built (A-C)

- CLI: `--dino-text back=l,t,w,h,#text,#shadow` (the colour-only form still reads; `=clear` removes the saved choice),
  `--other-books <folder of .dino>`, `--books-out <folder of book output folders>` (default: the parent of `--out`;
  alone it is a usage error).
- `kdp.json` `covers.back.dino` = `{color, shadow, box?, forOffsetPx?}`.
- Other-books pick: Core `KdpOtherBooks`, at most 15, seeded by SHA-256 of book id + candidate id, never this book.
  Heading constant `KdpFrontMatterWording.OtherBooksHeadingText`.
- Tiles: each other book's own front cover drawing through its `kdp.json` (the folder with a `-<language>` suffix is
  preferred, else the first by ordinal); tile 95 pt, 5 per row.
- Dino block maths in Core `KdpTextBoxMaths` (`SplitDinoBlock`, `CornerDinoBlock`, `ChosenBlockClearOfCredits`).
- A failure with the other-books folder or one tile is a warning in `check-report.md`, never an aborted export.
- Hands-on: Freddy's last page shows 15 books; Lion with a hand-set Dino box above the lion.

#### For CLAUDE.md (lifted 2026-11-20)

- The back Dino block's box (`covers.back.dino.box`) is set only by the CLI; its maths is Core `KdpTextBoxMaths`. The last interior page lists at most 15 other books, picked by Core `KdpOtherBooks` (seeded, never this book). Text goes over scenery, never over a character or animal (crop agent rule). (Slice 41: docs/plan/slices/41-kdp-more-books-and-back-placement.md)

#### Not yet verified

- None. The owner looked at the rebuilt covers and last pages and accepted them: "1- all good." (2026-11-20).

#### Review

- `reviews/pr-93-high.md`, `reviews/pr-93-medium.md`, `reviews/pr-93-check.md`, `reviews/pr-93-check2.md`, `reviews/pr-93-check3.md`

#### From review

- Skipped #3 (`reviews/pr-93-high.md`): "The heading is the English `OtherBooksHeadingText` and the tile grid and `DinoPair` row are left to right even for a right-to-left book" - other languages are parked by the owner; the English heading stays for now.
- Skipped #3 (`reviews/pr-93-medium.md`): "The other book's kdp.json is picked by `-<lang>` suffix; with no match, `folders[0]` (first by name) is used, which may be another language's crop and text box" - every book is built in English only, one output folder per book, so no other-language folder exists to be picked; this returns with Slice 33 (other languages, parked in plan.md).
- Skipped #4 (`reviews/pr-93-check.md`): "Each tile renders a full wrap cover and reparses the .dino; about 240 renders for a 16-book rebuild" - speed is acceptable (about 1 s per tile).
- Skipped #5 (`reviews/pr-93-check.md`): "When no folder matches the language, the first folder's kdp.json (other language) is applied to the drawn title" - every book is built in English only, one output folder per book, so no other-language folder exists to be picked; this returns with Slice 33 (other languages, parked in plan.md).
- Skipped #6 (`reviews/pr-93-check.md`): "WithDinoColors keeps a stale saved box after a changed --offset back; only a later warning shows it" - the `forOffsetPx` stale rule already falls back to the corner with a warning.
- Skipped #4 (`reviews/pr-93-medium.md`): "The cover grid and `CoverRow` always run left to right, even for right-to-left books, and the "More books by ..." heading is English-only" - other languages are parked by the owner (same as high #3).
- Skipped #3 (`reviews/pr-93-check2.md`): "SettingsFor falls back to folders[0] (another language's kdp.json) when no folder ends with -<lang>; its text box and colours are applied to this language's title" - every book is built in English only, one output folder per book, so no other-language folder exists to be picked; this returns with Slice 33 (other languages, parked in plan.md).
- Skipped #4 (`reviews/pr-93-check2.md`): "Each tile renders a full wrap cover and re-extracts the .dino (about 15 renders per language per export)" - speed is acceptable.
- Skipped #5 (`reviews/pr-93-check2.md`): "WithDinoColors keeps a saved box after a changed --offset in the same call; the box goes stale silently" - the stale rule already falls back to the corner with a warning.
