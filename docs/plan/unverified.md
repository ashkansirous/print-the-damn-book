# Not yet verified

Every open "Not yet verified" check, one block per slice, newest first. Remove a line when it passes on a device or the user confirms it.

### Slice 36: Amazon KDP: an agent picks every crop, cover without white boxes, tinted text pages ([slice](slices/36-kdp-crop-agent-and-cover.md))

- [owner] The owner looks at the Freddy cover and text spreads (H, rebuilt in `D:\print-the-damn-book-worktrees\s36\tmp\freddy-final`) and says whether they look right; the first cover was rejected on 2026-11-20.
- [owner] A printed proof of the cover and interior.

### Slice 35: Amazon KDP follow-ups ([slice](slices/35-kdp-followups.md))

- [session] Confirm on Amazon's KDP help pages the 8.5 x 8.5 in trim, the keyword character limit and the supported description HTML tags, and cite each in `listing-limits.mjs` (item H).

### Slice 30: Amazon KDP: `kdp-build` skill and visual-check agent ([slice](slices/30-kdp-build-skill.md))

- [session] A fresh Claude Code session runs `kdp-build` on two free books and ends with a clean report and the files. (live check 2026-11-20: failed, follow-up in Slice 35 item A)

### Slice 29: Amazon KDP: cover PDF ([slice](slices/29-kdp-cover-pdf.md))

- [owner] **You:** upload Freddy's cover with the interior (the owner rejected the first Freddy cover on 2026-11-20; the upload waits for the Slice 36 rebuild in `D:\print-the-damn-book-worktrees\s36\tmp\freddy-final` and the owner's answers to Slice 36's two open desk choices) | at https://kdp.amazon.com/en_US/bookshelf upload `cover.pdf` and `interior.pdf` from the `kdp` command's output, pass KDP's cover check (watch the 0.0012 in page-size rounding) and order a proof.
- [owner] A real Farsi book's cover (no free book has Farsi text; the lion fa check was layout only).

### Slice 28: Amazon KDP: print-ready pictures ([slice](slices/28-kdp-print-ready-pictures.md))

- [session] Look at the previews of the 7 pictures flagged on the 12 store books (run 2026-11-20, counts in Slice 35 item G) and of the unflagged ones scoring 2.0-2.2, to tell whether `CutLineCheck.FlagRatio` 2.5 under-flags: rerun those books with `--previews`: https://github.com/ashkansirous/print-the-damn-book/blob/main/docs/plan/slices/28-kdp-print-ready-pictures.md
- [owner] The printed proof decides whether an AI upscaler is needed (settles the upscaler question).

### Slice 31: Amazon KDP: listing pack and `kdp-listing` skill ([slice](slices/31-kdp-listing-pack.md))

- [owner] **You:** paste the Freddy listing into KDP | open https://kdp.amazon.com/en_US/bookshelf, create the paperback, paste each field from https://github.com/ashkansirous/print-the-damn-book/tree/main/kdp/listings and note any field that needed rewriting.
- [owner] **You:** pick the KDP categories and confirm the print cost | in the paperback form at https://kdp.amazon.com/en_US/bookshelf choose the real categories (the tree needs your login) and confirm the 4.20 print cost in KDP's calculator.

### Slice 27: Amazon KDP interior PDF for one Dino book ([slice](slices/27-kdp-interior-pdf.md))

- [owner] **You:** upload the interior PDF as a KDP paperback draft | at https://kdp.amazon.com/en_US/bookshelf upload `interior.pdf` in the paperback content step and note the previewer warnings in the slice file: https://github.com/ashkansirous/print-the-damn-book/blob/main/docs/plan/slices/27-kdp-interior-pdf.md

### Slice 23: Text that doesn't fit gets its own page, no empty lines, Dino back cover ([slice](slices/23-fit-and-back-cover.md))

- [owner] Print to PDF a store Dino book with a back cover in its own language and in Farsi: the credits are in the middle of the last outside page in that language, with no blank page after it (the credits are drawn into the picture; no script can read them).
- [session] A long book (40+ pages) stays responsive while editing text. (live check 2026-11-20: unreachable: VM not started, 5.1 GB free under the 8 GB threshold, and no tools/desktop-test script covers it)
- [owner] The real paper print and fold.

### Slice 25: Dino language switch translates every page ([slice](slices/25-dino-language-switch-pages.md))

- [owner] On the user's PC, Love from Seagull Island opened (already saved as `fa` over English) shows Farsi on all 16 pages, and switching fa, en, fa keeps that.
- [session] No VM run during Slice 25's own build (free memory 7.0 GB, under the 8 GB VM threshold). Slice 23's VM run at 14:23Z included Slice 25's code and passed 6 of 6, but no scripted check switches a language yet (Slice 26 item). (live check 2026-11-20: unreachable: VM not started, 5.1 GB free under the 8 GB threshold, and no tools/desktop-test script covers it)

### Slice 21: Scripted desktop checks ([slice](slices/21-desktop-test-harness.md))

- [session] `store-dialog` (list, download, Cancel) FAILED on the host on 2026-11-18: the list loaded 16 stories, but downloading the first ('Goldilocks and the Three Bears', a paid book) showed no progress bar or result in 60 s and Cancel was not reached, because the store answers it with HTTP 402. Likely cause, not proven: the app shows nothing on a 402 and the check picks the first story whatever its price. The cause is unproven (`DinoStoreClient.SendAsync` throws a Refused error on any non-2xx), so reproduce it first: Slice 26 item "Store dialog" (feedback loop 2026-11-18-1302), not Slice 24, which is skipped; log `D:/print-the-damn-book-worktrees/reviews/s21-store-dialog.log`. (live check 2026-11-20: unreachable: store-dialog runs only on the host desktop with -AcknowledgeHostDesktop, which needs someone at the machine; unattended run)

### Slice 20: Dino open and store hardening ([slice](slices/20-dino-open-hardening.md))

- [owner] The failed state with Back to list on a real download failure (cannot be forced without changing live state).
- [session] The hand-over alert for an `IOException` hand-over (unit-tested only). (live check 2026-11-20: unreachable: VM not started, 5.1 GB free under the 8 GB threshold, and no tools/desktop-test script covers it)
- [session] The "in use by another window" message when two windows download the same book (unit-tested only). (live check 2026-11-20: unreachable: VM not started, 5.1 GB free under the 8 GB threshold, and no tools/desktop-test script covers it)

### Slice 18: Get a story from Dino's store ([slice](slices/18-dino-store.md))

- [session] The language filter with more than one language, and Farsi/Arabic store cards (the live store has only English books; with English only, the filter and all 16 cover thumbnails passed on 2026-11-01). (live check 2026-11-20: unreachable: live store catalog has 16 books, all en, reviews/lc-2026-11-20/catalog.json)

### Slice 17: Right-to-left books (Farsi and Arabic) ([slice](slices/17-right-to-left-books.md))

- [owner] Paper print and fold of one Farsi book (user).
- [session] The font the Farsi text is drawn in (Vazirmatn): the 2026-11-01 run saw Arabic-script glyphs but did not read the font name from the UI tree. (live check 2026-11-20: unreachable: VM not started, 5.1 GB free under the 8 GB threshold, and no tools/desktop-test script covers it)
