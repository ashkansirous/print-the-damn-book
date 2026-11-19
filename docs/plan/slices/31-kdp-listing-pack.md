### Slice 31 — Amazon KDP: listing pack and `kdp-listing` skill

*Planned 2026-11-19 from the Scope Inbox doc
[2026-11-18-amazon-kdp-export.md](https://drive.google.com/file/d/16B6KoIGXtHRXaRS8VvQcopP5RyJhcUIM/view?usp=drivesdk)
(milestone 5).*

*Built 2026-11-19 (wave 2026-11-19), PR #70, not merged; the paste into KDP is not yet verified.*

**Touches:** `.claude/skills/kdp-listing/**` (new), `kdp/listings/**` (new)

**Migrations:** none

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

- [x] `kdp-listing` skill: reads the `.dino` and the check report, writes the listing file, checks KDP's field limits
  (read from KDP's help pages in the run) and keyword rules.
- [x] Freddy (English) listing written by the skill.

#### Built

- `.claude/skills/kdp-listing/` (`SKILL.md`, `listing-limits.mjs`, `check-listing.mjs`, `listing-limits.test.mjs`),
  `kdp/listings/freddy-and-the-slipper/en.md`, `kdp/listings/isbn-log.md`.
- KDP limits were read from Amazon help pages on 2026-11-19 (cited in the skill). Unconfirmed: the 8.5 x 8.5 trim, the
  keyword character limit and the supported description HTML tags.
- Freddy's page count 34 is `KdpPagePlan.TotalPages(14)` (14 pictures from the store catalog), not from a check-report: the
  paid `.dino` could not be downloaded. Price $9.99, royalty about $1.79 at the 4.20 print cost.
- Checks run: `node --test ".claude/skills/kdp-listing/*.test.mjs"` 18 pass, 0 fail; `check-listing.mjs` on the Freddy
  listing PASS, exit 0.

#### Verification

- The Freddy listing passes the skill's own limit checks `[session]`.
- `[owner]` Paste it into the KDP paperback form at https://kdp.amazon.com/en_US/bookshelf; any field that needed
  rewriting is written here.

#### From review

- Applied 1-10 (`reviews/pr-70-high.md`): the checker requires AI disclosure Yes for text, images and translations (Freddy now says
  Translations: Yes); keywords are compared against title, subtitle, contributors (role labels stripped) and category words by plural and
  4-letter stem; the limits and printing-cost bands the skill cites were read from KDP pages on 2026-11-19, while the 8.5 x 8.5 trim, the keyword character limit and the supported description HTML tags are still unconfirmed (listed in the skill); page parity is
  no longer a hard limit (no KDP page confirms it); price must equal `proposePrice` unless "owner override" is written, and the sum text
  must show the royalty; all 15 form headings are required in order; tests added; the test command is the quoted glob only.
- Applied 1, 2, 3, 6 (`reviews/pr-70-medium.md`): AI disclosure matches whole list lines; the sum text needs the royalty as a whole number; an odd page count gets its own "no printing cost" error; Work items ticked.
- Skipped #4 (`reviews/pr-70-medium.md`): "Pages count never checked against Core KdpPagePlan; wrong figure skews royalty." - the listing's page count is copied from the `kdp` command's check-report.md; a Node checker cannot call the C# Core plan.
- Skipped #5 (`reviews/pr-70-medium.md`): "Description rules (no price, no app advertising) unchecked." - description content rules are the writer's judgment in the skill, not a mechanical limit; the skill states them.
- Applied 1-4 (`reviews/pr-70-check.md`): a price over 250 fails even with an owner override; the stemmer strips -es after s/x/z/ch/sh (equal stems match, prefix rule stays 4 letters); the page count is read from "N pages"; the royalty text is matched by value within 0.001. Each has a test.
- Skipped #5 (`reviews/pr-70-check.md`): "Language, Series, Reading age, Publishing rights, Sexually explicit, ISBN sections only need to exist" - those sections hold owner choices with no KDP limit to check.
- Skipped #6 (`reviews/pr-70-check.md`): "Description length counted on trimmed text with CRLF stripped; KDP's line-break counting may differ." - stating the counting basis is documentation; the count already follows KDP's "tags counted" rule. Follow-up.
- Skipped #7 (`reviews/pr-70-check.md`): "Tests run in no CI or script; limits can drift from Core KdpPagePlan constants" - CI wiring of these Node tests is outside this slice's Touches; follow-up for the later KDP skill slice.
- Second check pass (`reviews/pr-70-check2.md`, 6 findings, none blocking): all six are follow-ups after the second check pass, listed below.

#### Follow-ups

- `kdp/listings/freddy-and-the-slipper/en.md:80`: mark the royalty 1.79 as unconfirmed, since the 4.20 print cost is unconfirmed for 8.5 x 8.5 premium colour (`reviews/pr-70-check2.md` #1).
- `.claude/skills/kdp-listing/listing-limits.mjs:150`: royalty always uses the premium-colour large-trim table; trim and ink are never read from the listing (#2).
- `.claude/skills/kdp-listing/listing-limits.mjs:112`: AI disclosure forces Yes for text, images and translations, so a human-written original cannot pass honestly (#3).
- `kdp/listings/freddy-and-the-slipper/en.md:48`: category 3 path is a guess until the real KDP list is visible after login (#4).
- `kdp/listings/freddy-and-the-slipper/en.md:35`: the description says "dark kitchen"; the art may light it by the fridge, unconfirmed (#5).
- `kdp/listings/freddy-and-the-slipper/en.md:45`: keyword 3 repeats the age field, and "tale(s)" sits in keywords 2 and 3 (#6).

#### For CLAUDE.md

- The `kdp-listing` skill writes listings under `kdp/listings/<book>/<lang>.md`; `check-listing.mjs` must pass before a listing is pasted into KDP; every KDP limit cites an Amazon help page with its date. (Slice 31: docs/plan/slices/31-kdp-listing-pack.md)

#### Not yet verified

- `[owner]` Paste the Freddy listing into the KDP paperback form at https://kdp.amazon.com/en_US/bookshelf and write any field that needed rewriting.
- `[owner]` Pick the real KDP categories (the category tree needs the login) and confirm the 4.20 print cost in KDP's calculator.
