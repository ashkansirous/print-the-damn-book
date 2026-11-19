### Slice 28 — Amazon KDP: print-ready pictures (cut-line flags, per-page offset, upscaler)

*Planned 2026-11-19 from the Scope Inbox doc
[2026-11-18-amazon-kdp-export.md](https://drive.google.com/file/d/16B6KoIGXtHRXaRS8VvQcopP5RyJhcUIM/view?usp=drivesdk)
(milestone 1).*

**Touches:** `src/PrintTheDamnBook.Core/Kdp/KdpPictureCrop.cs`, `src/PrintTheDamnBook.Core/Kdp/CutLineCheck.cs` (new),
`src/PrintTheDamnBook.Core/Kdp/KdpBookSettings.cs` (new), `src/PrintTheDamnBook.Kdp/Pictures/**` (new),
`src/PrintTheDamnBook.Kdp/KdpImageDecoder.cs`, `src/PrintTheDamnBook.Cli/**`, `tests/PrintTheDamnBook.Core.Tests/Kdp/**`,
`tests/PrintTheDamnBook.Kdp.Tests/**`

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

#### Work

- [ ] Crop rect with a vertical offset (clamped so the square stays inside the picture), unit tested.
- [ ] `CutLineCheck` scoring and threshold, unit tested on synthetic images (a shape crossing the line, a plain
  background); tuned on the 15 books' real pictures by a run that writes the scores into this slice.
- [ ] `kdp.json` through `KdpBookSettings`; `--offset` option; offsets applied in the renderer.
- [ ] Check report: per page source size, crop rect, scale factor, flag and preview path.

#### Verification

- Unit tests green on Windows and Linux CI.
- A run over all 15 books lists every flagged page; for Freddy the page images are exactly 2,588 × 2,625 px.
- `[owner]` Judge picture sharpness on the first printed proof (ordered from KDP after Slice 29); the answer is written
  here and settles the upscaler question.
