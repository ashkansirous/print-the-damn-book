---
name: kdp-style
description: Choose the per-book looks of the Amazon KDP story text of one Dino book and language - a colour for each speaking character, the main character (The End's colour) and the round title picture - with the kdp-style-check agent from small uncut copies of the pictures, and save them in kdp.json through the CLI flags (--speaker, --main-character, --title-picture). Never opens a full-size picture. Use before building a book that has no saved styles, when the owner says "choose the colours for <book>", "style the book", or from the kdp-build skill (it calls this first when kdp.json has no styles).
---

# kdp-style

The KDP interior colours each character's quoted words, sets The End in the main character's colour and shows one round picture on the title page
(Slice 46). Which colour suits which character and which picture frames the hero are decisions for eyes, not for code: the agent decides them from small
uncut copies of the pictures, the tool checks the colour rules and saves the choice in `kdp.json` (`styles`). With no saved choice the build still works (a fallback
palette, the speaker with most lines, the first picture centred) and says "fallback" in `check-report.md`.

Output folder: the one `kdp-build` uses, `<repo root>/tmp/kdp-out/<book>-<lang>/`. The book is a local `.dino` (the owner's are in `D:\high-res-books\`, read in
place; see `kdp-build`). English books only for now (Slice 46: other languages get their word lists in Slice 33). Nothing here uploads or signs in anywhere.

## Loop

1. **Make the style sheet.** From the repo root (a worktree when one is in use):

   ```powershell
   dotnet run --project src/PrintTheDamnBook.Cli -- kdp <book.dino> --language <code> --style-sheet --out tmp/kdp-out/<book>-<code>
   ```

   It writes `styles/speakers.md` (each speaker with lines, pages and the colour in use, the main-character fallback, the page tints, the title picture's
   measuring rule) and the 480 px `crop/<key>.png` copies beside the PDFs.
2. **Keep the owner's choices.** Read `kdp.json` in the output folder if it exists: a speaker colour, a main character or a title picture already in `styles`
   was chosen by the owner (or by an earlier run) and is kept. Decide only what is missing, unless the caller said to redo all (then pass `=clear` first, see step 4).
   When the sheet lists no speaker, only the title picture is decided.
3. **Dispatch `kdp-style-check`** (Agent tool, subagent_type `kdp-style-check`, **one agent per book**) with the output folder path. The brief names the speakers
   still to decide (the sheet's list minus the saved ones), whether the main character is still to decide and whether the title picture is. It opens `styles/speakers.md`
   and the 480 px `crop/<key>.png` of the pages it needs; never `crop/sheet.png` as a basis, never a full-size picture. It answers
   `<name> = #RRGGBB — <reason>` per speaker, `main = <name> — <reason>` and `title = <page> <cx>,<cy>,<d> — <reason>`, and `seen: n pictures`.
   **Refuse to apply an answer for something the agent did not see:** `NOT SEEN` or a missing line means nothing is applied for it; dispatch a fresh agent whose
   brief lists only the missing items, at most 2 redispatches per book, then report the book as `not checked` with the items.
4. **Apply** with one run (it also rewrites the sheet so the folder is ready to build):

   ```powershell
   dotnet run --project src/PrintTheDamnBook.Cli -- kdp <book.dino> --language <code> --speaker "Seagull=#0E6B78" --speaker "Kathryn=#1F4FA0" --main-character "Kathryn=the hero, in every picture" --title-picture 4=0.52,0.38,0.8 --style-sheet --out tmp/kdp-out/<book>-<code>
   ```

   The tool refuses a colour that fails the rules (not dark enough on white or on a page tint, red, too close to another speaker or a triplet colour) with the reason
   and writes nothing; a refused colour goes back to the agent once with that reason (a fresh agent, the one speaker only), and if it is refused again the speaker
   is reported as an owner question and keeps the fallback colour. `--speaker "<name>=clear"`, `--main-character clear` and `--title-picture clear` remove a saved
   choice (only in redo-all mode). The choices are saved in `kdp.json` (written last), so every later run keeps them.
5. **Confirm** the run's warnings: no `styles: ... (fallback)` line is left for what was decided. A warning that a saved colour is refused names the speaker and the rule.

## Stop rules

- **Two runs of the command, one agent round** (plus at most 2 redispatches of step 3 and one refused-colour retry). A colour the owner dislikes is the owner's `--speaker`, not another agent round.
- An agent answer of `unsure` is applied as given and listed in the report.
- A run that errors (exit 1) twice with the same message: stop and report it. Never edit `kdp.json`, the `.dino` or the Core maths by hand.

## Report (about 6 lines)

Verdict first: `styled` or `unsure on <names>`. Then each speaker's colour with the agent's reason, the main character, the title picture (page, centre, diameter), what was kept from
`kdp.json`, any `fallback` warning left, the folder.
