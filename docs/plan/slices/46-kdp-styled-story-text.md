### Slice 46 — Amazon KDP: styled story text and redesigned front and back pages

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
`docs/plan/slices/46-kdp-styled-story-text.md` (new)

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
5. **R8 shout** follows the rule as written, so "Look!" at the start of a quote is a shout even though the mock leaves it plain
   (owner: "the rules below are what must be built").
6. **"Quiet sound" next to** means in the same sentence or its attribution ("squawk, squawk, squawk," said Seagull softly).
7. **Precedence** of rules on one word is the mock's `style()` order, written in the typography spec.
8. **R3 colours** other than red, the "not red" and "differs clearly" thresholds and palette colours 6 to 10 are in the spec, marked desk.
9. **Choices live in `kdp.json`** (the owner's per-book record next to the PDFs, like crop and cover choices) as a new `styles` object:
   `{ speakers: { "<name>": "#RRGGBB" }, mainCharacter: { name, reason }, titlePicture: { page, centerX, centerY, diameter } }`
   (centre and diameter as fractions of the picture's shorter side). Set only by the CLI: `--speaker "<name>=#hex"` (`=clear`),
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

#### Work

- [ ] **A. Spike, then stop if it fails.** A Kdp test draws one page with an injected word shifted −6 pt and one rotated −4°, a song line
      bouncing ±2 pt, a Farsi line with an injected word, and a round 230 pt picture with border and shadow. Checks: the PDF text layer
      (`KdpPdfInspector`) holds every word once; the rendered page shows no glyph of a shaped line inside the next line's box; the line
      height grew by the shift. If any check can't pass cleanly, stop and ask the owner before building R6 or R10.
- [ ] **B. Smallest end-to-end: R1 and bold/italic fonts on X Marks.** Add `Andika-Bold.ttf`, `Andika-Italic.ttf`, `Vazirmatn-Bold.ttf`
      to the App's `Fonts/` (SIL OFL, from the upstream releases), link them as Kdp embedded resources, register them in `KdpFonts`, list
      them in `Services/FontLicences`. Core `Kdp/Styling/` gets the styled-run model (text, size, bold, italic, colour, letter spacing,
      baseline, rotation, side margin) and `StoryStyler` turning a page's text into paragraphs of runs. `KdpInteriorDocument` and
      `KdpTextFit` draw and measure those runs. X Marks builds with paragraph gaps and no overflow.
- [ ] **C. Word-list rules R2, R3, R9.** Core per-language lexicon (`en` only), R3 darkening to 4.5:1 on the page tint (Core contrast
      maths, OKLCH like `TextPageTint`).
- [ ] **D. Language-free rules R4, R7, R8.** R7 needs the whole book's text, so `StoryStyler` takes the book.
- [ ] **E. Voices R5 and songs R10, per-book choices.** Speaker detection from the cast; `kdp.json` `styles`; CLI flags; colour rules
      (contrast on white and every tint, not red, distinct); fallback palette and main character by line count; "fallback" and
      "no speaker found" warnings in `check-report.md`. Songs: 28 pt bold, singer's colour, ±2 pt bounce (inline elements from item A).
- [ ] **F. Shapes R6.** Meaning lists (`en`), the six classes, line growth maths in Core, drawing with item A's inline elements.
- [ ] **G. Front and back pages.** `KdpPagePlan`: Title, Credits, Belongs-to, story, blanks, The End, More books (only with other
      covers, taking a blank when there is one), Dino page last; total stays even and ≥ 24. Title page (tint of the first story text
      page, round picture from `styles.titlePicture` decoded through `KdpImageDecoder`, 72/36 pt title, R3 on the title), credits page
      (labels from `BackCoverWording`), belongs-to page, The End (main character's colour, tint of the last story text page), More books
      page (Slice 41 grid, unchanged look), Dino page (QR 150 pt to `https://dinoreader.sirous.uk/`, short address constant, its own
      wording constants, the speakers' names in their colours). Values from `docs/design/kdp-typography.md`.
- [ ] **H. Style agent and skill.** CLI `--style-sheet` writes `styles/speakers.md` (each speaker, line count, pages, the page tints)
      next to the crop sheet's small `crop/<key>.png` copies. New Sonnet-pinned read-only agent `kdp-style-check` reads only those small
      copies (never a full-size picture) and answers one line per speaker (`<name> = #hex — reason`), `main = <name> — reason`, and
      `title = <page> <cx>,<cy>,<d> — reason` (face and body framed, calm background, no other character cut in half). New `kdp-style`
      skill runs it per book and language and saves the answers with the CLI flags; `kdp-build` runs it before the build when
      `kdp.json` has no `styles`.
- [ ] **I. Builds and previews.** `kdp-build` for X Marks the Treasure! (pilot), The Gingerbread Man and The Three Little Pigs in English,
      with `--previews`; `kdp-visual-check` on each; `check-report.md` has no overflow error. Local builds only; nothing goes to KDP.
- [ ] **J. Docs.** `docs/design/README.md` lists the new files (in this plan PR); the CLAUDE.md rules to lift: styling in Core
      `Kdp/Styling`, choices in `kdp.json` `styles` set only by the CLI, short address constant, fonts list.

#### Verification

- `dotnet test PrintTheDamnBook.slnx` green: a test per rule (R1 to R10), per shape class, per precedence step, for the colour rules,
  the fallbacks and the page plan, with excerpts from five books; the Farsi and Arabic tests for right-to-left order.
- `dotnet format PrintTheDamnBook.slnx --verify-no-changes` passes; `kdp-linux` CI green (Core `Kdp` namespace and Kdp.Tests).
- `kdp` builds X Marks the Treasure!, The Gingerbread Man and The Three Little Pigs (`en`) with exit 0 and no overflow in
  `check-report.md`; previews checked by `kdp-visual-check` match the ★ rows (story pages like `Asked.dc.html`, front and back pages like
  `Matter.dc.html` and `DinoLink.dc.html`, More books on its own page before the Dino page).
- The interior PDF's text layer holds every story word once (no doubled shaped words).
