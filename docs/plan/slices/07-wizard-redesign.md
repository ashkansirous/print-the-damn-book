### Slice 07 — Redesign: the Open → Check → Print wizard in Nocturne

*Built 2026-10-22, awaiting merge. Desktop-tested 2026-10-22 on a PDF printer only; 141 tests.*

**Touches:** `src/PrintTheDamnBook.App/**`, `tests/PrintTheDamnBook.Core.Tests/**` (only if a Core helper is added)

**Why.** Today everything sits on one screen: the folder, the printer settings, the warnings, the page strip and
the sheet list all compete for attention, and Print sits in the middle of a toolbar (design 0a). The redesign walks the
user through three steps and ends with a fold guide, in the Nocturne dark theme.

**Decisions** (2026-10-22, from the design; recommended where the design leaves a choice):

- Direction **1b, the left rail** (Windows 11 settings style), which is the prototype's default. Each step shows
  a live summary under its title. The cover thumbnail, the folder name and "N pages on M sheets" sit at the
  bottom of the rail. Headings stay at the 1b size (22 px).
- The steps:
  - **Open pages:** drop a folder on the window or pick one. Once loaded, the step shows the cover, the
    folder path and "Pick a different folder".
  - **Check the order:** a Reading order (spreads) / As printed (sheets) toggle, Add pages, drag to move,
    an × on each editable page to remove it, and pinned covers. Problems show as an amber ring and a note
    on the affected thumbnail instead of a banner, with warning chips above the grid.
  - **Print:** printer, paper, the cover-on-its-own switch, the test-sheet card and the stacked-sheet preview.
  - **Done:** the three fold steps, Print another copy, and Make another book.
  - The footer holds the status text, Back and Next.
- All copy comes from `docs/design/BookWizard.dc.html` (`renderVals`). It changes only where the app
  already behaves differently:
  - the test-sheet text points at the app's own Flip edge / Rotate backs options, not "your printer settings";
  - manual two-sided printing (Slice 6) keeps its reload dialog, restyled as a Nocturne dialog.
- The design doesn't show the options from Slices 4 and 6: Two-sided Automatic/Manual, Reverse, Rotate,
  Flip edge, Fit/Fill and margin. They go in a collapsed **More options** section on the Print step, styled
  with the Nocturne field and segmented-control look. Page numbers (Slice 09) will join them.
- Theme: the Nocturne tokens from `docs/design/nocturne/styles.css` become WPF resources in
  `Themes/Nocturne.xaml`: a brush for every `--color-*` token, the radii, and the three shadows. Inter (OFL)
  and the Phosphor icons (MIT) are bundled as fonts. Dark only; a light theme is out of scope for now.
- A custom 32 px title bar per the design (icon + "Print the damn book · folder"), built with
  `WindowChrome` so snap layouts and the caption buttons keep working.
- No behaviour changes. Core, printing, manual duplex and settings stay as they are; the redesign only
  rebinds the existing view models, split per step where that reads better.

**Work.**

- [x] Nocturne resource dictionary, bundled Inter and Phosphor, and base control styles (primary,
      secondary and ghost buttons, segmented control, switch, input, tag, card, dialog).
- [x] Window shell: WindowChrome title bar, the left rail with step summaries and the cover block, the footer.
- [x] Open step, with folder drop and the loaded card.
- [x] Check step: Reading order spreads and As printed sheets, drag/remove/add, inline warnings.
- [x] Print step: printer, paper, cover switch, test-sheet card, stack preview, More options.
- [x] Done step: fold guide, Print another copy, Make another book.
- [x] Every control, including the remove buttons, gets an `AutomationProperties.Name` (this closes the Slice 3 gap).

**Changed while building:**

- Added `Core/Spreads.cs` (with tests) for the reading-order spreads.
- README gained a Licences note (Inter OFL, Phosphor MIT; texts embedded and shown from a Licences link in the rail).
- The heading row wraps (toggle group follows the heading) instead of docking right. Print left column has MinWidth 260 / MaxWidth 420.
- A dropped folder is accepted on the Open step only.
- Uppercase captions have no letter-spacing. The sheet preview no longer shows the Fill crop.
- Warning colour converted to sRGB `#EFBA64` (from `oklch(0.82 0.12 78)`) in `Themes/Nocturne.xaml`.
- Copy added where the mock is silent: missing cover / duplicate covers / unreadable images chips, dialog titles, the manual-mode line.
- `NocturneDialog` replaces the MessageBox prompts; `PrintStatusKind` enum drives the footer tone.

**From review:** two passes, 21 findings applied. Desktop test 2026-10-22 (PDF printer only) passed 6 of 7; the layout bugs found were fixed (min-size wrap/clip, caption alignment, footer wrap, focus rings, drag tooltip, Alt+Right first press).

**Verification.**

- `dotnet build`, `dotnet test` and format verify are green.
- Desktop test on `Book sample` and on a folder with problems (unrecognised names, no back cover). Compare each
  step by eye with its 1b/1d artboard in `docs/design/Redesign Directions.dc.html`. Drag, remove and add still
  work, and printing to Microsoft Print to PDF produces the same sheets as before.

#### Design (Redesign Directions + BookWizard mock, sections 1b and 1d, added 2026-10-22)

Four frames of one prototype: 1a stepper, 1b rail, 1c focus and 1d rail on As printed (each 1100×760, in `docs/design/Redesign Directions.dc.html` lines 22-37), plus 0a, today's UI. All layout and copy live in `docs/design/BookWizard.dc.html` (markup lines 18-318, copy in `renderVals()` from line 405). The mock agrees with the plan on direction 1b, the steps and the rail summaries. The frames are window mocks, not WPF specs: the dummy printers, the framed 8 px window with `--shadow-lg` and the `book/*.jpg` pages are illustrations. 1a, 1c and 0a are not built.

Tokens come from `docs/design/nocturne/styles.css`. Radii: `--radius-sm` 4, `--radius-md` 8, `--radius-lg` 14. Shadows: `--shadow-sm/md/lg` (a 1 px ring, plus blur on md and lg). Body 13 px / 1.4 Inter, `--color-text` on `--color-bg` (#161826). Phosphor is used at two weights only: regular (`ph`) and fill (`ph-fill`).

- **Shell (BookWizard lines 20-31).** Title bar 32 px, no fill. Left: `ph-fill ph-book-open-text` 16 px in `--color-accent`, "Print the damn book" 12 px `--color-neutral-300`, then "· <folder name>" 12 px `--color-neutral-500` once a folder is open. Caption buttons 46 px wide: `ph-minus` 14, `ph-square` 12, `ph-x` 14, all `--color-neutral-300`. Hover is 8% `--color-text`; close hover is #c42b1c with `--color-neutral-100`.
- **Rail (lines 62-86).** 236 px wide, padding 14/10, gap 4. Each step is a button: padding 10/12, `--radius-md`, a 26 px circle (12 px / 500), a 3 px accent mark at the left edge for the current step, title 13 px / 500, summary 11 px `--color-neutral-500` on one line with ellipsis. Current: 6% `--color-text` fill, number in `--color-accent` with a 1 px accent ring and a 14 px glow (45% accent). Done: `--color-accent-800` fill with `ph-check` 13 px in `--color-accent-200`. To do: 1 px `--color-divider` ring, `--color-neutral-500` number and title. A step is clickable only when reachable: not while busy, and not past Open until a folder is loaded. Bottom of the rail: cover thumbnail 42×59 (`--shadow-md`, 2 px radius, on `--color-neutral-100`), folder name 13 px / 500, status 11 px `--color-neutral-500`.
- **Main panel (lines 89-95).** Top-left corner `--radius-lg`, `--shadow-sm`, background a 180° gradient from `--color-surface` 55% over `--color-bg` to `--color-bg` at 70%. Content padding 24/28. Heading `--font-heading` 22 px / 500, line 1.15, letter-spacing -0.01em; sub-heading `--color-neutral-400`, max 62ch.
- **Footer (lines 293-300).** Min 56 px, padding 10/28, a fading top rule (`--color-divider`, transparent over the first and last 48 px). Status text on the left in `--color-neutral-400` (`--color-accent-300` while a print status shows, warning amber on failure). Then Back (`btn-secondary`, `ph-arrow-left`, on Check and Print only) and Next (`btn-primary`, `ph-arrow-right` trailing; on Print it is `ph-printer` leading, with an 18 px glow at 35% accent when enabled). Both disable while busy. The busy spinner is `ph-circle-notch`, 0.9 s per turn.
- **Open step (lines 109-150).** Not loaded: a dashed 1 px `--color-neutral-700` card, max 620, padding 36/32, `--radius-lg`, a radial wash of `--color-accent-900` from the top-left; `ph-folder-open` 40 px in `--color-accent` (`ph-circle-notch` spinning while reading); title 17 px / 500; hint in `--color-neutral-400`; a row of `tag-accent` "Cover", `tag-neutral` "1" "2" "3", "…", `tag-accent` "Back cover"; `btn-primary` with `ph-folder-open`. Loaded: a card on `--color-surface` with `--shadow-sm`, padding 20, the cover at **84×119** (`--shadow-md`), folder name 17 px / 500, path 12 px monospace `--color-neutral-500` (ellipsis), the status line in `--color-neutral-300`, and `btn-secondary` with `ph-arrows-clockwise`.
- **Check step (lines 98-110, 152-232).** Toggle and Add pages sit at the right of the heading row: `.seg` with `ph-book-open` "Reading order" and `ph-files` "As printed"; `btn-secondary` with `ph-plus`. Warning chips go under the heading: 12 px, padding 4/10, 6 px radius, `ph-fill ph-warning`, text in the warning colour on a 14% tint of it. **Warning colour: `oklch(0.82 0.12 78)`** (`static W`, line 321; tint is `color-mix(in oklch, W 14%, transparent)`).
  - Reading order: thumbnails **128×181**, 2 px radius, `--shadow-md`, on `--color-neutral-100`. A spread is two thumbs 2 px apart; the gap between spreads is 28 (rows) by 30 (columns). The caption under each spread is 10 px upper-case, letter-spacing .1em, `--color-neutral-600`: "Front cover", "Spread N", "Back cover" (the cover sits in the right slot of its row, the back cover in the left). The label under a thumb is 12 px `--color-neutral-300`; pinned covers show `ph-fill ph-push-pin` 11 px with the tooltip "Stays at the end".
  - A warned page: a 2 px ring in the warning colour instead of the shadow, a 22 px round badge at top-left (`ph-fill ph-warning` 13 px, `--color-bg` on the warning fill), the label in the warning colour, and an 11 px note under it. A blank filler is a dashed `--color-neutral-700` box with "BLANK" 11 px upper-case; the last blank is dashed in the warning colour when there is no back cover.
  - Remove: a 24 px round button at top-right, `ph-x` 12, 70% `--color-bg` fill, 75% opacity (full on hover, `--color-accent-300`), tooltip "Remove from the book". Only editable pages have it (not covers, not blanks). Dragging: the dragged page drops to 35% opacity; the target gets a 2 px `--color-accent` ring and a 22 px accent glow. Nothing can be dropped on the Cover; dropping on the Back cover inserts before it.
  - As printed (1d): sheet cards on `--color-surface`, `--shadow-sm`, `--radius-md`, padding 14/16/16, gap 16. Title "Sheet N" 13 px / 500, with a `tag-accent` "test sheet + covers" on sheet 1. "Front" and "Back" faces (10 px upper-case `--color-neutral-500`), each two tiles of **84×119**, 1 px apart, labels 11 px centred. The warning badge there is 18 px at a 4 px inset.
- **Print step (lines 228-290).** Two columns, gap 40. Left (max 420, gap 18): `.field` Printer as an `.input` select; Paper as `.seg` "A4" / "Letter"; a switch row; the test-sheet card. Switch: 36×20, 10 px radius, 1 px `--color-neutral-600` border, knob 12 px `--color-neutral-400` at 3 px; on: border and knob `--color-accent`, fill 22% accent, knob at 19 px. Label "Print the cover sheet on its own", hint 12 px `--color-neutral-500`. Test card: `--color-surface`, `--shadow-sm`, `--radius-md`, padding 14/16, `ph-arrows-counter-clockwise` 16 px in accent, title at 500, body 12 px `--color-neutral-400`, `btn-secondary` with `ph-printer`. Right (320): the stack preview in a 300×220 box. The front sheet is 264×187 on `--color-neutral-100`, split by a 1 px fading centre line (the fold), `--shadow-md`. Behind it are up to 4 layers on `--color-neutral-200` with `--shadow-sm`, each offset 7 px right and 6 px down, so `min(sheets, 5) - 1` layers.
- **Done step (lines 266-284).** Three cards in an auto-fit grid (min 200, gap 14, max 780): the number 28 px / 500 in `--color-accent`, title at 500, body 12 px `--color-neutral-400`. Below: `btn-secondary` `ph-printer` and `btn-ghost` `ph-folder-open`.
- **Dialog (lines 303-313).** The Nocturne `.dialog` over `.dialog-backdrop`, scoped to the main panel in the mock. It is the Slice 6 cover-then-reload dialog; in the app the backdrop should cover the whole window.

Copy, per step (from `renderVals`; N is the sheet count, n the page count):

- Open, empty: heading "Point me at your pages"; sub "Name them Cover, 1, 2, 3 … Back cover and I'll work out which page goes where on each sheet."; card title "Drop a folder here, or pick one" (while reading, "Reading images…"); hint "One image per page. JPG or PNG. I don't touch the files."; button "Open folder…". Rail summary "A folder of images". Status "Open a folder of page images to begin."
- Open, loaded: "Got your pages"; "Happy with this folder? Next you check the order."; "Pick a different folder"; Next "Next: check the order". Status "n pages on N sheets" ("1 page", "1 sheet" in the singular).
- Check: "Check the order"; sub "This is how the book reads. Drag a page to move it; the covers stay at the ends." (Reading order) or "This is what comes out of the printer. Each sheet prints both sides; fold the stack and it reads in order." (As printed). Toggle "Reading order" / "As printed"; "Add pages…". Rail summary: the folder name, then "N sheets" plus " · K to check". Chips: "K files have names I don't recognise" ("1 file has a name I don't recognise"), "No Back cover file", "N sheets is too thick to fold flat. 24 pages is about the limit." (above 6 sheets). Tile notes: "Name not recognised, so it went to the end.", "No “Back cover” file, so the back stays blank.", "Filler: a sheet holds 4 pages." Next "Next: printing".
- Print: "Send it to the printer"; "One job, double-sided. N sheets of A4." Rail summary "<first two words of the printer> · A4". Labels "Printer", "Paper", "Print the cover sheet on its own", "I'll pause after it so you can load thicker paper for the rest.", "First time with this printer?", "Print test sheet". Test text "Prints only Sheet 1, so you can check the printer flips the paper the right way before you spend the rest." (after sending: "Test sheet sent. Flip it over: page 1 should sit behind the cover, the right way up. Upside down? Switch the flip edge in your printer settings."). Stack title "N sheets · n pages · A4" and "Two pages per side, printed both sides. The line down the middle is where you fold." Next "Print the damn book".
- Footer while printing: "Sending the booklet to <printer>…" then "Sent the booklet to <printer>." (also "the test sheet", "the cover sheet", "the rest of the booklet"). After Cancel in the dialog: "The cover sheet was sent, but the rest of the booklet was not printed. You chose not to continue."
- Done: "Sent. Now fold the damn thing."; "Three steps and it's a book."; "1 Grab the stack: Take all N sheets off the tray. Keep them in the order they came out."; "2 Fold it in half: Fold the whole stack at once, cover on the outside. Run a thumbnail along the crease."; "3 Staple the spine: Two staples on the fold. That's a book. Go read it to someone."; "Print another copy"; "Make another book". Rail summary "Sent".
- Dialog: title "Cover's away"; body "The cover sheet has been sent. Load the paper for the inside pages, then press OK to print the rest."; buttons "Cancel", "OK, print the rest".
- Tooltips: "Remove from the book", "Stays at the end".

New strings: all of the above, in both locales if the app is localised.

**Open point: Test-sheet text.** Plan: the text points at the app's own Flip edge / Rotate backs options. Mock: "Switch the flip edge in your printer settings." Default: "Upside down? Change Flip edge, or turn on Rotate backs 180°, under More options." and keep the other sentences.

**Open point: Warning colour and derived colours.** Mock: `oklch(0.82 0.12 78)`, not a Nocturne token, plus many `color-mix()` tints and the 16% `--color-divider`. WPF has neither oklch nor `color-mix`. Default: convert once to sRGB and add `WarningBrush`, `WarningTintBrush`, `DividerBrush` (and the 6%/8% hover tints) to `Themes/Nocturne.xaml`, with the oklch source in a comment.

**Settled: "Too thick" chip.** The app already warns above `Imposition.MaxFlatSheets` sheets (`BookChecks` `BookIssue.TooThick`), so the chip shows that existing check in the mock's chip style and copy. This is not a behaviour change.

**Open point: Title-bar icon.** Mock BookWizard uses `ph-fill ph-book-open-text`; Store Kit 2a shows `app-icon-sm.svg` at 16 px. Default: the Phosphor glyph until Slice 08 lands, then the app icon.

**Open point: Paper choices.** Mock Paper is only A4 / Letter. Default: keep the two as a `.seg`; any other sizes the app offers go to More options.

**Open point: Fonts and icons offline.** The mock loads Inter from Google Fonts and Phosphor from unpkg. The plan bundles both, which is right; it needs the `regular` and `fill` Phosphor weights.

#### For CLAUDE.md (lifted 2026-10-22)

- UI tokens live in `src/PrintTheDamnBook.App/Themes/Nocturne.xaml` (from `docs/design/nocturne/styles.css`): use the brushes, never raw hex. Every focusable control uses the FocusRing style and has `AutomationProperties.Name`. Prompts go through `IPrintPrompts` / `NocturneDialog`, never `MessageBox`. (Slice 7: docs/plan/slices/07-wizard-redesign.md)

#### For README.md (lifted 2026-10-22)

- The app is now a three-step wizard (Open, Check, Print) ending with a fold guide.

#### Not yet verified

- Focus ring on the A4/Letter radios and switches, seen on screen.
- Done-step footer wrap, seen on screen.
- The stuck drag tooltip (not reproduced).
- Snap layout on a high-DPI monitor.
