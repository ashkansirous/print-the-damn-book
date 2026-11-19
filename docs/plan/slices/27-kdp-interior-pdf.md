### Slice 27 — Amazon KDP: interior PDF for one Dino book, from a cross-platform command line

*Planned 2026-11-19 from the Scope Inbox doc
[2026-11-18-amazon-kdp-export.md](https://drive.google.com/file/d/16B6KoIGXtHRXaRS8VvQcopP5RyJhcUIM/view?usp=drivesdk)
(milestone 2, plus the plain crop of milestone 1 and the doc's "First step").*

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

- [ ] **A — Core page plan.** `Core/Kdp/KdpTrim` (8.5 × 8.5, bleed 0.125 in on width and 0.25 in on height → 8.625 ×
  8.75 in page; safe margin constant with its KDP source link), `Core/Kdp/KdpPagePlan` (the plan above, from a
  `DinoBook` page count and `ReadingDirection`), `Core/Kdp/KdpPictureCrop` (source size → crop rect for the square,
  top-anchored, and the target pixel size). Unit tests: 13 / 15 / 17 pictures give 32 / 36 / 40 pages or fewer, every
  text page faces its picture, even count, ≥ 24, blanks only before The End, numbers only on text pages.
- [ ] **B — Kdp renderer.** `PrintTheDamnBook.Kdp` with `KdpImageDecoder` (SkiaSharp decode, EXIF orientation, crop,
  resample to the exact pixel size), `KdpFonts` (embedded Andika/Vazirmatn registered with QuestPDF), and
  `KdpInteriorDocument` (QuestPDF: page size from `KdpTrim`, zero margin, picture full bleed, text page with the Core
  text box and font size chosen by `StoryTextLayout.Choose` with the fit measured by QuestPDF, page numbers from
  `PageNumbering`). Front and back matter wording (title, copyright line with year and credits from `DinoCredits`,
  dedication, The End, about page) comes from a Core `Kdp/KdpFrontMatterWording`, English only in this slice.
  QuestPDF licence set once (`QuestPDF.Settings.License = LicenseType.Community`, confirmed via library-docs).
- [ ] **C — Command line.** `print-the-damn-book kdp <book.dino> --language en --out <folder>` writes `interior.pdf` and
  `check-report.md` (page count, each page's role, picture source and target size, fonts embedded, warnings). Exit code
  non-zero on any error. A `.dino` is untrusted: only `DinoBookReader`, its caps apply.
- [ ] **D — Linux in CI.** A `ubuntu-latest` job in `ci.yml` that builds Core, Kdp, Cli and runs Core.Tests and Kdp.Tests
  (the App and App.Tests stay on Windows). Kdp.Tests renders a small synthetic book (pictures drawn in code, no
  committed user art) and checks the PDF's page count, page size and embedded fonts.
- [ ] **E — Freddy draft.** Build Freddy and the Slipper (English) on this PC from the store's `.dino` (fetched through
  `DinoStoreClient`'s catalog, or the copy in the Dino cache), keep the output outside git, and record the check
  report in this slice file.

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
