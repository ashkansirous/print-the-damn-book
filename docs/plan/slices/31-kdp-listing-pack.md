### Slice 31 — Amazon KDP: listing pack and `kdp-listing` skill

*Planned 2026-11-19 from the Scope Inbox doc
[2026-11-18-amazon-kdp-export.md](https://drive.google.com/file/d/16B6KoIGXtHRXaRS8VvQcopP5RyJhcUIM/view?usp=drivesdk)
(milestone 5).*

**Touches:** `.claude/skills/kdp-listing/**` (new), `kdp/listings/**` (new)

**Depends on:** Slice 27 (page count per book, for price). Independent of 28–30 otherwise.

#### Why

Acceptance (doc): the owner can paste each book's listing into KDP without writing anything.

#### Decisions

- Approved (doc): per book and language: title, subtitle, series "Dino Stories", description, 7 keywords, categories,
  reading age, contributors, AI-disclosure answers, price, ISBN log. AI disclosure is **Yes** for text, images and
  translations (KDP content guidelines: AI-made counts even after edits). Paperback royalty is 60% from $9.99 (50% below).
- Decided by this planning session, 2026-11-19:
  - One Markdown file per book and language, `kdp/listings/<book-id>/<lang>.md`, with one heading per KDP form field in
    the form's order, so it is pasted top to bottom.
  - Contributors come from the `.dino`'s `DinoCredits` (today only Freddy credits Elham Shafiei Gol and Ashkan Sirous;
    the other books' credits are a ReadTheStupidText spin-off, doc *Spin-offs*).
  - Price: the skill proposes the lowest price at or above $9.99 that gives a positive royalty after KDP's printing cost
    for the book's page count, with the sum shown; the printing-cost figures are read from KDP's help pages in that run.
  - ISBN log: `kdp/listings/isbn-log.md`, one line per book, language and format, filled by the owner after KDP assigns
    the free ISBN.

#### Work

- [ ] `kdp-listing` skill: reads the `.dino` and the check report, writes the listing file, checks KDP's field limits
  (read from KDP's help pages in the run) and keyword rules.
- [ ] Freddy (English) listing written by the skill.

#### Verification

- The Freddy listing passes the skill's own limit checks `[session]`.
- `[owner]` Paste it into the KDP paperback form at https://kdp.amazon.com/en_US/bookshelf; any field that needed
  rewriting is written here.
