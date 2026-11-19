# Not yet verified

Every open "Not yet verified" check, one block per slice, newest first. Remove a line when it passes on a device or the user confirms it.

### Slice 31: Amazon KDP: listing pack and `kdp-listing` skill ([slice](slices/31-kdp-listing-pack.md))

- [owner] Paste the Freddy listing into the KDP paperback form at https://kdp.amazon.com/en_US/bookshelf and write any field that needed rewriting.
- [owner] Pick the real KDP categories (the category tree needs the login) and confirm the 4.20 print cost in KDP's calculator.

### Slice 27: Amazon KDP interior PDF for one Dino book ([slice](slices/27-kdp-interior-pdf.md))

- [owner] A Freddy and the Slipper `.dino` from the owner's own copy (paid store book, download refused), then build its interior with the command.
- [owner] Upload the interior PDF as a KDP paperback draft at https://kdp.amazon.com/en_US/bookshelf and record the previewer warnings.

### Slice 23: Text that doesn't fit gets its own page, no empty lines, Dino back cover ([slice](slices/23-fit-and-back-cover.md))

- [owner] Print to PDF a store Dino book with a back cover in its own language and in Farsi: the credits are in the middle of the last outside page in that language, with no blank page after it (the credits are drawn into the picture; no script can read them).
- [session] A long book (40+ pages) stays responsive while editing text.
- [owner] The real paper print and fold.

### Slice 25: Dino language switch translates every page ([slice](slices/25-dino-language-switch-pages.md))

- [owner] On the user's PC, Love from Seagull Island opened (already saved as `fa` over English) shows Farsi on all 16 pages, and switching fa, en, fa keeps that.
- [session] No VM run during Slice 25's own build (free memory 7.0 GB, under the 8 GB VM threshold). Slice 23's VM run at 14:23Z included Slice 25's code and passed 6 of 6, but no scripted check switches a language yet (Slice 26 item).

### Slice 21: Scripted desktop checks ([slice](slices/21-desktop-test-harness.md))

- [session] `store-dialog` (list, download, Cancel) FAILED on the host on 2026-11-18: the list loaded 16 stories, but downloading the first ('Goldilocks and the Three Bears', a paid book) showed no progress bar or result in 60 s and Cancel was not reached, because the store answers it with HTTP 402. Likely cause, not proven: the app shows nothing on a 402 and the check picks the first story whatever its price. The cause is unproven (`DinoStoreClient.SendAsync` throws a Refused error on any non-2xx), so reproduce it first: Slice 26 item "Store dialog" (feedback loop 2026-11-18-1302), not Slice 24, which is skipped; log `D:/print-the-damn-book-worktrees/reviews/s21-store-dialog.log`.

### Slice 20: Dino open and store hardening ([slice](slices/20-dino-open-hardening.md))

- [owner] The failed state with Back to list on a real download failure (cannot be forced without changing live state).
- [session] The hand-over alert for an `IOException` hand-over (unit-tested only).
- [session] The "in use by another window" message when two windows download the same book (unit-tested only).

### Slice 18: Get a story from Dino's store ([slice](slices/18-dino-store.md))

- [session] The language filter with more than one language, and Farsi/Arabic store cards (the live store has only English books; with English only, the filter and all 16 cover thumbnails passed on 2026-11-01).

### Slice 17: Right-to-left books (Farsi and Arabic) ([slice](slices/17-right-to-left-books.md))

- [owner] Paper print and fold of one Farsi book (user).
- [session] The font the Farsi text is drawn in (Vazirmatn): the 2026-11-01 run saw Arabic-script glyphs but did not read the font name from the UI tree.
