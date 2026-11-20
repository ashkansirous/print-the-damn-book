### Slice 42 — Amazon KDP: one text size for every story page of a book

*Planned 2026-11-20.*

**Touches:** `src/PrintTheDamnBook.Core/Kdp/`, `src/PrintTheDamnBook.Kdp/`, `tests/PrintTheDamnBook.Kdp.Tests/`,
`tests/PrintTheDamnBook.Core.Tests/`, `docs/plan/slices/42-kdp-one-text-size-per-book.md`

**Migrations:** none

**Depends on:** Slice 36 (the one-line rule, merged), Slice 41 (merged; its rebuilt books are the baseline).

#### Why

Owner, 2026-11-20: "on "little-red-riding-hood" page 28 is generated with wrong font. investigate why and fix it."
The font was right (Andika everywhere); page 28 was set at 14 pt between pages at 24 pt. The Slice 36 one-line rule
(`ChooseSize`, `src/PrintTheDamnBook.Kdp/KdpInterior.cs`) picks a size per page: the largest of `KdpTrim.TextStepsPt`
(24 down to 14) at which every line of the page stays on one line, else 24 pt with wrapping. So a page whose longest
line just fits at 14 pt shrinks while its neighbours stay at 24 pt. Seven books have such jumps (Freddy, Goldilocks,
Good Enough, Lion and the Mouse, Three Little Pigs, Tortoise and the Hare, Little Red Riding Hood).

Offered (desk): one size for the whole book, "every text page uses the size of its tightest page that still fits, so
there are no jumps. A long sentence may wrap"; or an 18 pt floor; or as is. Owner, 2026-11-20: "yes. your pick is good.
1 sizr".

#### Decisions (owner, 2026-11-20)

- **One text size for every story text page of a book.** The book's size is the size of its tightest page that still
  fits: the smallest of the per-page one-line sizes, over the pages where all lines fit on one line at some step. A
  page whose lines don't fit even at 14 pt does not pull the size down; it wraps at the book's size.
- The 14 pt minimum stays (owner, earlier the same day: "14 pt is ok").

#### Decisions (desk, 2026-11-20)

- The book-size maths (from the per-page one-line sizes to the one book size) lives in Core `Kdp/` with tests; the
  Kdp project only measures lines and draws (CLAUDE.md: layout maths in Core).
- Front-matter pages (title, copyright, the last page) keep their own fixed sizes; only story text pages change.
- `check-report.md`: one line "Story text set at N pt on every page (the tightest page that fits on one line: page P)"
  in place of the per-page "set at N pt" lines; a page that wraps at the book size keeps its own warning.
- Books with no page that needs shrinking keep 24 pt (no change).

#### Work

- [ ] **A — Core book size.** A Core function taking each story page's one-line size (or none) and returning the one
  book size; tests: all none gives the first step; mixed gives the smallest; pages with none are ignored.
- [ ] **B — Interior uses it.** `KdpInterior` measures each page, asks Core for the book size, sets every story page
  at it, and writes the report lines above; tests: every story page in a built test book has the same size, a page
  that wraps at the book size keeps its warning.
- [ ] **C — Rebuild all 16 books** with their saved `kdp.json` (no crop agent round), and show the owner Little Red
  Riding Hood pages 26 and 28 side by side and a contact sheet of one changed book.

#### Verification

- `dotnet test PrintTheDamnBook.slnx` green.
- Every story page of Little Red Riding Hood has the same font size (read from the PDF).
- `[owner]` The owner looks at the rebuilt pages.

#### Not yet verified

- [owner] The owner looks at the rebuilt story pages.
