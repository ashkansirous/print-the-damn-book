### Slice 42 — Amazon KDP: one text size for every story page of a book

*Built 2026-11-20, merged as PR #100 (4425035), 980 tests pass. Item C (rebuild all 16 books) done before merge on the reviewed code is re-run after merge.*

**Touches:** `src/PrintTheDamnBook.Core/Kdp/`, `src/PrintTheDamnBook.Kdp/`, `tests/PrintTheDamnBook.Kdp.Tests/`,
`tests/PrintTheDamnBook.Core.Tests/`, `src/PrintTheDamnBook.Cli/`, `.claude/agents/kdp-visual-check.md`,
`.claude/skills/kdp-build/`, `docs/plan/slices/42-kdp-one-text-size-per-book.md`

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

#### Decisions (owner, 2026-11-20, second answer: replaces the first)

- **Every story text page of every book is set at 24 pt.** Lines that don't fit on one line wrap. Owner, after seeing
  Little Red Riding Hood built at 14 pt on every page under the first rule: "they all should stay big. the one size on
  top left is the correct size for all books. ... all pages, all books 24pt."
- The one-line shrink (Slice 36, `KdpTextFit.LinesStayWhole` choosing 24 down to 14 pt per page) is no longer used for
  story text pages.
- Superseded first answer (same day): one size per book = the smallest per-page one-line size. Built in PR #100 first
  and replaced before merge; it set Little Red Riding Hood at 14 pt on all 14 story pages.

#### Decisions (desk, 2026-11-20)

- Front-matter pages (title, copyright, the last page) keep their own fixed sizes; only story text pages change.
- A story page whose wrapped words do not fit the text box at 24 pt is listed in `check-report.md` as an error naming
  the page, never silently shrunk; the owner decides what to do with such a page.
- `check-report.md`: one line "Story text set at 24 pt on every page." in place of the per-page "set at N pt" lines.

#### Work

- [x] **A — Fixed 24 pt.** Story text pages are always set at 24 pt (a Core constant); remove the first answer's
  book-size function (`KdpBookTextSize`) and its tests; tests: every story page in a built test book is 24 pt, a page
  whose wrapped words don't fit the box at 24 pt gives the error line naming it.
- [x] **B — Report.** The one report line above; per-page wrap notes for pages that simply wrap are dropped (wrapping
  is now normal); tests.
- [x] **C — Rebuild all 16 books** with their saved `kdp.json` (no crop agent round), and show the owner Little Red
  Riding Hood pages 26 and 28 side by side and a contact sheet of one changed book.

#### Verification

- `dotnet test PrintTheDamnBook.slnx` green.
- Every story page of Little Red Riding Hood has the same font size (read from the PDF).
- `[owner]` The owner looks at the rebuilt pages.

#### Built

- Story text pages are fixed at 24 pt (Core `KdpTrim.StoryTextPt`). `KdpTextFit.LinesStayWhole`, the per-page shrink and the superseded `KdpBookTextSize` are removed.
- A page whose wrapped words overflow its box keeps the planned page count (QuestPDF `StopPaging`; the overflowing lines are cut). It is listed in `check-report.md` under `## Errors`, printed as `Error:` on stderr, and makes the `kdp` command exit 1 after the PDFs, report and `kdp.json` are written.
- Summary line in the report: "Story text set at 24 pt on every page." (or it names the pages that don't fit).
- `kdp-visual-check` rule 6 no longer flags wrapped lines. The `kdp-build` SKILL documents exit 1 with the files written.
- Hands-on: all 16 books rebuilt at 24 pt before review; none has a page that doesn't fit. Little Red Riding Hood's 14 story pages are all 24 pt (read from the PDF).

#### Review

- `D:/print-the-damn-book-worktrees/reviews/pr-100-high.md`
- `D:/print-the-damn-book-worktrees/reviews/pr-100-medium.md`
- `D:/print-the-damn-book-worktrees/reviews/pr-100-check.md`

#### From review

- Skipped #3 (`reviews/pr-100-medium.md`): "Still says each own line stays on one line (KdpTextFit.LinesStayWhole), which this change deletes" - CLAUDE.md changes only in the index pass after merge (repo docs layout); the wording is in this file's For CLAUDE.md section.
- Skipped #3 (`reviews/pr-100-check.md`): "Rule still names the deleted `KdpTextFit.LinesStayWhole` and shrink-to-fit" - CLAUDE.md changes only in the index pass after merge (repo docs layout).
- Skipped #1 (`reviews/pr-100-check.md`): "StopPaging only drops what does not fit; it does not clip symmetrically" - the error names the page and says its words don't fit, which is what the owner acts on; none of the 16 books has such a page.
- Skipped #2 (`reviews/pr-100-check.md`): "src/PrintTheDamnBook.Kdp/KdpTextFit.cs:100, nit. Fits turns every DocumentLayoutException into" - the fonts are embedded resources of the Kdp project; a load failure fails every Kdp test, so it cannot reach a build unnoticed.
- Skipped #4 (`reviews/pr-100-check.md`): "On overflow, files (interior.pdf, kdp.json) are still written" - Decision (desk) in this file: the page is reported as an error and the owner decides; the exit code is 1; the robot gap is recorded under Follow-ups.
- Skipped #5 (`reviews/pr-100-check.md`): "Overflow tests check page count, report text and exit code, not the overflowing page's PDF content" - the overflow path is covered by the page-count, error-line and exit-code tests.

#### Follow-ups

- `tools/kdp-robot` (`src/draft.mjs:45-52`) checks only that interior.pdf and cover.pdf exist; it would accept a book whose check-report.md has an `## Errors` section if the kdp-build exit 1 were skipped. Hardening: `planDraft` refuses a folder whose check-report.md has `## Errors`. Goes to plan.md Outstanding work in the index pass.

#### Not yet verified

- [owner] The owner looks at the rebuilt story pages.
- [session] Item C re-run after merge: rebuild all 16 books on merged `main` with their saved `kdp.json` and confirm none has a page that doesn't fit (the pre-merge rebuild was on the reviewed code).

#### For CLAUDE.md

Replace the "each own line stays on one line (`KdpTextFit.LinesStayWhole`)" part of the cover-words bullet with: "Story text pages are always 24 pt (Core `KdpTrim.StoryTextPt`); lines wrap; a page that overflows its box is a report error and a non-zero exit, never shrunk."
