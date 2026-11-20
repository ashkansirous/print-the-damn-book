### Slice 28 — Amazon KDP: print-ready pictures (cut-line flags, per-page offset, upscaler)

*Planned 2026-11-19 from the Scope Inbox doc
[2026-11-18-amazon-kdp-export.md](https://drive.google.com/file/d/16B6KoIGXtHRXaRS8VvQcopP5RyJhcUIM/view?usp=drivesdk)
(milestone 1).*

*Built 2026-11-19 (wave 2026-11-19), merged as PR #72 (9acb8db) on 2026-11-19. Not yet verified: the 12 paid books and the printed proof (see below).*

**Touches:** `src/PrintTheDamnBook.Core/Kdp/KdpPictureCrop.cs`, `src/PrintTheDamnBook.Core/Kdp/CutLineCheck.cs` (new),
`src/PrintTheDamnBook.Core/Kdp/KdpBookSettings.cs` (new), `src/PrintTheDamnBook.Kdp/Pictures/**` (new),
`src/PrintTheDamnBook.Kdp/KdpImageDecoder.cs`, `src/PrintTheDamnBook.Kdp/KdpInterior.cs`,
`src/PrintTheDamnBook.Kdp/KdpCheckReport.cs`, `src/PrintTheDamnBook.Kdp/KdpExporter.cs`, `src/PrintTheDamnBook.Cli/**`,
`tests/PrintTheDamnBook.Core.Tests/Kdp/**`, `tests/PrintTheDamnBook.Kdp.Tests/**`

**Migrations:** none

*Premise check (wave 2026-11-19, origin/main 65c7c89): `KdpPictureCrop.For(int sourceWidth, int sourceHeight)`
(Core/Kdp/KdpPictureCrop.cs:19) has no offset; `KdpImageDecoder.Prepare(byte[])` (Kdp/KdpImageDecoder.cs:17) is called as
`Pictures.Select(KdpImageDecoder.Prepare)` (Kdp/KdpInterior.cs:43); the `kdp` parser (Cli/KdpCommandLine.cs:12-13, 37)
knows only `--language` and `--out` and rejects other options; the report is the static `KdpCheckReport`. Touches was
widened to the three Kdp files the offset and flags must pass through.*

**Depends on:** Slice 27.

#### Why

Owner, 2026-11-18: "The pictures should be cut correctly." The art is 1792 × 2400 (3:4). The square trim keeps the full
width and the top ~1,818 px (~76%), then needs a ~1.45× upscale to 2,588 × 2,625 px. The 70% rule (characters above
the 70% line) makes the crop mostly safe, but some pictures reach ~73–75% (doc, *The hard part*).

#### Decisions

- Approved (doc, 2026-11-18): the exporter crops and upscales at export time and **flags** any page where a character
  would be cut; a **manual offset per page** is offered and **never re-suggested over a user choice** (like Slice 16's
  band pick).
- Approved kill rule (doc): if upscaled pictures look soft in a printed proof, the fix is regenerating art at higher
  resolution in ReadTheStupidText, not more upscaling.
- Decided by this planning session, 2026-11-19:
  - The flag is computed in Core `CutLineCheck` from pixels the Kdp project hands over (the same split as Slice 16's
    `BandSuggestion`): detail (edge energy) in the strip just below the cut line compared with the band below it. A
    flagged page is listed in the check report with a small crop preview; the `kdp-visual-check` agent (Slice 30)
    confirms by eye.
  - Per-page offsets live in `kdp.json` next to the book's output (`{ "pages": { "7": { "cropOffsetPx": 60 } } }`),
    read and written only through Core `KdpBookSettings`; `--offset 7=60` on the command line writes it. A page with a
    saved offset is never flagged again over that choice.
  - Upscaler: the default is SkiaSharp's best resampler (Mitchell or Catmull-Rom cubic, the builder picks by
    side-by-side crops). An AI upscaler (local model, or a paid API) is added behind a `--upscaler` option only if the
    owner's printed proof (Slice 27/29) shows the default is soft. The upscaler question stays open until that proof.
  - Save order of `KdpExporter` (fixed, do not reorder again): build the document in memory, write `interior.pdf` and
    `check-report.md` (temp + rename), write the previews (stale ones removed), and only then write `kdp.json` (temp +
    rename); a failure before the last step leaves the old `kdp.json` untouched. `kdp.json` records the book's identity
    (`DinoBook.Id` and title); a file whose id differs from the book is a `KdpSettingsException` (exit 1, never
    overwritten) telling the owner to use another `--out` folder; a file with no recorded id is accepted and gets one on
    its next save. An offset with no effect is warned about once per page; the stale-entry check uses the picture count.

#### Work

- [x] Crop rect with a vertical offset (clamped so the square stays inside the picture), unit tested.
- [x] `CutLineCheck` scoring and threshold, unit tested on synthetic images (a shape crossing the line, a plain
  background, textured art with no character, a band too thin to compare).
- [ ] Threshold tuned on the 15 books' real pictures by a run that writes the scores into this slice: done on the 4 free
  store books (62 pictures); the 12 paid books (HTTP 402 on download) were not reached, see *Built*.
- [x] `kdp.json` through `KdpBookSettings`; `--offset` option; offsets applied in the renderer.
- [x] Check report: per page source size, crop rect, scale factor, flag and preview path.

*Built (wave 2026-11-19): `kdp` run on every store book that downloads without payment (plain GET of the catalog's `hdDownloadUrl`
where it has one, else `downloadUrl`; saved under the git-ignored `tmp/store/`). Cut-line score per picture (threshold 2.5, strip
energy at least 6):*

- *The Lion and the Mouse (HD, 15 pictures of 1792 x 2400): 0.3 to 1.6, none flagged.*
- *X Marks the Treasure! (HD, 17 pictures of 1792 x 2400): 0.6 to 2.0, page 16 flagged at 2.9 (called a real cut here; corrected 2026-11-20: the live check of Slice 30 looked at the same page and found the line runs through sand and sticks below everyone's feet, nothing cut, so this is a false flag; re-tuning is [Slice 35](35-kdp-followups.md) item A).*
- *The Gingerbread Man (standard, 14 pictures of 896 x 1200): 0.3 to 2.0, pages 9, 10, 11, 12 flagged at 4.2, 4.8, 4.9, 3.3 (page 12 seen: the grandparents' feet cross the line).*
- *The Three Little Pigs (standard, 16 pictures of 896 x 1200): 0.6 to 2.3, pages 1, 10, 11, 15 flagged at 3.2, 2.5, 2.8, 3.0 (page 10 seen: the wolf's feet are cut). Page 13 scores 2.3 and is clean (rug edge only, seen).*

*Result: 9 of 62 pictures flagged; the 3 flagged pages looked at are real cuts and the highest clean page looked at (2.3) is correctly
left alone, so `CutLineCheck.FlagRatio` stays 2.5. The margin is thin (2.3 clean to 2.5 cut), and the unflagged pages scoring 1.9 to 2.0
(X Marks the Treasure! pages 2 and 8, Gingerbread Man page 14) and the other 6 flagged pages were not looked at. The standard-size books
are 896 px wide (about 104 dpi on the page), so the soft-picture warning fires for them; only HD books reach about 208 dpi.*

*Not reached (12 paid books, the store answers 402): Goldilocks and the Three Bears, The Little Red Hen, Love from Seagull Island,
Freddy and the Slipper, Good Enough, The Tortoise and the Hare, Jack and the Beanstalk, Little Red Riding Hood, The Elves and the
Shoemaker, The Three Billy Goats Gruff, The Town Mouse and the Country Mouse, The Ugly Duckling. Freddy's page size of exactly
2,588 x 2,625 px is covered by the unit test only, not by a run on that book.*

*Upscaler: side-by-side of the same 320 px crop enlarged 1.44x from two Lion and the Mouse pictures (a throwaway console project under
the git-ignored `tmp/resample/`): Catmull-Rom is visibly sharper than Mitchell with no ringing seen, and a down-and-up round-trip PSNR
against the original is 37.62 vs 36.42 dB (page 3) and 38.73 vs 37.22 dB (page 8). The enlarging resampler is now Catmull-Rom.*

#### Verification

- Unit tests green on Windows and Linux CI.
- A run over all 15 books lists every flagged page; for Freddy the page images are exactly 2,588 × 2,625 px. **Not met:**
  4 free books run (see *Built*); `[owner]` the 12 paid books (the store answers 402) and Freddy's exact page size need a
  run with the owner's copies of those `.dino` files, then the threshold is re-checked against their scores.
- `[owner]` Judge picture sharpness on the first printed proof (ordered from KDP after Slice 29); the answer is written
  here and settles the upscaler question.

*Checks 2026-11-19: build 0 errors; tests 735 pass (Core 607, App 77, Kdp 51); `dotnet format` clean. CLI on The Lion and the Mouse (HD): `--offset 3=40` exits 0, the report row says "chosen offset 40 px" and `kdp.json` holds the book id and page 3; `--offset 3=0` clears it; `--offset 99=40` exits 1 with a clear error; a `kdp.json` from another book id exits 1 and is left as it is. The run logs were in the worktree's `tmp/` and were not kept.*

#### From review

- Applied #1-#7 (`reviews/pr-72-high.md`): all seven findings applied.
- Applied #1, #2, #3 (`reviews/pr-72-medium.md`): `--offset` for a page without a picture is a `KdpException` (exit 1); previews are written only after the PDF and report are in place; a `kdp.json` entry with no `cropOffsetPx` is refused as a broken file.
- Skipped #4 (`reviews/pr-72-medium.md`): "Thresholds (ratio 2.5, energy floor 6) only tested" on synthetic images: the textured-art false-positive test and the tuning scores were added for the high review's findings; further tuning needs the 12 paid books (see Verification).
- Applied #1, #2, #3, #4 (`reviews/pr-72-check.md`): `kdp.json` is written last, after the PDF, report and previews; it records the book id and title and a different id is refused (exit 1); an offset with no effect is warned about once; the stale-entry check uses the picture count.
- Skipped #5 (`reviews/pr-72-check.md`): "WritePreviews runs after WriteBoth; failure leaves report pointing at stale or missing previews." - previews are written right after the outputs and stale ones are removed in the same step, so a mismatch needs a failure inside that one step.
- Skipped #6 (`reviews/pr-72-check.md`): "CreateDirectory moved before the build, so a failed run leaves an empty out folder." - an empty folder holds no wrong data and the next run reuses it.
- Follow-ups after the second check pass (`reviews/pr-72-check2.md`, 5 of 5 read, none applied, no blocking finding); each is a planned improvement, not done in this slice:
  - `src/PrintTheDamnBook.Core/Kdp/CutLineCheck.cs:49`: a strip or band too thin to compare returns `CutLineScore.None` and the report prints "ok (score 0.0)" for a check that never ran; add a "not checked" state and print it.
  - `src/PrintTheDamnBook.Kdp/KdpExporter.cs:47`: `SaveOffsets` runs last, so a locked or read-only `kdp.json` leaves the PDF and report replaced (the report says the offset was chosen) while the run exits with an error and the choice is lost; write `kdp.json` first or warn instead of throwing.
  - `src/PrintTheDamnBook.Kdp/KdpExporter.cs:91`: `ReadStored` calls `EnsureBook` on every run, so a `kdp.json` from another book in a reused out folder fails a plain run with no PDF (it worked in Slice 27); enforce identity only when entries would apply, else warn and do not overwrite.
  - `src/PrintTheDamnBook.Core/Kdp/KdpBookSettings.cs:62`: `EnsureBook` accepts a file with entries but no book id, so a hand-written or old `kdp.json` is applied to whatever book is exported next; warn, and stamp the id on the next successful run.
  - `src/PrintTheDamnBook.Kdp/KdpInterior.cs:45`: `settings.OffsetFor(i + 1)` keys by index in `pictures`, not by story page; when story pages are left out (`book.SkippedPages > 0`) offsets and check rows can point at the wrong picture; key by the real story index or refuse offsets when pages were left out.
  - Found in the check run (no review finding): when two books share a title, the id-mismatch error names the same title twice, so the message does not tell the books apart.

#### For CLAUDE.md (lifted 2026-11-19)

- The cut-line flag and the crop offset maths live in Core `Kdp/` (`CutLineCheck`, `KdpPictureCrop`, `KdpBookSettings`); the Kdp project only decodes and hands over pixels. (Slice 28: docs/plan/slices/28-kdp-print-ready-pictures.md)
- `kdp.json` in the output folder is the owner's per-page offset record, keyed to the book id. It is written last (after the PDF, report and previews) and never overwritten when unreadable or from another book. (Slice 28: docs/plan/slices/28-kdp-print-ready-pictures.md)

#### For README.md (lifted 2026-11-19)

- `kdp` command: `--offset <page>=<px>` (repeatable; `=0` clears) moves the square crop down on that page; the choice is saved in `kdp.json` in the output folder.

#### Not yet verified

- `[owner]` Run the kdp command over the 12 paid books (the owner's own .dino copies) and confirm `CutLineCheck.FlagRatio` 2.5 on them.
- `[owner]` The printed proof decides whether an AI upscaler is needed (settles the upscaler question).
