---
name: kdp-build
description: Build the Amazon KDP files of one Dino book and language (interior.pdf, cover.pdf, check-report.md) with the repo's kdp command, read the check report, have the kdp-visual-check agent look at the page previews, fix what can be fixed (a crop offset for a cut picture) and rerun until the report is clean or only owner questions are left. Never uploads to KDP. Use when the user says "build the KDP files", "make the KDP book for <book>", or a slice asks for a proof-ready book.
---

# kdp-build

Output folder: `<repo root>/tmp/kdp-out/<book>-<lang>/` (`<book>` is the `.dino` file's name without its extension), inside the repo or worktree you
run from, in the git-ignored `tmp/` so no PDF is committed by accident and nothing is written outside the working directory (the command creates it). The book is a local `.dino` file.

**Where the books are:** the owner keeps the high-resolution `.dino` of every Dino store book, paid ones included, in
`D:\high-res-books\` (`<Title> v<N>.hd.dino`, for example `Freddy and the Slipper v10.hd.dino`; owner, 2026-11-20). Whenever the
owner names a book, look there first (match the title, take the highest `v<N>` if several), then `Book sample/`. Ask for a path only
when neither has it. Read the folder in place; never copy, edit or commit a book from it.

This skill never uploads, signs in or submits anything on KDP.
Uploading is the owner's step; the listing text is the `kdp-listing` skill's job.

## Loop

1. **Run the command**, from the repo root (a worktree when one is in use):

   ```powershell
   dotnet run --project src/PrintTheDamnBook.Cli -- kdp <book.dino> --language <code> --previews --out tmp/kdp-out/<book>-<code>
   ```

   **Other books on the last page (Slice 41):** for every book add `--other-books D:/high-res-books --books-out <folder that holds each book's output folder>`
   (this repo's `tmp/kdp-out` for a build here, or the folder of the rebuilt books). The last interior page then shows "More books by ..." with up to 15
   other books' front covers (this book never; more than 15 are picked at random, seeded by this book's id, so a rebuild gives the same page) and the Dino
   line with its QR code at the bottom. Each cover is titled as on that book's own `kdp.json`; without `--books-out` the output folder's parent is used.
   Without `--other-books` the last page stays as before. The command prints the titles it used.

   Add `--offset <story page|front|back>=<pixels>` for each offset chosen so far in this run (they are saved in `kdp.json` in the output
   folder, so a later run keeps them). Exit 0 means the files were written; 1 means a failure: either the book could not be made (read the `Error:` line), or it was made but something is wrong. A story page whose words
   do not fit their box at the one 24 pt size (Slice 42) writes every file and exits 1; `check-report.md` then has an `## Errors` section naming each page
   ("Page N: the words do not fit their box ..."), and the same page is an `Error:` line on stderr. That book did build: do not count it as "the same
   error twice, stop"; the fix is shorter words on that page (a text change by the owner, never a smaller size), so report the page to the owner. A cover or
   preview failure also leaves `interior.pdf`, the report and `kdp.json`; 2 is a wrong argument. A run without `--previews`
removes the previews of an earlier run.
2. **Read `check-report.md`**: page count (even, at least 24), fonts embedded, text pages facing their pictures, the `## Cover` section
   and every warning at the end.
3. **Choose the crops first: run the `kdp-crop` skill** (once per build, before any visual check; Slice 36). It makes the crop sheet, has the
   `kdp-crop-check` agent decide every picture's window (story pages, `front`, `back`) from small uncut copies, and (Slice 37, for every book) the
   cover words too: the title's box and colours on the front, the credit lines' box and colours on the back, and the colours of the Dino line beside the
   QR code; it saves the offsets and the words choices (`--text`, `--dino-text`) in `kdp.json` and reruns with `--previews`. Its two runs are not counted in the 4 below. Never cut a book and look afterwards instead.
   **Dispatch `kdp-visual-check`** (Agent tool, subagent_type `kdp-visual-check`) with the output folder path. It reads only the contact
   sheet and small page PNGs. Never open a full-size picture or the PDF's pages yourself.
4. **Sort what is left** into fixable and owner questions:
   - *Fixable:* a picture still cut through a character after `kdp-crop` (report warning "may be cut through a character", or the agent says
     so). Open `previews/page-NN.jpg` (small, red line on the cut), choose a pixel offset (steps of about 5 percent of the picture height, the
     report's `Kept` column shows the current crop; "clamped" in the report gives the most the picture allows), set it with `--offset`.
     `--offset <page|front|back>=0` clears one. A page whose offset `kdp-crop` saved is a choice already made: change it only with a reason
     the agent can see in the preview.
   - *Owner questions, never fixed here:* low resolution ("pictures are enlarged ... dpi", the source art is what it is), words that
     do not fit, a missing back cover or credits, a language whose front matter is English only, a picture the agent says is out of
     story order, a cover that failed, a book under 24 pages' worth of pictures. Quote the report line.
5. **Rerun** (step 1) after changing offsets. Compare the new report and agent verdict with the last one.

## Stop rules

- **Clean:** the report has no warning except the known owner-question kinds in step 4, and the agent says `clean`. Finish.
- **At most 4 runs** in one skill call. Two offset tries on the same page that still flag it: stop trying that page and list it as an owner
  question with the two previews.
- A run that errors (exit 1) twice with the same message: stop and report the message; do not patch the book.
- Never edit the `.dino`, the Core maths or the report by hand. A wrong result from the tool is a finding for a slice, not something to work around.

## Report (about 10 lines)

Verdict first: `clean` or `owner questions left`. Then the output folder with `interior.pdf`, `cover.pdf`, `check-report.md`,
`contact-sheet.png`; page count; the offsets set (`page|front|back=pixels`, from `kdp-crop` and by hand); each remaining owner question with its report line; how many runs it took.
