### Slice 46 — Amazon KDP: styled story text and redesigned front and back pages

*Built 2026-11-25, PR #105 (not merged). Nothing needs a device (CLI and PDF only); `kdp-linux` CI runs on the PR.*

*Planned 2026-11-25 (interactive session, owner present).*

**Touches:** `src/PrintTheDamnBook.Core/Kdp/Styling/` (new), `src/PrintTheDamnBook.Core/Kdp/KdpBookSettings.cs`,
`src/PrintTheDamnBook.Core/Kdp/KdpPagePlan.cs`, `src/PrintTheDamnBook.Core/Kdp/KdpFrontMatterWording.cs`,
`src/PrintTheDamnBook.Core/Kdp/KdpCoverWording.cs`, `src/PrintTheDamnBook.Core/Dino/BackCoverWording.cs`,
`src/PrintTheDamnBook.Kdp/KdpInteriorDocument.cs`, `src/PrintTheDamnBook.Kdp/KdpInterior.cs`,
`src/PrintTheDamnBook.Kdp/KdpTextFit.cs`, `src/PrintTheDamnBook.Kdp/KdpFonts.cs`, `src/PrintTheDamnBook.Kdp/KdpExporter.cs`,
`src/PrintTheDamnBook.Kdp/KdpCheckReport.cs`, `src/PrintTheDamnBook.Kdp/KdpImageDecoder.cs`,
`src/PrintTheDamnBook.Kdp/PrintTheDamnBook.Kdp.csproj`, `src/PrintTheDamnBook.Kdp/Styling/` (new),
`src/PrintTheDamnBook.Kdp/StyleSheet/` (new), `src/PrintTheDamnBook.Cli/KdpCommandLine.cs`, `src/PrintTheDamnBook.Cli/Program.cs`, `src/PrintTheDamnBook.Cli/KdpRunner.cs`,
`src/PrintTheDamnBook.App/Fonts/Andika-Bold.ttf` (new), `src/PrintTheDamnBook.App/Fonts/Andika-Italic.ttf` (new),
`src/PrintTheDamnBook.App/Fonts/Vazirmatn-Bold.ttf` (new), `src/PrintTheDamnBook.App/Services/FontLicences.cs`,
`tests/PrintTheDamnBook.Core.Tests/Kdp/Styling/` (new), `tests/PrintTheDamnBook.Core.Tests/Kdp/`, `tests/PrintTheDamnBook.Kdp.Tests/`,
`.claude/agents/kdp-style-check.md` (new), `.claude/skills/kdp-style/` (new), `.claude/skills/kdp-build/SKILL.md`,
`docs/design/kdp-typography.md` (new), `docs/design/kdp-story-text/` (new), `docs/design/README.md`,
`docs/plan/slices/46-kdp-styled-story-text.md` (new); added during the build (item E: `DinoBook` did not read `Cast`):
`src/PrintTheDamnBook.Core/Dino/DinoBook.cs`, `src/PrintTheDamnBook.Core/Dino/DinoBookReader.cs`,
`tests/PrintTheDamnBook.Core.Tests/DinoBookReaderTests.cs`

**Migrations:** none

**Depends on:** Slices 36, 37, 41, 42 and 45 (merged). Pilot book: X Marks the Treasure!, English. Only English books are built in
this slice (owner, 2026-11-25: "focusing only on english books and x marks the treasure as the pilot").

#### Why

Owner, 2026-11-25: every KDP interior gets styled story text (ten rules, R1 to R10) and redesigned front and back pages, for every book,
KDP only (the home-print App layout is untouched). The owner's full spec is the message of 2026-11-25; the look is the "Story Text
Experiments" Design canvas, https://claude.ai/code/artifact/79577946-e48e-4087-a83b-dbba0476196f, copied to
`docs/design/kdp-story-text/`. Its ★ rows are agreed (`Asked.dc.html`, `Matter.dc.html`, `DinoLink.dc.html`); they are drawn from one
book to show the look, and the rules are what is built, for all books. Every value is in **`docs/design/kdp-typography.md`**: the
builder reads values from there, never from this file's prose, and the design file wins where they differ.

#### Decisions

Owner, 2026-11-25 (answers in this session):

- The short address is `dinoreader.sirous.uk`, one Core constant for all books (the address the QR code already uses,
  `KdpCoverWording.DinoReaderUrl`). The mock's `dino.sirous.uk` is not used.
- The "More books by Ashkan Sirous" grid (Slice 41) moves to its own page just before the new Dino page, look unchanged; no other
  covers, no page.

From the owner's spec (quoted short; the full text is the owner's message):

- Detection and maths in Core, drawing only in `PrintTheDamnBook.Kdp`, unit tests for every rule with several books as test data.
- R1, R4, R7 and R8 are language-free; R2, R3, R6, R9 word lists and the R5/R10 attribution verbs are per-language lists in Core.
  A language without a list skips those rules quietly. Right-to-left books must work.
- Per-book choices (speaker colours, main character, title picture) are made by an agent from the small copies, saved through the CLI,
  never guessed in code. With no saved choice: the fallback palette, main character = most lines, title picture = first story picture
  centred, and a "fallback" warning in `check-report.md`, as for covers.
- An overflowing page stays a report error and a non-zero exit, never shrunk. `KdpTextFit` measures the real styled text.
- QuestPDF can't shift or rotate one span. If inline placement can't be done cleanly, stop and ask the owner; never fake it with moves
  that overlap lines.
- Keep `KdpFrontMatterWording.DinoLine` on the back cover as it is; the last page gets its own wording constants.

Desk (decided by this planning session, 2026-11-25; the owner can change any of them):

1. **Speakers come from the book's own cast.** Every `.dino` has a `Cast` with names (and `LanguageCasts` per language; read
   2026-11-25 from all 16 books in `D:\high-res-books`). R5 looks for an attribution verb next to a quote (`said|asked|shouted|called|
   cried|sang|whispered|replied … <Name>` or `<Name> said …`, either side) and accepts only a cast name; a pronoun ("she said") or no
   attribution leaves the quote in ink with a warning naming the page.
2. **Paragraphs.** The source has blank lines between paragraphs (`"…flap!\n\nKathryn…"`); `StoryLines.Clean` keeps a single `\n` there,
   so a `\n` after `Clean` is the paragraph break. Gap 14 pt through QuestPDF `ParagraphSpacing`.
3. **R4 rhyme test.** Three words in a row (punctuation between them allowed) that are equal, or share their first two and last two
   letters and are at least four letters long (splish, splash, splosh). Two in a row ("far, far") is not a triplet.
4. **R7 refrain.** A clause is text between `. ! ? ; :`, commas inside kept, and quote marks. Clauses of 3+ words, compared
   case- and punctuation-free, found 2+ times anywhere in the book. Whole-clause matches only, so "said Kathryn" never counts.
   An attribution clause (an R5 attribution verb next to a cast name, e.g. "sang Pancake Polly", "said Gingerbread Man") is never a
   refrain, whatever its length (desk, 2026-11-25, during the build: the intent of "said Kathryn never counts").
5. **R8 shout** follows the rule as written, so "Look!" at the start of a quote is a shout even though the mock leaves it plain
   (owner: "the rules below are what must be built").
6. **"Quiet sound" next to** means in the same sentence or its attribution ("squawk, squawk, squawk," said Seagull softly).
7. **Precedence** of rules on one word is the mock's `style()` order, written in the typography spec.
8. **R3 colours** other than red, the "not red" and "differs clearly" thresholds and palette colours 6 to 10 are in the spec, marked desk.
9. **Choices live in `kdp.json`** (the owner's per-book record next to the PDFs, like crop and cover choices) as a new `styles` object:
   `{ speakers: { "<name>": "#RRGGBB" }, mainCharacter: { name, reason }, titlePicture: { page, centerX, centerY, diameter } }`
   (corrected while building: the centre is fractions of the picture's width and height, the diameter a fraction of the shorter side). Set only by the CLI: `--speaker "<name>=#hex"` (`=clear`),
   `--main-character "<name>"`, `--title-picture <page>=<cx>,<cy>,<d>`. The CLI refuses a speaker colour that fails the colour rules and
   says which. Older `kdp.json` files still read; an unreadable one or one from another book is never overwritten (Slice 28 rule).
10. **Wording is English only** in this slice (title, credits labels, "This book belongs to", "Draw yourself here!", the Dino page lines,
    the R2/R3/R6/R9 lists and the verbs), in per-language tables with one `en` entry. Other languages get their tables in Slice 33;
    until then a non-English build skips the word-list rules quietly and uses English page wording with one warning.
11. **Credit labels** follow the mock: "Story by", "Illustrations by" (today's wording says "Illustration: {0}"). The labels move into
    `BackCoverWording` as label-only entries per role, so names and roles still come from the credit wording, not from the page code.
12. **Title-picture fallback** with no saved choice is the first story picture, centred, diameter = its shorter side; the code never
    tries to guess what the agent would pick.
13. **No right-to-left KDP book is publishable** (no Farsi or Arabic paperback on KDP, *Outstanding work*), and only English is built
    now, so right-to-left is proven by unit tests (Farsi and Arabic strings from the books' `Translations`) and one Kdp test that draws
    a Farsi page, not by a full book build.
14. **Test data.** Short one- or two-sentence excerpts from at least five catalog books (X Marks the Treasure!, The Gingerbread Man,
    The Three Little Pigs, Little Red Riding Hood, The Ugly Duckling) as test strings, as the owner asked. Plus one opt-in test that reads
    every book in `D:\high-res-books` when the folder exists (skipped on CI) and checks detection never throws.

#### Rendering approach (checked 2026-11-25, `library-docs` against the installed QuestPDF 2026.9.2)

- Per span QuestPDF sets `FontSize`, `Bold`, `Italic`, `FontColor`, `LetterSpacing` (em) and `FontFamily` fallbacks. That covers R2,
  R3, R4, R5, R7, R8 and R9 inside one `Text` block. `ParagraphSpacing(14)` covers R1.
- There is no per-span baseline shift or rotation (only `Superscript`/`Subscript` presets). R6 and R10 words are placed one by one as
  inline elements: `text.Element(TextInjectedElementAlignment…)` holding the word, with `TranslateY` and `Rotate`, padded by the shift
  (or the rotated box's extra height, maths in Core) so the line grows and no neighbour is overlapped. Such an element must fit on one line,
  which a single word does.
- There is no measuring API. `KdpTextFit` already lays out a probe page with the real composer; it keeps doing that with the styled
  composer, so gaps, bigger words and shapes count.
- Unconfirmed until item A: whether `Rotate` turns about the element's centre, whether `TranslateY` compiles as named, whether an
  injected word stays real text in the PDF, how inline elements order in right-to-left text, and whether QuestPDF can round-clip an
  image with a border and shadow (else the round picture is a PNG made with SkiaSharp, like `KdpSoftShadow`).

**Item A results (2026-11-25, spike passed; `tests/PrintTheDamnBook.Kdp.Tests/KdpStyledTextSpikeTests.cs`):**

- `Rotate(deg)` turns about the element's centre and does not change layout: pad with `PaddingVertical(extra)`,
  extra = (w·sinθ + h·cosθ − h)/2 (maths in Core). A rotated word lands in the PDF as one run per letter: text-layer checks
  for rotated words compare letters.
- `TranslateY` is obsolete in 2026.9.2 (CS0618); use `OffsetY(float)`. It moves the drawing only; padding grows the line.
- An injected word (`text.Element(handler, TextInjectedElementAlignment.Bottom|Top)`) stays real text, once.
- Right-to-left: an injected word with its own `ContentFromRightToLeft()` inside `ContentFromRightToLeft().Text(...)` keeps
  logical order. The PDF stores joined forms: compare after NFKC.
- Round picture is pure QuestPDF: `.Width(d).Height(d).Shadow(new BoxShadowStyle{…}).CornerRadius(d/2).Border(6)
  .BorderColor(white).BorderAlignmentInside().Image(bytes)`; no SkiaSharp fallback.
- Baseline calibration for an injected shifted word is measured at 24 pt Andika only (0.032 em for Bottom, 0.203 em for Top);
  it does not scale to 28 or 30 pt. Shifts (R6) apply only to words in 24 pt lines; a word whose neighbours are not 24 pt is not
  shifted unless re-calibrated. Song bounce needs no calibration: every word `Bottom`, `PaddingBottom(4)` on high words.

#### Work

- [x] **A. Spike, then stop if it fails.** (passed, results above) A Kdp test draws one page with an injected word shifted −6 pt and one rotated −4°, a song line
      bouncing ±2 pt, a Farsi line with an injected word, and a round 230 pt picture with border and shadow. Checks: the PDF text layer
      (`KdpPdfInspector`) holds every word once; the rendered page shows no glyph of a shaped line inside the next line's box; the line
      height grew by the shift. If any check can't pass cleanly, stop and ask the owner before building R6 or R10.
- [x] **B. Smallest end-to-end: R1 and bold/italic fonts on X Marks.** (built 2026-11-25; the Andika Bold/Italic files are the google/fonts copies matching the existing Regular file, same family version; the SIL v7.000 release has different metrics from the Regular already bundled) Add `Andika-Bold.ttf`, `Andika-Italic.ttf`, `Vazirmatn-Bold.ttf`
      to the App's `Fonts/` (SIL OFL, from the upstream releases), link them as Kdp embedded resources, register them in `KdpFonts`, list
      them in `Services/FontLicences`. Core `Kdp/Styling/` gets the styled-run model (text, size, bold, italic, colour, letter spacing,
      baseline, rotation, side margin) and `StoryStyler` turning a page's text into paragraphs of runs. `KdpInteriorDocument` and
      `KdpTextFit` draw and measure those runs. X Marks builds with paragraph gaps and no overflow.
- [x] **C. Word-list rules R2, R3, R9.** (built 2026-11-25) Core per-language lexicon (`en` only), R3 darkening to 4.5:1 on the page tint (Core contrast
      maths, OKLCH like `TextPageTint`).
- [x] **D. Language-free rules R4, R7, R8.** (built 2026-11-25; R7 is literal per decision 4, so a repeated 3-word attribution such as "sang Pancake Polly" or "said Gingerbread Man" is a refrain, pinned by a test) R7 needs the whole book's text, so `StoryStyler` takes the book.
- [x] **E. Voices R5 and songs R10, per-book choices.** (built 2026-11-25; `DinoBook` now reads `Cast`/`LanguageCasts` names, which were ignored before; R6 is last in the precedence, so a loud-sound triplet at the start of a shout, "Squawk! Squawk! Squawk!", is 24/27/30 pt, not 30 for the first word; an attribution clause is never a refrain; the first fallback blue `#1F4FA0` is within 0.10 of the R4 blue `#1F5FA8`, so the fallback palette is checked against other speakers only and a saved/CLI colour against the triplet colours too; `RunLook` gained `Song`; the tint and distinctness rules of a CLI colour are checked at build time and refuse before anything is written) Speaker detection from the cast; `kdp.json` `styles`; CLI flags; colour rules
      (contrast on white and every tint, not red, distinct); fallback palette and main character by line count; "fallback" and
      "no speaker found" warnings in `check-report.md`. Songs: 28 pt bold, singer's colour, ±2 pt bounce (inline elements from item A).
- [x] **F. Shapes R6.** (built 2026-11-25; baseline moves (down, up, bounce) apply only when the triplet and its neighbouring words are 24 pt, the item A calibration limit; size and turn shapes have no such limit; the width used for turn padding is an estimate, since QuestPDF has no measuring API) Meaning lists (`en`), the six classes, line growth maths in Core, drawing with item A's inline elements.
- [x] **G. Front and back pages.** (built 2026-11-25; the title's red X in the mock is not drawn, since R3 only colours colour words and "X Marks the Treasure!" has none; the title circle's centre is read as fractions of the picture's width and height and the diameter of its shorter side, because a portrait picture's centre would leave the 0 to 1 range `KdpTitlePicture` accepts if both were fractions of the shorter side; the dashed frames and the dotted name line are plain SVG shapes; the Dino page's names wrap between names only) `KdpPagePlan`: Title, Credits, Belongs-to, story, blanks, The End, More books (only with other
      covers, taking a blank when there is one), Dino page last; total stays even and ≥ 24. Title page (tint of the first story text
      page, round picture from `styles.titlePicture` decoded through `KdpImageDecoder`, 72/36 pt title, R3 on the title), credits page
      (labels from `BackCoverWording`), belongs-to page, The End (main character's colour, tint of the last story text page), More books
      page (Slice 41 grid, unchanged look), Dino page (QR 150 pt to `https://dinoreader.sirous.uk/`, short address constant, its own
      wording constants, the speakers' names in their colours). Values from `docs/design/kdp-typography.md`.
- [x] **H. Style agent and skill.** (built 2026-11-25; `--style-sheet` also writes the `crop/<key>.png` copies, as `--crop-sheet` does; the agent has not been run on a book yet, that is item I's review of the build) CLI `--style-sheet` writes `styles/speakers.md` (each speaker, line count, pages, the page tints)
      next to the crop sheet's small `crop/<key>.png` copies. New Sonnet-pinned read-only agent `kdp-style-check` reads only those small
      copies (never a full-size picture) and answers one line per speaker (`<name> = #hex — reason`), `main = <name> — reason`, and
      `title = <page> <cx>,<cy>,<d> — reason` (face and body framed, calm background, no other character cut in half). New `kdp-style`
      skill runs it per book and language and saves the answers with the CLI flags; `kdp-build` runs it before the build when
      `kdp.json` has no `styles`.
- [x] **I. Builds and previews.** `kdp-build` for X Marks the Treasure! in English only (owner, 2026-11-25, at the build start:
      "do it only for 1 ... book in english"; no other book or language is built in this slice), with `--previews`;
      `kdp-visual-check` on it; `check-report.md` has no overflow error. Local builds only; nothing goes to KDP.
      Built 2026-11-25 with styles chosen by `kdp-style-check`: exit 0, 40 pages (1 Title, 2 Credits, 3 Belongs-to, 4-37 story
      text/picture pairs, 38 The End, 39 More books with 15 covers, 40 Dino page). `check-report.md` has no errors and two warnings:
      one unattributed quote on story page 2 ("X marks the treasure!") and 17/17 pictures at about 208 dpi (an older warning).
      `kdp-visual-check`: clean. PDF text-layer check of page 6: Seagull's quote in `#24407A`, Kathryn's in `#7E1F5E`, triplets
      orange/blue/purple, "red X." in `#C62828`. Nothing went to KDP.
- [x] **J. Docs.** `docs/design/README.md` already lists `kdp-typography.md` and `kdp-story-text/` (checked, no change). The CLAUDE.md
      rules to lift are under *For CLAUDE.md* below.

#### Verification

- `dotnet test PrintTheDamnBook.slnx` green: a test per rule (R1 to R10), per shape class, per precedence step, for the colour rules,
  the fallbacks and the page plan, with excerpts from five books; the Farsi and Arabic tests for right-to-left order.
- `dotnet format PrintTheDamnBook.slnx --verify-no-changes` passes; `kdp-linux` CI green (Core `Kdp` namespace and Kdp.Tests).
- `kdp` builds X Marks the Treasure! (`en`, the only book built) with exit 0 and no overflow in
  `check-report.md`; previews checked by `kdp-visual-check` match the ★ rows (story pages like `Asked.dc.html`, front and back pages like
  `Matter.dc.html` and `DinoLink.dc.html`, More books on its own page before the Dino page).
- The interior PDF's text layer holds every story word once (no doubled shaped words).

#### Found during the build (2026-11-25)

- **Owner question, R5 follow-on quotes.** Across the 16 books, 104 of the English quotes have no speaker; about 70 are a second
  quote by the same speaker right after the attribution (`"Stop, stop!" said Granny. "Come back and give me a big cuddle!"`).
  Decision 1 ("next to a quote") leaves them in ink with a warning. Giving such a quote to the previous speaker is a new rule; not
  built. X Marks has one ("X marks the treasure!", story page 2). Counts per book: X Marks 1/28, Good Enough 0/22, Little Red Hen
  2/36, Goldilocks 3/29, Love from Seagull Island 4/27, Elves and Shoemaker 4/24, Three Little Pigs 4/23, Town Mouse 5/26, Billy Goats
  6/26, Red Riding Hood 7/34, Gingerbread Man 8/29, Lion and Mouse 8/22, Ugly Duckling 8/30, Jack and the Beanstalk 10/33, Tortoise and
  Hare 11/33, Freddy 23/44.
- **X Marks styles chosen** (kdp-style-check, from the small copies): Seagull `#24407A`, Billy `#0A5F6B`, Pancake Polly `#2F6B34`, Izzy
  `#8A4B12`, Kathryn `#7E1F5E`; main character Kathryn; title picture story page 4 at 0.50,0.55, diameter 0.90. The agent's first teal
  for Kathryn and plum for Billy were refused (OKLab < 0.10 from the triplet colours); its retry for Kathryn (`#8E2766`) would have
  been refused too (hue inside the red band, 0.0997 from the purple), so the coordinator took the nearest passing shade of the same
  plum-magenta, `#7E1F5E`. The agent file now names the 0.10 distance.
- `kdp.json` wrote a duplicate `SpeakerColours` key (computed property serialised); fixed with `[JsonIgnore]` and a test.

#### Changed while building

- **Touches grew:** `src/PrintTheDamnBook.Core/Dino/DinoBook.cs`, `src/PrintTheDamnBook.Core/Dino/DinoBookReader.cs`,
  `tests/PrintTheDamnBook.Core.Tests/DinoBookReaderTests.cs`, `src/PrintTheDamnBook.Core/Kdp/KdpMatterSpec.cs`,
  `src/PrintTheDamnBook.Core/Kdp/KdpTitleCircle.cs`.
- **Title picture centre** is fractions of the picture's width and height (diameter a fraction of the shorter side); Decision 9's
  wording is corrected above.
- **R7 attribution clauses** are never refrains (Decision 4, last lines).
- **The mock's red X is not drawn on the title page:** R3 colours colour words only and the title has none. The owner can ask for it.
- **R6 wins over R8** per the spec's precedence: "Squawk!" grows 24/27/30 pt.

#### Review

- *From review:* findings files `D:\print-the-damn-book-worktrees\reviews\s46-pass1-high.md` and
  `D:\print-the-damn-book-worktrees\reviews\s46-pass2-medium.md`. Pass 1 (high): 0 blocker, 6 nits; 1, 2, 3, 5, 6 fixed; 4 skipped
  (`KdpInterior.cs:95`, `FrontPagesFor` re-runs the style setup per `DrawingFor` call: a speed cost only, the output is the same).
  Pass 2 (medium): 1 important (right-to-left names on the Dino page) and 6 nits, all fixed. Post-review: the duplicate `SpeakerColours` key in `kdp.json` was fixed (see *Found during the build*).

#### For CLAUDE.md

- Styling maths (the R1-R10 rules, colour checks, speaker analysis) lives in Core `Kdp/Styling`; drawing lives in Kdp `Styling/`. The
  Kdp project never decides what is styled. (Slice 46: docs/plan/slices/46-kdp-styled-story-text.md)
- Per-book style choices are `kdp.json` `styles` (`speakers`, `mainCharacter`, `titlePicture`), set only by the CLI (`--speaker`,
  `--main-character`, `--title-picture`), never by a skill editing the file. The style sheet is a side output like the previews and never
  stops the PDFs; `kdp.json` is still written last. (Slice 46: docs/plan/slices/46-kdp-styled-story-text.md)
- The short address on the Dino page is the one constant `dinoreader.sirous.uk`. (Slice 46: docs/plan/slices/46-kdp-styled-story-text.md)
- Styled text uses Andika Bold and Italic (google/fonts copies, matching the bundled Regular) and Vazirmatn Bold, linked from the App's
  `Fonts/` like the others; both licences stay in `Services/FontLicences`. (Slice 46: docs/plan/slices/46-kdp-styled-story-text.md)
- R6 size shifts apply only to 24 pt story lines (calibration); R6 wins over R8. (Slice 46: docs/plan/slices/46-kdp-styled-story-text.md)
- The `kdp-style` skill and `kdp-style-check` agent (in `.claude/`) pick speaker colours, the main character and the title picture
  from the crop sheet's small copies; `kdp-build` runs them when `kdp.json` has no `styles`. Never open a full-size picture.
  (Slice 46: docs/plan/slices/46-kdp-styled-story-text.md)

#### Not yet verified

- None needing a device (CLI and PDF only). `kdp-linux` CI runs on PR #105.
