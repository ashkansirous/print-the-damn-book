### Slice 17 — Right-to-left books (Farsi and Arabic)

*Built 2026-11-01, merged as PR #36 (86f848c).*

**Touches:** `src/PrintTheDamnBook.Core/Imposition.cs`, `src/PrintTheDamnBook.Core/Spreads.cs`,
`src/PrintTheDamnBook.Core/Sheet.cs`, `src/PrintTheDamnBook.Core/Layout/**`, `src/PrintTheDamnBook.Core/Text/**`,
`src/PrintTheDamnBook.Core/Dino/DinoBook.cs`, `src/PrintTheDamnBook.Core/Dino/DinoTranslator.cs`,
`tests/PrintTheDamnBook.Core.Tests/**`, `src/PrintTheDamnBook.App/Printing/StoryPainter.cs`,
`src/PrintTheDamnBook.App/Printing/FaceRenderer.cs`, `src/PrintTheDamnBook.App/Printing/ManualDuplexRunner.cs`,
`src/PrintTheDamnBook.App/Views/CheckStepView.xaml*`, `src/PrintTheDamnBook.App/ViewModels/MainViewModel.Story.cs`,
`src/PrintTheDamnBook.App/ViewModels/MainViewModel.Dino.cs`, `src/PrintTheDamnBook.App/Services/BookTextStore.cs`,
`src/PrintTheDamnBook.App/Services/FontLicences.cs`, `src/PrintTheDamnBook.App/Fonts/Vazirmatn*`,
`src/PrintTheDamnBook.App/PrintTheDamnBook.App.csproj`, `src/PrintTheDamnBook.App/ViewModels/MainViewModel.cs`,
`src/PrintTheDamnBook.App/ViewModels/MainViewModel.Wizard.cs`, `src/PrintTheDamnBook.App/ViewModels/WizardModels.cs`,
`src/PrintTheDamnBook.App/Themes/Controls.xaml`, `src/PrintTheDamnBook.App/Printing/AutomaticDuplexPlan.cs`,
`src/PrintTheDamnBook.App/Services/FolderImages.cs`, `CLAUDE.md` (index pass only). New: `Core/Text/TextDirection.cs`,
`Core/Text/StoryFont.cs`, `ReadingDirection` enum, `Fonts/Vazirmatn-Regular.ttf` and its licence. (Callers of `Imposition`
added at the claim, wave 2026-11-01.)

**Wave note (2026-11-01).** Slices 18 and 19 are built alongside and also edit `MainViewModel.Dino.cs`; keep this
slice's edits there to the language/direction block (`:23-26`, `:238`, `:268-370`). Merge order 17, 19, 18.

**Why.** The user asked to "add right to left languages" (2026-11-01). Their Dino books exist in Farsi, and Slice 15
disabled every right-to-left language with "right-to-left books are not supported yet".

**What the code does today (grounded 2026-11-01).**

- Story text is wrapped and drawn by WPF `FormattedText` in `Printing/StoryPainter.cs:91-98`, with `FlowDirection`
  hard-coded to `LeftToRight`, `InvariantCulture` and Andika. The editor `TextBox` (`CheckStepView.xaml:413`) has no
  `FlowDirection`. Core only computes rectangles and font size (`Layout/StoryTextLayout.cs`).
- Only `Fonts/Andika-Regular.ttf` is bundled; there is no font fallback. (Andika's lack of Arabic glyphs is from memory,
  not checked against its glyph table.)
- Left/right slots are fixed in `Core/Imposition.cs:37-41`. The text-page side (`StoryTextLayout.cs:148`, `rightHalf`),
  the spread-alignment spacers and `ManualDuplexPlan` all assume a left-bound book.
- `Dino/DinoBook.cs:10` has `DinoLanguage(Code, Name, IsRightToLeft, IsOwn)` from `CultureInfo.TextInfo.IsRightToLeft`;
  `StartLanguage()` (`:40-42`) skips RTL languages and a book whose languages are all RTL is refused.
- Page numbers are drawn with `number.ToString(InvariantCulture)` in `FaceRenderer.cs:262`.
- Dino Reader solved the text side already (`D:\ReadTheStupidText\docs/plan/slices/96-dino-layout-never-mirrors.md`):
  each text picks its direction from its first strong letter, and Arabic-script text uses Vazirmatn.

**Decisions** (locked with the user via RefineScope, 2026-11-01):

1. **A right-to-left book opens from the right.** Cover on the right, pages turn leftward. Every left/right choice in
   imposition, spread-alignment spacers, the text-page side, the "As printed" preview and manual duplex is mirrored.
2. **Languages and font:** Farsi and Arabic, in Vazirmatn (OFL; licence added to `Services/FontLicences`). Hebrew and
   other non-Arabic-script RTL languages stay disabled ("this language is not supported yet").
3. **How a book becomes RTL:** a *Reading direction* switch (Left to right / Right to left) on the Check step, saved in
   `book.json`. A `.dino` book sets it from the chosen language (changing the language re-sets it). A folder book
   sets it from the first strong letter of its first non-empty story text until the user picks; the user's pick is
   saved and never re-guessed over.
4. **The app's own screens are not translated and never mirror.** Only the book changes.
5. **Each story text picks its own direction and alignment** from its first strong letter (Core), in print and in the
   typing box.
6. **Page number digits:** Persian digits (۱۲۳) in Farsi books, Arabic-Indic (١٢٣) in Arabic books, Latin otherwise,
   chosen in Core `PageNumbering`.
   6a (coordinator, wave 2026-11-01): a folder book has no language, so its digits follow its script: Persian letters give Persian digits, other Arabic script Arabic-Indic.
7. **.dino:** Farsi and Arabic become selectable; the "not supported yet" message stays only for other RTL languages.
8. **Landing site** stays as is.

**Imposition (the rule change).** For a right-to-left book each face swaps sides:
front `left = p[2k]`, `right = p[N-1-2k]`; back `left = p[N-2-2k]`, `right = p[2k+1]`. Left-to-right stays exactly as
in `CLAUDE.md`. Blanks still go before the back cover.

**Work.** The first item is the smallest end-to-end path: a folder book with the switch set to Right to left prints
mirrored sheets.

- [x] Core: `ReadingDirection` enum (`LeftToRight`, `RightToLeft`), carried by the book text model (`Text/BookText.cs`)
      and read/written by `BookTextStore` (`readingDirection`, absent = left to right, so old `book.json` files load).
      Tests for the round trip and the missing field.
- [x] Core: `Imposition` takes the direction and mirrors faces as above. Tests: 4, 8, 12 and padded page counts in
      both directions; RTL sheet `k` equals the LTR sheet with each face's sides swapped.
- [x] Core: spread-alignment spacers (`Spreads.cs`), text-page side (`StoryTextLayout.cs`, `rightHalf`), and
      `ManualDuplexPlan` (order and rotation) follow the direction. Tests for each in both directions.
- [x] App: *Reading direction* switch on the Check step next to the language select, using the existing segmented
      switch style from `Controls.xaml` (no mock exists; labels "Left to right" / "Right to left",
      `AutomationProperties.Name` "Reading direction"). The "As printed" preview shows the mirrored sheets.
- [x] Core: `TextDirection.StartsRightToLeft(text)` (first strong character; empty or neutral-only = left to right) and
      `StoryFont.For(text)` (Vazirmatn when the text has Arabic-script letters, else Andika). Tests including mixed
      text, digits only, and leading punctuation.
- [x] Core: default direction for a folder book from the first non-empty story text, applied only while the user has
      not picked; tests that a saved pick is never replaced.
- [x] App: bundle `Vazirmatn-Regular.ttf` with its OFL licence, list it in `FontLicences`; `StoryPainter` uses
      `StoryFont.For` and sets `FlowDirection` and alignment per text; the Check step's text box does the same as you
      type. Shrink-to-fit still comes from Core.
- [x] Core: page-number digits by book language (`fa` → U+06F0..U+06F9, `ar` → U+0660..U+0669, else Latin), in
      `PageNumbering`; `FaceRenderer` draws the string Core gives it. Tests.
- [x] Core/App: `.dino` — Farsi and Arabic enabled in `DinoTranslator`/`DinoBook` (`StartLanguage` no longer skips
      them; a book is refused only if no language is supported); choosing one sets the reading direction. Tests.
- [ ] Docs (index pass after merge, not yet done): replace the imposition rule in `CLAUDE.md` with both directions
      (`plan.md` *Out of Scope* was already updated by the plan PR).

#### Design (Story Text mock, sections 2a-2c and 1f, added 2026-11-01)

Four frames, live renders of `docs/design/StoryCheck.dc.html`: 2a a Farsi Dino book (`Story Text.dc.html:24`), 2b its
"As printed" view (`:28`), 2c a folder book with the direction guessed (`:32`), and 1f (`:85`), whose caption now reads
"Farsi enabled since Slice 17" (the language select offers Farsi). The mock agrees with the Decisions above except where
an Open point is marked. The script holds the logic: `dirOf`/`guessDir`/`bookDir` (`StoryCheck.dc.html:445-452`),
`impose(list, rtl)` (`:517-523`), `digits` (`:524`), copy in `renderVals()` (`:559` on).

- **Reading direction switch (Check step).** A `.seg` radiogroup, `aria-label="Reading direction"`
  (`StoryCheck.dc.html:77-80`), two options: `ph arrow-line-right` "Left to right", `ph arrow-line-left`
  "Right to left". It sits after the language select and before the Reading order / As printed switch, and is shown for
  folder and Dino books alike. Its tooltip is "Reading direction · your pick", "... · set by the language" (Dino) or
  "... · guessed from the first story text" (folder). A click is a pick: it is saved and the book stops following
  the language or the guess. Changing the language clears the pick (`onLang`, `:635`).
- **Language select (Dino books only).** Options: "English (the book's own)", "Deutsch", "فارسی" (enabled),
  "עברית · this language is not supported yet" (disabled). Arabic is not in the mock's list.
- **2a, Reading order, Farsi.** Direction is rtl from the language. Sub-heading: "Reads right to left: the book opens
  from the right, so each spread starts on its right page. Set by the language." (folder: "... Guessed from the first
  story text."; after a pick the last sentence is dropped). Spreads are drawn right page first: the front cover sits
  on the right with an empty left slot, the back cover on the left. Footer ends "Opens from the right. Page numbers
  print as ۱, ۲, ۳." (`:659`). Story text in Vazirmatn, `direction: rtl`, per text from its first strong letter;
  the editor text box takes the same `dir` and font.
- **2b, As printed, mirrored.** Sub-heading: "What comes out of the printer, mirrored: the cover sits on the left of sheet 1's
  front, so the fold opens from the right." Sheet `k` follows `front = [p[2k], p[N-1-2k]]`, `back = [p[N-2-2k], p[2k+1]]`
  (left, right), the plan's formula exactly.
- **2c, folder book, own page.** For a right-to-left book the words page sits on the right, its picture on the left.
  Placement note: "Text on the right, picture on the left" (LTR: "Text on the left, picture on the right"), then
  ". I put a spacer page in front so they face each other." or ", facing each other when the book is open." (`:588`).
- **Tokens and sizes.** No new ones: the switch is the existing `.seg` / `.seg-opt` from `nocturne/styles.css:189-199`.
- **Simplified in the mock:** page digits appear only in the footer line, not on the tiles; the Persian/Arabic digit
  tables are real; the guess regex stands in for Core `TextDirection`.

**New strings** (app screens stay English, never mirrored): "Reading direction", "Left to right", "Right to left", the
three tooltip forms, the two sub-heading tails above, "Text on the right, picture on the left", "Opens from the right.",
"Page numbers print as ...". No second locale (Decision 4).

**Open point: switch shown for every book.** Plan: switch next to the language select (Dino only). Mock: the select is
Dino-only, the switch is shown for folder books too. **Default:** show the switch for folder and Dino books.
**Open point: Arabic in the Dino language select.** Plan enables Farsi and Arabic. Mock's select lists English, Deutsch,
Farsi and Hebrew only. **Default:** list the book's own languages; Farsi and Arabic enabled, other RTL ones disabled.
Settled by the mock: icons and tooltip wording of the switch; the sub-heading and footer copy.

**Verification.**

- `dotnet test PrintTheDamnBook.slnx`: the imposition, spacer, text-side, manual-duplex, direction, font and digit tests.
- Desktop (device-test, scripted): open a Farsi `.dino` from `Book sample/` or a fixture, check the language select
  offers Farsi, the switch reads Right to left, the "As printed" preview shows the cover on the right, Farsi text is
  right-aligned in Vazirmatn; Print to PDF and compare page order with the RTL formula; page numbers in Persian digits.
- Paper (user): print and fold one Farsi book; it opens from the right and reads in order.

**Changed while building:**

- `ManualDuplexPlan` and `PageList` needed no code: mirrored sheets flow through them, and tests prove it.
- `Core/Text/BookDirectionChoice` owns the pick, the guess and "changing the language clears the pick".
- `PageNumberSetup` carries a typed `Digits` field (`PrintViewModel.cs` touched).
- The direction guess runs only for Arabic-script text.
- Alt+Left / Alt+Right flip in an RTL book. Tile page numbers show the book's digits in Vazirmatn.
- Known behaviour: folder book digits follow the script (6a); Urdu and Pashto stay disabled; an RTL book with a left-to-right pick keeps Persian/Arabic digits (Decision 6); the guess is re-made live while the user has not picked.

**From review** (files in `D:\print-the-damn-book-worktrees\reviews\`):

- High (`slice-17-high.md`): all 9 findings applied.
- Medium (`slice-17-medium.md`): 1, 3 and 4 applied. Skipped: 2 (Hebrew gets Andika; Hebrew stays unsupported by Decision 2), 5 (digits follow the language even after an opposite pick; Decision 6), 6 (text box direction follows the live text; per Decision 5 and the mock), 7 (the RTL swap stated as a second pass over the LTR faces; design choice, no bug).
- Check (`slice-17-check.md`): all 6 skipped. 1 (guess re-made while typing) and 2 (language change clears the pick) are per spec and mock; 3 (digits for an RTL folder book) is per 6a; 4 (Spreads keeps the cover alone on the right, Imposition puts it on the left of sheet 1) is consistent with mock 2a/2b; 5 (Urdu disabled in the Dino select) is the fa/ar list of Decision 2; 6 (circular Core dependency via `DinoBook.Primary`) was skipped as a structure-only note with no failure path (low).

**Desktop run 2026-11-01: PASS.** The switch is present on a folder book; LTR and RTL "As printed" for 12 pages match both formulas; RTL sub-headings show; the pick is saved in `book.json` and restored.

#### For CLAUDE.md (lifted 2026-11-01)

- Replace the imposition rule with both directions: LTR as now; RTL front `left = p[2k]`, `right = p[N-1-2k]`; back `left = p[N-2-2k]`, `right = p[2k+1]`; blanks before the back cover still. (Slice 17: docs/plan/slices/17-right-to-left-books.md)
- Vazirmatn joins Andika as a story font (Arabic-script text); its licence is listed in `Services/FontLicences`. (Slice 17: docs/plan/slices/17-right-to-left-books.md)
- Reading direction and page-number digits are decided only in Core (`TextDirection`, `StoryFont`, `BookDirectionChoice`, `PageNumbering`); the App never derives them. (Slice 17: docs/plan/slices/17-right-to-left-books.md)

#### Verified 2026-11-01 (host desktop, Release build of main 96cbf2f)

- Farsi .dino (built for the test from an English one, opened by command line): the language select offers
  Farsi, the switch reads Right to left, the cover sits alone on the right, the typing box is right-aligned.
  Band text is centred, as for every language (`StoryPainter` bands use `TextAlignment.Center`).
- Print to PDF page order matches the RTL formula (sheet 1 back p1 | p14, sheet 2 front p2 | p13, sheet 4
  front p6 | p9).
- Persian digits on the printed pages (۲ ۱۳ ۶ ۹).

Evidence: `D:\print-the-damn-book-worktrees\reviews\verify-checks-2026-11-01.md`.

#### Not yet verified

- Paper print and fold of one Farsi book (user).
- The font the Farsi text is drawn in (Vazirmatn): the 2026-11-01 run saw Arabic-script glyphs but did not read the font name from the UI tree (feedback loop 2026-11-01-2024).
