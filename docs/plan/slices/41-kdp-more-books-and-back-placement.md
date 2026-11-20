### Slice 41 — Amazon KDP: "More books" on the last page, and the crop agent places the back-cover words and the Dino QR in the empty space

*Planned 2026-11-20.*

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

- [ ] **A — Dino box on the back cover.** `kdp.json`, CLI and Core box maths for a placed Dino block (QR plus line),
  with the clearances above and the fallback; tests.
- [ ] **B — "More books" on the last page.** Seeded pick of at most 15 other books (Core, tested: never this book,
  stable per book id, at most 15); the last interior page draws heading and covers on top, the Dino line and QR at the
  bottom; page count unchanged; tests (count, order stable, this book absent, QR still decodes).
- [ ] **C — Agent and skills.** `kdp-crop-check` returns the back credits box and the Dino box placed in the empty
  space (rules above); `kdp-crop` applies them; `kdp-build` passes the other-books folder.
- [ ] **D — Rebuild all 16 books.** Re-run the crop agent's back-cover choice for every book, rebuild, show the owner
  the 16 back covers and Freddy's last page.

#### Verification

- `dotnet test PrintTheDamnBook.slnx` green; Core tests for the pick and the Dino box maths; Kdp tests for the last
  page and a saved Dino box drawn where saved.
- `[owner]` The owner looks at the 16 back covers and Freddy's last page.

#### Not yet verified

- [owner] The owner looks at the rebuilt back covers and last pages.
