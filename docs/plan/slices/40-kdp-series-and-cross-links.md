### Slice 40 — Amazon KDP: series, "Also by" page, author page, and a short link to the Dino app

*Planned 2026-11-20 with the owner (interactive session, RefineScope).*

**Touches:** `kdp/catalog.json` (new, from Slice 38), `kdp/author/` (new), `tools/kdp-robot/` (new, from Slice 38),
`src/PrintTheDamnBook.Core/Kdp/`, `src/PrintTheDamnBook.Kdp/`, `tests/PrintTheDamnBook.Core.Tests/Kdp/`,
`tests/PrintTheDamnBook.Kdp.Tests/`, `.claude/skills/kdp-publish/` (new, from Slice 38), `docs/plan/slices/40-kdp-series-and-cross-links.md`

**Migrations:** none

**Depends on:** Slice 38; Slice 39 for languages beyond English; ReadTheStupidText Slice 423 (the short links) before the
QR codes point at them. Slice 37 overlaps (`src/PrintTheDamnBook.Kdp/` cover drawing): run after it.

#### Why

The owner wants readers of one book to find the others: books of the same series linked, other books named, and the Dino
app reachable from every book, with a link that survives a future domain change.

#### Decisions

Approved by the owner, 2026-11-20 (RefineScope):
- **Series "Little Seagull’s Big Adventures"** (answer 3) holds X Marks the Treasure and Love from Seagull Island. Each
  language gets its own series, numbered, so readers of one language see only books in that language.
- **Short links** (answer 4, "yes"): books print `https://dinoreader.sirous.uk/book/<book-id>`, which forwards to the
  Dino app page; if the owner buys a domain later, only the forward changes, never a printed book or a listing.
- Not objected to (defaults given with the answers): every book gets an **"Also by Elham Shafiei Gol and Ashkan Sirous"**
  page inside, with a QR code per book; the robot keeps the authors' **Amazon author page** (Author Central) up to date;
  **no web links in book descriptions** (KDP's link rules for Kindle books allow links only when they help the reader
  and ban other ebook stores, https://kdp.amazon.com/en_US/help/topic/GQ6JQ7FM6C72HE4X; no rule found for the description
  field, 2026-11-20).

Decided by this planning session, 2026-11-20 (open points the owner can change):
- The other 14 books keep series "Dino Stories" (the Freddy listing's existing series, Slice 31), unless the owner names
  other groups.
- Series order: Love from Seagull Island is book 1 and X Marks the Treasure book 2 until the owner says otherwise.
- The series name in other languages is translated by `kdp-listing` like the titles and stored in the catalog.

#### Work

- [ ] A. Catalog: series name per language and number per book; checker refuses two books with the same number in one
  series and language.
- [ ] B. Robot: series fields on *Paperback Details* (KDP's "Add series details", https://kdp.amazon.com/en_US/help/topic/G201097560),
  and the series page description in KDP's series manager.
- [ ] C. "Also by" page: Core `Kdp/` decides the page list (other books in the same language from the catalog, the
  series first) and where it sits in the page plan, keeping the page count even and within KDP's limits; the Kdp project
  draws it with a QR code per book to its short link (QRCoder `PngByteQRCode`, CLAUDE.md). Tests for order, page count
  and that each QR code decodes to its short link.
- [ ] D. Back-cover QR code (Slice 36) points at `https://dinoreader.sirous.uk/book/<book-id>` once ReadTheStupidText
  Slice 423 is live (checked by a request returning the forward).
- [ ] E. Author page: bio per language in `kdp/author/<lang>.md`; robot command `author` opens
  https://author.amazon.com, claims any book not yet on the page, and updates the bio; summary like Slice 38's.

#### Verification

- Unit tests for A and C.
- Live `[session]`: `curl -sI https://dinoreader.sirous.uk/book/love-from-seagull-island` shows the forward; the two series
  books' drafts show the series and numbers; the author page lists the published books.
- `[owner]` publishes the drafts. The series page appears on Amazon about 72 hours after the books are live
  (https://kdp.amazon.com/help/topic/G83483M7NAQMBX46); checked by this repo's next live-checks session `[chrome]`.

#### Not yet verified

_(none yet)_
