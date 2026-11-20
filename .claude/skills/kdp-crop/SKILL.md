---
name: kdp-crop
description: Choose the crop window of every picture of a Dino book for the Amazon KDP square page. Runs the kdp command with --crop-sheet, has one kdp-crop-check agent per book look at the 480 px uncut picture of every key and decide each window (story pages, front cover, back cover) and, for the front and back covers, where the cover words go, their colour and their shadow colour; applies the offsets with --offset and the words with --text and --dino-text (all saved in kdp.json) and reruns. Runs for every book. Never opens a full-size picture. Use before cutting any book, when the owner says "check the crops", "choose the crops for <book>", or from the kdp-build skill (it calls this first).
---

# kdp-crop

Look before cutting: the tool drops about a quarter of each 3:4 Dino picture to fit the square page, and where it cuts is a decision, not a default
(Slice 36). The agent decides it from a small uncut copy of every picture, so no full-size picture is ever opened.

Since Slice 37 the same agent also decides the cover words: for `front` (the title only) and `back` (the credit lines) a text box in the empty space
of the kept window, the text colour and the shadow colour, and the colours of the Dino line beside the back's QR code. **This runs for every
book, never only the one the owner is looking at**; the tool's own rule (a calm band, white words, dark shadow) is only the fallback for a cover
with no saved choice.

Output folder: the same one `kdp-build` uses, `<repo root>/tmp/kdp-out/<book>-<lang>/` (git-ignored `tmp/`). The book is a local `.dino`
(the owner's are in `D:\high-res-books\`, read in place, never copied; see `kdp-build`). Nothing here uploads or signs in anywhere.

## Loop

1. **Make the crop sheet.** From the repo root (a worktree when one is in use):

   ```powershell
   dotnet run --project src/PrintTheDamnBook.Cli -- kdp <book.dino> --language <code> --crop-sheet --out tmp/kdp-out/<book>-<code>
   ```

   It writes `crop/sheet.png` (every picture whole, about 200 px wide, the kept window outlined, labelled `<key> y=<applied>/<max>`) and
   `crop/<key>.png` (480 px wide) beside the PDFs. Keys are the story page numbers, `front` and `back`.
2. **Keep the owner's choices.** Read `kdp.json` in the output folder if it exists: every page or cover in it was chosen by the owner (or by an
   earlier run of this skill) and is kept as it is. Decide only the other keys, unless the caller said to redo them all (then every key is
   undecided and the new offsets replace the saved ones). The cover words are kept the same way: a cover whose entry has `text` (and `dino` on
   the back) keeps its saved words choice, and a cover with an offset but no `text` still gets its words decided (a file from Slice 36 has none).
3. **Dispatch `kdp-crop-check`** (Agent tool, subagent_type `kdp-crop-check`) with the output folder path, **one agent per book** (one agent
   hit an image limit at about 250 reads in the trial; a book is about 15 to 30 pictures). **The brief lists the expected keys**: the file names
   in `crop/` without `.png` (Glob `crop/*.png`, minus `sheet`), in the sheet's order, so the agent and the skill check "a line for every key"
   against the same list, and `seen: n of n` means that list's length. It opens the 480 px `crop/<key>.png` of every key
   (`crop/sheet.png` is an overview only, never the basis of a decision; the trial of 2026-11-20 showed it misses feet, legs and small
   animals cut at the bottom edge). It answers one line per key: `<key> | <offset> | f=<fraction> | <reason>`, and `seen: n of n`; then, for
   `front` and `back`, the cover-words lines `front text | box=<left>,<top>,<width>,<height> | text=#RRGGBB | shadow=#RRGGBB | <reason>`,
   `back text | ...` and `back dino | text=#RRGGBB | shadow=#RRGGBB | <reason>` (or `<key> text | no box | <reason>`). The words are decided
   **only from what the agent saw**: a cover in `NOT SEEN` has no words lines, and the skill never supplies a box or colour itself.
   **Refuse to apply a result that does not list every key as seen:** if the answer says `NOT SEEN`, lacks a line for any key that has a
   key of the expected list, or reports a read failure, apply nothing for the keys not seen (keep the lines for keys that were seen); dispatch a
   fresh agent for that book whose brief lists **only the missing keys**, never the whole book again (a guess for a picture nobody saw is never
   applied), and merge its lines with the earlier ones. At most **2 redispatches per book** for `NOT SEEN` or a missing key; if keys are
   still not seen after that, stop for that book, apply nothing for those keys and report the book as `not checked` with the keys.
4. **Apply.** For every key the agent decided whose offset differs from the `y=` in its label (and that has no saved choice), pass
   `--offset <key>=<pixels>`; an answer of `0` for a key with a saved offset is `--offset <key>=0` (it clears the entry) **only in redo-all mode**; otherwise a
   key with a saved offset is not decided at all and its saved choice is kept. For a key with none saved, `0` needs nothing. A key whose `max` is 0 needs nothing. Rerun with all the offsets, `--crop-sheet` and `--previews` so the folder is ready for the
   visual check. Pass each cover-words line the agent gave for a cover that has no saved choice as `--text <front|back>=<left>,<top>,<width>,<height>,<text>,<shadow>`
   (the numbers and the two `#RRGGBB` colours as the agent wrote them) and the `back dino` line as `--dino-text back=<text>,<shadow>`; a `no box` answer passes nothing
   (the fallback stays). `--text <key>=clear` and `--dino-text back=clear` remove a saved choice, **only in redo-all mode**, and there **every
   `--offset <key>=<pixels>` (including `=0`) that changes a cover's offset, and every cover whose new answer is `no box`, is issued together with `--text <key>=clear`** (for `back`, when the agent gave no `back dino` colours, also `--dino-text back=clear`), because `--offset` alone keeps the words
   choice and a box chosen for the old window would stay; a key given a new `--text` line needs no `clear` (the new line replaces it), and `--dino-text back=clear` is left out when a new `back dino` line is given. Normal mode keeps saved words choices:

   ```powershell
   dotnet run --project src/PrintTheDamnBook.Cli -- kdp <book.dino> --language <code> --offset 4=120 --offset back=582 --text "front=0.12,0.05,0.76,0.2,#FFF6D8,#1B2A4A" --text "back=0.3,0.06,0.62,0.24,#FFFFFF,#2A1A10" --dino-text "back=#FFFFFF,#2A1A10" --crop-sheet --previews --out tmp/kdp-out/<book>-<code>
   ```

   The offsets and the words choices are saved in `kdp.json` (written last), so every later run keeps them. A cover warning that a saved text box is
   unusable or the title does not fit it (the build says "fallback") means the agent's box was too small or too close to the margin or barcode: say so in the report; the
   cover still builds with the fallback placement. A warning "no effect" or "more than the picture allows"
   means the agent's number was wrong for that picture: use the `y=` the new label shows and say so in the report.
5. **Confirm.** Open the new `crop/sheet.png` once (small) and check each label's `y=` equals what was asked and the outlines moved the way the
   agent's reasons say. Nothing else to look at for the crops; the cover words are looked at by the owner (a small render of the front title and
   the back credits) and by the `kdp-visual-check` agent in `kdp-build`.

## Stop rules

- **Two runs of the command, one agent round** (plus at most 2 redispatches of step 3 for a book whose answer was incomplete). Do not loop: a window the owner dislikes is the owner's `--offset`, not another agent round.
- An agent answer of `unsure` on a key is applied as given and listed in the report so the owner can look at `crop/<key>.png`.
- A run that errors (exit 1) twice with the same message: stop and report it. Never edit the `.dino`, `kdp.json` or the Core maths by hand.

## Report (about 8 lines)

Verdict first: `cropped` or `unsure on <keys>`. Then the offsets set (`key=pixels`, with the agent's reason for each that is not 0), the cover words set
(`front`, `back`, `dino`: box, colours, reason), the keys kept from `kdp.json`, any "fallback" warning, the folder, and how many runs it took. Never paste the sheet; the folder path is enough.
