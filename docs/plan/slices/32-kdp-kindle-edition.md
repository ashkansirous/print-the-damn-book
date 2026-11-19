### Slice 32 — Amazon KDP: Kindle edition

*Planned 2026-11-19 from the Scope Inbox doc
[2026-11-18-amazon-kdp-export.md](https://drive.google.com/file/d/16B6KoIGXtHRXaRS8VvQcopP5RyJhcUIM/view?usp=drivesdk)
(milestone 6). Starts only when the owner says the first two paperbacks sold enough (the doc's kill rule, 2026-11-18:
"if the first two paperbacks sell close to nothing after a few months of ads, stop at English and don't build
milestones 6–7").*

**Touches:** `src/PrintTheDamnBook.Core/Kdp/KindleLayout.cs` (new), `src/PrintTheDamnBook.Kdp/Kindle/**` (new),
`src/PrintTheDamnBook.Cli/**`, `tests/PrintTheDamnBook.Core.Tests/Kdp/**`, `tests/PrintTheDamnBook.Kdp.Tests/**`

**Depends on:** Slices 27–28.

#### Why

A cheap extra edition (expect low sales; the Dino app stays the real digital product). The 70% royalty option charges
delivery per MB (£0.10/MB UK), so files must be small (~4–6 MB per book).

#### Decisions

- Approved (doc): fixed-layout images or EPUB built from the same pages; acceptance is Kindle Previewer showing it
  correctly.
- Open, settled at the start of this slice with `library-docs`/`web-research`: fixed-layout EPUB 3 vs KDP Kids' Book
  Creator input, and the image size that meets the file-size goal.

#### Work

- [ ] Kindle output from the same page plan and pictures, `--format kindle`.
- [ ] Size check in the report (fails above 6 MB).

#### Verification

- Freddy (English) under 6 MB; `[owner]` opens it in Kindle Previewer on this PC (or a session does, if Previewer can be
  driven by script) and the result is written here.
