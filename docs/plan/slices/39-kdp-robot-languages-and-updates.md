### Slice 39 — Amazon KDP robot: every language, and keeping published books up to date

*Planned 2026-11-20 with the owner (interactive session, RefineScope).*

**Touches:** `tools/kdp-robot/`, `kdp/catalog.json`, `kdp/listings/`, `.claude/skills/kdp-listing/`,
`.claude/skills/kdp-publish/`, `docs/plan/slices/39-kdp-robot-languages-and-updates.md`

**Migrations:** none

**Depends on:** Slice 38 (the robot and catalog) and Slice 33 (other-language interiors and covers: its language part is
no longer parked, see below).

#### Why

Every book exists in 15 languages (`en` plus `ar da de es fa fi fr ja nb nl pt ru sv zh`, from
`content/dino/love-from-seagull-island/translations/` in ReadTheStupidText), and the owner will add books and languages
over time. Adding a language or fixing a description must be one command, not a day in the KDP website.

#### Decisions

Approved by the owner, 2026-11-20: **all languages from day one** (RefineScope answer 1, "agree. handles all
languages"). This lifts the "English first, other languages only after the first two paperbacks sell" gate on Slice 33's
paperback-language work. Kindle (Slice 32) keeps its gate.

Decided by this planning session, 2026-11-20:
- **Languages KDP cannot print are skipped and listed, not forced.** Slice 33 records that KDP prints no Arabic or Farsi
  paperbacks; those wait for the Kindle edition (Slice 32's gate). Chinese and Japanese paperback support is checked on
  KDP's supported-languages help page (cite URL and date here) before they are built. The robot reads the language list
  from the catalog and refuses a language KDP's list does not have.
- **Listings in every language** come from `kdp-listing` (title from `titles.json`, description and keywords written
  in that language, `check-listing.mjs` passing). No owner review round: the skill writes, checks and the robot drafts.
- **`sync`**: for every book and language in the catalog, read the live KDP page, compare it with the repo, and change
  only the fields that differ. A live book's edits stay as KDP's unpublished changes; the owner presses Publish. A field
  edited on KDP by hand that the repo doesn't know is shown in the summary and never overwritten without the owner's
  word in that session.
- **`sync --dry-run`** prints the differences and touches nothing.
- A new book or language is added by a catalog line plus its listing; `sync` creates the draft.

#### Work

- [ ] A. KDP supported-languages check for paperbacks (help page, dated), written into the catalog checker.
- [ ] B. `kdp-listing` writes and checks a listing for each supported language of each book.
- [ ] C. `sync` and `sync --dry-run` in the robot, field by field against `kdp/state/`; tests for "only changed fields",
  "hand edit on KDP is reported, not overwritten".
- [ ] D. `kdp-publish` skill gains "all languages of a book" and "everything in the catalog" runs, one title at a time,
  with one combined summary and a direct link per title.

#### Verification

- Unit tests for the diff and the unsupported-language refusal.
- Live `[session]`, owner signed in: `sync --dry-run` lists every book and language with its difference; `sync` on one
  second language of Freddy makes a draft; a second `sync --dry-run` shows no difference for it.

#### Not yet verified

_(none yet)_
