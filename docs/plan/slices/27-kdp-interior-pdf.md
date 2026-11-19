### Slice 27 — Amazon KDP: interior PDF for one Dino book, from a cross-platform command line

*Planned 2026-11-19 from the Scope Inbox doc
[2026-11-18-amazon-kdp-export.md](https://drive.google.com/file/d/16B6KoIGXtHRXaRS8VvQcopP5RyJhcUIM/view?usp=drivesdk)
(milestone 2, plus the plain crop of milestone 1 and the doc's "First step").*

*Built 2026-11-19 (wave 2026-11-19), PR #67, not merged; item E built from a stand-in book (Freddy is a paid store book), not yet verified on KDP.*

**Touches:** `src/PrintTheDamnBook.Core/Kdp/**` (new), `src/PrintTheDamnBook.Kdp/**` (new),
`src/PrintTheDamnBook.Cli/**` (new), `tests/PrintTheDamnBook.Core.Tests/Kdp/**` (new),
`tests/PrintTheDamnBook.Kdp.Tests/**` (new), `PrintTheDamnBook.slnx`, `.github/workflows/ci.yml`,
`Directory.Packages.props` (if the repo uses central package versions; otherwise the new csproj files only)

**Depends on:** nothing. Slices 28–33 build on it.

#### Why

The user wants the 15 Dino books on Amazon as paperbacks (owner, 2026-11-18: "I have to convert my books to something
Amazon would accept. The pictures should be cut correctly and everything should be on the right place, page numbers
and alike."). Today's output is a saddle-stitch booklet on A4/Letter through System.Printing, which KDP cannot use: KDP
wants single pages in reading order at the trim size, with bleed, in one PDF. This slice is the smallest end-to-end
piece: `.dino` in, KDP interior PDF out, for Freddy and the Slipper in English, built on Linux or Windows.

#### Decisions (approved by the owner in the Scope Inbox doc, 2026-11-18)

- **Trim** 8.5 × 8.5 in square, premium colour, bleed on. Second choice on file: custom 6 × 8 (keeps 3:4 art; KDP
  acceptance unconfirmed). Fallback if 8.5 × 8.5 causes problems: 8 × 10.
- **Where it runs:** a cross-platform command-line tool in this repo that reuses Core, so agents can build books in the
  cloud. This lifts the plan's "macOS or Linux builds" out-of-scope line **for this tool only**; the WPF app stays
  Windows-only.
- **Who crops:** the exporter, at export time (it knows trim and bleed). This lifts the "Trim marks, bleed, crop marks"
  out-of-scope line **for KDP output only**.
- **First book:** Freddy and the Slipper, English. The owner uploads to KDP by hand (no KDP upload API found).

#### Decisions (decided by this planning session, 2026-11-19, from the grounding below)

- **Libraries:** QuestPDF (Community licence: free for individuals and businesses under USD 1M gross revenue a year,
  commercial use included; https://www.questpdf.com/license/community.html) for the PDF, and SkiaSharp (with
  `SkiaSharp.NativeAssets.Linux`) for decoding, EXIF orientation, crop and resample. QuestPDF draws with SkiaSharp, so
  there is one decoder path. Latest stable on NuGet on 2026-11-19: QuestPDF 2026.9.2, SkiaSharp 4.157.0. The builder
  re-checks the versions and APIs through `library-docs` (context7) before writing code. PDFsharp was rejected (no text
  shaping, needed later for Farsi/Arabic); ImageSharp was rejected (a second decoder, Six Labors split licence).
- **Projects:** layout maths (page sizes, bleed, safe margins, the page plan, padding) goes in **Core `Kdp/`**, unit
  tested, per the root rule "all imposition and layout maths lives in Core". `src/PrintTheDamnBook.Kdp` (net10.0, new)
  holds the only drawing code (QuestPDF) and the only image decoding for KDP (`Kdp/KdpImageDecoder`, one place, in the
  spirit of the App's `Services/ImageDecoder` rule; the App keeps its own WPF decoder). `src/PrintTheDamnBook.Cli`
  (net10.0 console, new, assembly name `print-the-damn-book`) only parses arguments and calls Kdp.
- **Fonts:** the Andika and Vazirmatn `.ttf` files stay in `src/PrintTheDamnBook.App/Fonts/`; the Kdp project links
  them as `EmbeddedResource` (`Include="..\PrintTheDamnBook.App\Fonts\Andika-Regular.ttf"` etc.), so there is one copy.
  The PDF embeds them (QuestPDF/Skia subset embedding; the builder checks the output with `pdffonts` or an equivalent
  that every font is embedded, since KDP rejects unembedded fonts).
- **Page plan (Core `KdpPagePlan`):** page 1 title (right-hand page), page 2 copyright, page 3 dedication / "This book
  belongs to", then one **spread per story page: text on the left (even) page, picture on the right (odd) page**, so
  each text faces its picture; then The End and the about page ("hear this story in Dino: Kids Books & Stories");
  padded with blank pages before The End to an even count, at least 24. The doc's count formula (4 + 2 × pictures + 2,
  rounded to even: 13 → 32, 15 → 36, 17 → 40) is kept as the upper bound; three front pages instead of four is a desk
  choice so the spreads face. Over 40 pages is allowed but the check report warns (it leaves KDP's flat-price band).
  Right-to-left books mirror the spread (picture left, text right) through Core `TextDirection` (used from Slice 33 on).
- **Reuse, not re-derive:** reading direction from `TextDirection`, font from `StoryFont.For`, page numbers from
  `PageNumbering.Compute` on the KDP page list (not `PageList.Padded`, whose `Imposition.Pad` pads to a multiple of 4
  for saddle-stitch), text-size steps and the fit choice from `StoryTextLayout.Choose` / `PageStepsPt`. Line breaking
  and measuring is done by the engine that draws (QuestPDF), the same split the WPF `StoryPainter` has today
  (`StoryPainter.cs:55,62` measures, Core chooses). `.dino` is read only through `DinoBookReader`.
- **Picture in this slice:** a plain crop to the square (full width, keep the top, drop the bottom text band) and a
  high-quality resample to exactly 2,588 × 2,625 px (8.625 × 8.75 in at 300 dpi). Flagging, per-page offsets and the
  upscaler choice are Slice 28.
- **Story text:** on the text page only, never on the picture (the picture's own text band is cut off). Page
  numbers on text pages, bottom centre, inside the safe margin; none on title, copyright, dedication, blanks, pictures.

#### Decisions (wave 2026-11-19, from the premise check on origin/main ed0e03c)

- **Text-size steps:** `StoryTextLayout.PageStepsPt(PaperKind)` knows only A4 and Letter (`LayoutTypes.cs:9`), so the
  KDP steps are a Core constant `KdpTrim.TextStepsPt` fed to `StoryTextLayout.Choose(stepsPt, fits)`; `PaperKind` and
  `StoryTextLayout` are not changed.
- **Page numbers:** a printed book's page number is its physical position (title page = 1). Numbers come from
  `PageNumbering.Compute` over the KDP page list mapped to `BookPage`s if that yields the physical position for every
  text page; if it does not (its Cover/`IsUnnumbered` rules), Core `KdpPagePlan` gives each page `Number = position`
  and the builder writes why in this file. Either way they are shown on text pages only.
- **Getting the `.dino`:** `DinoBookCache` and `DinoStoreClient` are App-only (net10.0-windows), so the command line
  takes a local `.dino` path only. For item E the builder fetches Freddy's `.dino` with a plain GET from the store
  catalog (`DinoStoreCatalog`, host dinoreader.sirous.uk; no identifiers) into a git-ignored folder, or, if Freddy is
  not in the catalog, builds the first English store book and writes which one here.
- **CI:** the ubuntu job builds and tests the Core, Kdp, Cli and test projects by path, never the `.slnx` (the App is
  net10.0-windows). On Linux it runs all of Kdp.Tests and only the Core tests under the
  `PrintTheDamnBook.Core.Tests.Kdp` namespace (MTP `--filter-namespace`); the full Core.Tests stays on the Windows job.
  (Wave 2026-11-19, after both reviews found existing Core tests with Windows paths, `HandoverArgumentsTests` then
  `PageListTests`: excluding classes one by one cannot be shown complete without a Linux run, a namespace filter can.)
- **Tests of internals:** `InternalsVisibleTo` for `PrintTheDamnBook.Kdp.Tests` goes in the Kdp csproj; Kdp.Tests copies
  the existing test csproj shape (xunit.v3.mtp-v2 3.2.2, `xunit.runner.json`).

#### Grounding (ground-in-code on origin/main 416a274, 2026-11-19)

- Core is plain `net10.0` with no package references (Core.csproj); nothing Windows-only. CI (`ci.yml:14-22`) builds and
  tests only on `windows-latest`.
- `StoryPainter.cs` (WPF) does line breaking and measuring with `FormattedText` (line 112) and loads fonts via pack URIs
  (line 28); Core `StoryTextLayout` gives the geometry, steps and `Choose` (StoryTextLayout.cs:57-110). `FaceRenderer` is
  tied to the half-sheet; nothing of it is reused.
- `DinoBookReader.Read(Stream)` returns `DinoBook(Id, Title, Language, CoverFile, Pages[], Translations, SkippedPages,
  BackCover)` (Core/Dino/DinoBook.cs:21-28); `ExtractPictures` writes the pictures and returns `DinoPictures`
  (DinoBookReader.cs:9,79). Pictures are read as they are: no pixel-size check, no high-res variant. The doc says the art
  is 1792 × 2400 (3:4).
- `PageNumbering.Compute(IReadOnlyList<BookPage> padded)` numbers by position and is not tied to a paper size
  (PageNumbering.cs:56). `Imposition.Pad` pads to a multiple of 4 (Imposition.cs:15).
- `.claude/` holds only `settings.json` (no agents or skills).

#### Work

- [x] **A — Core page plan.** `Core/Kdp/KdpTrim` (8.5 × 8.5, bleed 0.125 in on width and 0.25 in on height → 8.625 ×
  8.75 in page; safe margin constant with its KDP source link), `Core/Kdp/KdpPagePlan` (the plan above, from a
  `DinoBook` page count and `ReadingDirection`), `Core/Kdp/KdpPictureCrop` (source size → crop rect for the square,
  top-anchored, and the target pixel size). Unit tests: 13 / 15 / 17 pictures give 32 / 36 / 40 pages or fewer, every
  text page faces its picture, even count, ≥ 24, blanks only before The End, numbers only on text pages.
- [x] **B — Kdp renderer.** `PrintTheDamnBook.Kdp` with `KdpImageDecoder` (SkiaSharp decode, EXIF orientation, crop,
  resample to the exact pixel size), `KdpFonts` (embedded Andika/Vazirmatn registered with QuestPDF), and
  `KdpInteriorDocument` (QuestPDF: page size from `KdpTrim`, zero margin, picture full bleed, text page with the Core
  text box and font size chosen by `StoryTextLayout.Choose` with the fit measured by QuestPDF, page numbers from
  `PageNumbering`). Front and back matter wording (title, copyright line with year and credits from `DinoCredits`,
  dedication, The End, about page) comes from a Core `Kdp/KdpFrontMatterWording`, English only in this slice.
  QuestPDF licence set once (`QuestPDF.Settings.License = LicenseType.Community`, confirmed via library-docs).
- [x] **C — Command line.** `print-the-damn-book kdp <book.dino> --language en --out <folder>` writes `interior.pdf` and
  `check-report.md` (page count, each page's role, picture source and target size, fonts embedded, warnings). Exit code
  non-zero on any error. A `.dino` is untrusted: only `DinoBookReader`, its caps apply.
- [x] **D — Linux in CI.** A `ubuntu-latest` job in `ci.yml` that builds Core, Kdp, Cli and runs Core.Tests and Kdp.Tests
  (the App and App.Tests stay on Windows). Kdp.Tests renders a small synthetic book (pictures drawn in code, no
  committed user art) and checks the PDF's page count, page size and embedded fonts.
- [x] **E — Freddy draft.** Build Freddy and the Slipper (English) on this PC from the store's `.dino` (fetched through
  `DinoStoreClient`'s catalog, or the copy in the Dino cache), keep the output outside git, and record the check
  report in this slice file. *Built from the stand-in The Lion and the Mouse, because Freddy is a paid store book
  (download refused with 402); the Freddy build itself is still open, see Not yet verified.*

#### Built notes (implement-slice, wave 2026-11-19)

- **Page numbers:** Core `KdpPagePlan` gives each page `Number = position` (text pages only). `PageNumbering.Compute` was not used: its Cover and
  Blank/Spacer rules answer for a saddle-stitch booklet and it needs `BookPage`s the KDP plan does not have. Digits and the printed form still come
  from `PageNumbering.DigitsFor` and `PageNumbering.Format`.
- **Page size:** QuestPDF snaps the page to whole raster pixels, so `ImageRasterDpi` stays at its default 288 (621 x 630 pt exactly; at 300 the
  width became 2,588 px = 621.12 pt). Pictures are embedded as their own JPEG (`UseOriginalImage`) at exactly 2,588 x 2,625 px.
- **Fit:** QuestPDF has no measuring call, so `KdpTextFit` sets the words in a one-page document of the box's size with `ShowEntire()` and catches
  `DocumentLayoutException`; Core `StoryTextLayout.Choose` picks the size from `KdpTrim.TextStepsPt` (24, 22, 20, 18, 16, 14 pt). Words that fit at no
  size are an error (exit 1).
- **Fonts embedded:** the PDF is written uncompressed (`CompressDocument = false`) and `KdpPdfInspector` counts `/FontDescriptor` entries with a
  `/FontFile`; the build throws when any is missing. Kdp.Tests also reads the PDF back with PdfPig (page count, size, image pixels, subset font names).
- **Safe margin:** `KdpTrim.SafeMarginInches` is 0.5 in; the KDP help link in its comment (topic G201834190) was written from memory and not opened.
- **Not run:** the `kdp-linux` job in `ci.yml` (no Docker daemon here, no Linux host); the same commands are green on Windows.

**Item E (Freddy draft), what was actually built.** Freddy and the Slipper is in the store catalog but `access` is `paid`: a plain GET of its
download URL answers `402 Payment Required` (checked 2026-11-19), and so does every other `paid` book, Goldilocks (the first English entry) included.
The first English book a plain GET can fetch is **The Lion and the Mouse** (`access: free`, 15 pages), so the draft is that one, built from the store's
HD download (`downloadUrl` plus `&size=hd`, the catalog's `hdDownloadUrl`: 1,792 x 2,400 px pictures; the normal download has 896 px). Output is in the
git-ignored `tmp/lion-hd-out/` of the worktree. A Freddy `.dino` needs the owner's own copy (the Dino BookBuilder's output) and is still to do.

Check report of that run (`print-the-damn-book kdp lion-hd.dino --language en --out tmp/lion-hd-out`, exit 0), checked again with a separate PdfPig
reader: 36 pages, every page 8.625 x 8.75 in, 15 images all 2,588 x 2,625 px, one font (`AAAAAA+Andika`) embedded as a subset, every even page 4-34 is
the words (number = its position) with its picture alone on the next odd page. Warning: "15 of 15 pictures are enlarged to 2588 px across; the lowest
has 1792 px, about 208 dpi on the page (KDP asks for 300)" (Slice 28's upscaler). With the normal 896 px download it is about 104 dpi.

**Found, not fixed (outside Touches):** `DinoBackCoverReader` (`Core/Dino/DinoBackCover.cs`) reads `storyBy`, `month`, `retold`, `illustration` and
`namesByLanguage` with `JsonElement.TryGetProperty`, which is case-sensitive, but the real store file writes `StoryBy`, `Month`, `Retold`, ... in
PascalCase; so `DinoBook.BackCover.Credits` is null for the real Lion book (picture found, credits dropped) and the KDP copyright page shows only the
`(c) year name` line, without the credit lines. Likely the App's back cover loses its credit lines for real books too. Needs a Core fix and a test on a
real-shaped story.json (planned as Slice 34 in `plan.md`).

**Checks 2026-11-19:** build 0 warnings; tests 688 pass (Core 580, Kdp 31, App 77); `dotnet format` clean; the command line on `lion.dino` exits 0.

#### Verification

- `dotnet test PrintTheDamnBook.slnx` green on Windows; the new Linux CI job green.
- The Freddy `interior.pdf`: page size 8.625 × 8.75 in on every page, every picture page image 2,588 × 2,625 px, all
  fonts embedded, text pages face their pictures (checked by the run, written in the check report).
- `[owner]` Upload the Freddy `interior.pdf` as a paperback draft (8.5 × 8.5, premium colour, bleed) at
  https://kdp.amazon.com/en_US/bookshelf and run KDP's previewer; every warning it gives is written into this slice
  (it tells Slices 28–29 what KDP checks). Needs the owner's KDP login.

#### Not in this slice

Crop flagging, per-page offsets and the upscaler (28); cover PDF (29); skills and agents (30); listing pack (31);
Kindle (32); other languages and right-to-left (33); release artifacts for the command line (cloud agents run it with
`dotnet run`).

#### From review

- Skipped #8 (`reviews/pr-67-high.md`): "src/PrintTheDamnBook.Cli/KdpCommandLine.cs:19, nit. Global rule" - method-length style only, no failure path.
- Skipped #10 (`reviews/pr-67-high.md`): "the caller should confirm it names context7 library ids for the csproj/CI condition and the Linux native-assets package" - not a defect: the builder's report named context7 ids /questpdf/questpdf, /mono/skiasharp, /uglytoad/pdfpig.
- #1: `HandoverArgumentsTests` (outside this slice's Touches) uses Windows paths, so the Linux CI job runs Core.Tests with `--filter "FullyQualifiedName!~HandoverArgumentsTests"`.
- Skipped #2 (`reviews/pr-67-medium.md`): "Copyright year from DateTime.UtcNow inside Export; rebuild after New Year changes the PDF. Fix: put the year on KdpExportRequest or take a clock." - the year should be the build year for a new KDP edition; a rebuild in another year is meant to change it.
- Skipped #4 (`reviews/pr-67-medium.md`): "At the 50M pixel cap memory roughly triples (raw, Orient copy, Resample copy); Orient copies even for TopLeft. Fix: return raw for TopLeft; lower MaxPixels (e.g. 25M)." - the 50M total-pixel cap already bounds memory; real art is 1792 x 2400 and no failure path is shown.
- Applied pass 2 (`reviews/pr-67-medium.md`): #1 supersedes the earlier `--filter` line: the Linux job runs Core.Tests with `--filter-namespace "PrintTheDamnBook.Core.Tests.Kdp"` and all of Kdp.Tests.
- Third review pass (`reviews/pr-67-check.md`), no fix applied: #1 (the Linux `--filter-namespace`) is proven by the PR's own `kdp-linux` CI run; #2-#8 are nits, kept as follow-ups below.

#### Follow-ups (nits from `reviews/pr-67-check.md`, not fixed)

- #2 `kdp-linux` has no `timeout-minutes` (`.github/workflows/ci.yml:24`).
- #3 `WriteBoth` moves the PDF and the report in two calls (`KdpExporter.cs:43`).
- #4 The `.dino` is opened twice, in `ReadBook` and `ReadPictures` (`KdpExporter.cs:22`).
- #5 Repeated `--out` / `--language` overwrite silently (`KdpCommandLine.cs:22`).
- #6 `Orient` allocates a second full-size bitmap even for TopLeft (`KdpImageDecoder.cs:43`).
- #7 Only `DocumentLayoutException` is caught in `Fits` (`KdpTextFit.cs:21`).
- #8 `--help` prints usage on stderr and exits 2 (`KdpRunner.cs:15`).

#### For CLAUDE.md

- KDP layout maths (trim, bleed, page plan, crop) lives in Core `Kdp/`; drawing and image decoding only in `PrintTheDamnBook.Kdp` (`KdpImageDecoder`); the CLI `PrintTheDamnBook.Cli` only parses arguments and calls Kdp. (Slice 27: docs/plan/slices/27-kdp-interior-pdf.md)
- Kdp fonts link the App's `Fonts/` files as embedded resources; never add a second copy. (Slice 27: docs/plan/slices/27-kdp-interior-pdf.md)
- Linux CI (`kdp-linux`) builds and tests the Core, Kdp, Cli and Kdp.Tests projects by path, never the `.slnx`, and runs only Kdp.Tests plus the Core `Kdp` test namespace. (Slice 27: docs/plan/slices/27-kdp-interior-pdf.md)

#### For README.md

- Build a KDP interior PDF from a `.dino` on Windows or Linux: `dotnet run --project src/PrintTheDamnBook.Cli -- kdp <book.dino> --language en --out <folder>` (the exe is `print-the-damn-book kdp <book.dino> --language en --out <folder>`); it writes `interior.pdf` and `check-report.md`.

#### Not yet verified

- `[owner]` A Freddy and the Slipper `.dino` from the owner's own copy (paid store book, download refused), then build its interior with the command.
- `[owner]` Upload the interior PDF as a KDP paperback draft at https://kdp.amazon.com/en_US/bookshelf and record the previewer warnings.
