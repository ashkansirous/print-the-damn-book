### Slice 35 — Amazon KDP follow-ups from Slices 27-31 reviews and the 2026-11-20 live check

*planned.* Written by the feedback loop of night 2026-11-19-2055
([note](../notes/2026-11-19-2055-feedback-loop.md)). The review findings below were kept only under each built slice's
*Follow-ups* list, which no wave builds; this slice gathers them so the next night does. Each item names its source list:
build every line of that list unless the item says otherwise (the source slice file wins on wording).

**Touches:** `src/PrintTheDamnBook.Core/Kdp/`, `src/PrintTheDamnBook.Kdp/`, `src/PrintTheDamnBook.Cli/`,
`tests/PrintTheDamnBook.Kdp.Tests/`, `tests/PrintTheDamnBook.Core.Tests/`, `.github/workflows/ci.yml`,
`.claude/skills/kdp-build/`, `.claude/agents/kdp-visual-check.md`, `.claude/skills/kdp-listing/`,
`kdp/listings/freddy-and-the-slipper/en.md`, `docs/plan/slices/35-kdp-followups.md`

**Migrations:** none

**Depends on:** nothing (Slice 34 stays parked; item G's books show the title-only back cover until it is built).

#### Work items

- [ ] **A. The live check of 2026-11-20 failed** ([Slice 30](30-kdp-build-skill.md), *Follow-up (not built), live check
  2026-11-20 FAILED*, its points 1-4): the cut-line flag (`CutLineCheck`, `FlagRatio` 2.5) fires on uncut pictures in free
  books; the skill cannot end clean when the visual check overrules a flag (give it a recorded dismissal the report
  honours); story page to book page mapping in the report or previews (the session passed page 31 for Pigs story 15,
  should be 33); preview the cover too. Verify: a fresh `claude -p` run of `kdp-build` on The Three Little Pigs and X
  Marks the Treasure ends with a clean report or only owner questions.
- [ ] **B. Slice 28 second check pass** ([Slice 28](28-kdp-print-ready-pictures.md), every follow-up after the second
  check pass): a "not checked" state for strips too thin to compare; a locked `kdp.json` must not lose the chosen offset;
  a `kdp.json` from another book in a reused out folder must not fail a plain run; and first, since it can print a crop
  on the wrong page, `KdpInterior.cs:45` keys offsets by picture index, not story page, when pages are skipped.
- [ ] **C. Slice 29 skipped findings** ([Slice 29](29-kdp-cover-pdf.md)): check pass 3 findings 1-7
  (`pr-76-check3-medium.md`), and from the earlier passes check #4 (the credits box has no fit check), check2 #3
  (`HasSpineText` uses `>= 79` where KDP says more than 79), check2 #4 (blank title guard) and check2 #6 (tests for the
  `StaleCover.Kept` branch, a right-to-left book through `Export`, the `BuildCover` exception wrap, the spine-text report
  branch).
- [ ] **C2.** A test for Slice 28's `SaveOffsets` guard (the fixer added only an assertion to the page-9 test; wave 1
  entry 730).
- [ ] **D. Slice 27 nits #2-#8** ([Slice 27](27-kdp-interior-pdf.md), *Follow-ups*), including `timeout-minutes` on `kdp-linux`.
- [ ] **E. Slice 31 follow-ups** ([Slice 31](31-kdp-listing-pack.md), *Follow-ups* and skipped check #6, #7): royalty
  marked unconfirmed, trim and ink read from the listing, the listing checker's Node tests run in CI.
- [ ] **F.** Contact-sheet labels may be blank on Linux without a default font ([Slice 30](30-kdp-build-skill.md), medium #3):
  load Andika from the embedded fonts for the labels.
- [ ] **G. Build the paid books locally, not from the owner** (feedback loop 2026-11-19-2055, audit of wave 1 entries
  363, 470, 1143): every store book's source is in `D:\ReadTheStupidText\content\dino\<book>`, and ReadTheStupidText's
  `dotnet run --project tools/dino/DinoReader.BookBuilder -- build <stem>` builds the package (an hd `.dino` built this way
  is at `D:\ReadTheStupidText\artifacts\print\`). Write a script in `.claude/skills/kdp-build/` that builds a book's
  `.dino` into the repo's ignored `tmp/` (after `git -C D:\ReadTheStupidText lfs pull --include` of its `high-res`; not
  checked whether the audio packs are needed), then run the kdp command over Freddy and the 12 paid books and record the
  cut-line flag counts in this file. Verify: Freddy's `interior.pdf` exists and its page count is written next to Slice
  31's assumed 34. Never commit book content.

- [ ] **H. Confirm the KDP limits no Amazon page confirmed yet** ([Slice 31](31-kdp-listing-pack.md) lines 44 and 61):
  the 8.5 x 8.5 in trim, the keyword character limit and the supported description HTML tags. Read Amazon's KDP help
  pages (web, no login), cite each with its date in `.claude/skills/kdp-listing/listing-limits.mjs`, fix any figure that
  differs. Verify: `check-listing.mjs` passes on the Freddy listing with the cited figures.
- [ ] **I. Make the Windows-path Core tests portable** (wave 1 entries 509-541): `HandoverArgumentsTests` and
  `PageListTests` use Windows paths and fail on Linux, which is why `kdp-linux` runs only the Core `Kdp` namespace.
  Low priority; build them with `Path.Combine` or skip on non-Windows with a reason. Verify: the full Core test project
  passes on Linux CI, then widen the `kdp-linux` filter in a separate change only if wanted.

#### Not yet verified

(none yet; items A, G and H are the checks.)
