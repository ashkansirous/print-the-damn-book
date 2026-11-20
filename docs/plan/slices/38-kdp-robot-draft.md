### Slice 38 — Amazon KDP robot: the book catalog, and one paperback saved as a KDP draft

*Planned 2026-11-20 with the owner (interactive session, RefineScope). First of three robot slices (38, 39, 40).*

**Touches:** `kdp/catalog.json` (new), `kdp/state/` (new), `tools/kdp-robot/` (new), `.claude/skills/kdp-publish/` (new),
`CLAUDE.md`, `docs/plan/slices/38-kdp-robot-draft.md`

**Migrations:** none

**Depends on:** Slices 27–31 and 36 (built: the `kdp` command makes `interior.pdf` and `cover.pdf`; `kdp-listing` writes
and checks the listing text). Run after Slice 35 item H if it changes the listing limits.

#### Why

Amazon has no API for creating or editing KDP books (Slice 27 found none; a web search on 2026-11-20 found none either,
only KDP help pages describing the manual + Create flow, https://kdp.amazon.com/en_US/help/topic/G202172740). The owner
has 16 books in 15 languages and will add more, so typing every title into the KDP website by hand does not scale. A
Playwright robot fills the KDP website from what the repo already holds.

Inspiration, taken in part only (owner, 2026-11-20: "take whatever would help me ... dont take them as they are"):
- *KDP Book Publishing Workflow* (https://mcpmarket.com/tools/skills/kdp-book-publishing-workflow, repo
  https://github.com/queelius/claude-anvil): Playwright drives the KDP dashboard and the owner helps with login. Taken:
  Playwright on the real site, owner-assisted login. Not taken: AI cover art, OpenAI key, a free-form agent clicking
  around.
- *amazon-kdp-skill*: no public repo by that name was found (three searches, 2026-11-20). The nearest, "KDP Publisher"
  on clawhub, fills fields by element id with JavaScript and is flagged by its own security scan for uploading or pricing
  without confirmation. Taken as a warning: every change is shown before it counts, and the robot never publishes.

#### Decisions

Approved by the owner, 2026-11-20 (RefineScope answers):
- **The robot handles every language from day one**; the owner picks which book and language each run (answer 1,
  "agree. handles all languages"). Languages beyond English are Slice 39.
- **The robot stops at a draft** (answer 2, "agree with draft. I'll publish myself"): the owner logs in once in a browser
  window the robot keeps and types any two-step code; the robot fills the details, uploads `interior.pdf` and `cover.pdf`,
  sets the price, saves as draft and shows a summary of what changed. The owner presses Publish.
- Not objected to (defaults given with the answers): paperback first, Kindle stays with Slice 32's gate; no web links in
  book descriptions.

Decided by this planning session, 2026-11-20:
- **One catalog file** `kdp/catalog.json` holds only what no other file holds: per book its id (the
  `D:\ReadTheStupidText\content\dino\<id>` folder name), the `.dino` it builds from, the languages to publish, its series
  and number (Slice 40), contributors, and price. Titles come from the book's own `titles.json`, listing text from
  `kdp/listings/<book>/<lang>.md`; never copied into the catalog.
- **What KDP holds is recorded** in `kdp/state/<book>/<lang>.json` after each run: the KDP title id, the state the
  robot last read (draft, in review, live), and a hash of each field it set. The robot always reads the live KDP page
  first and compares it with the repo before changing anything.
- **The robot is TypeScript + Playwright under `tools/kdp-robot/`**, the same Node toolchain as `tools/store-assets/`.
  Its browser profile lives in `%LOCALAPPDATA%\PrintTheDamnBook\kdp-browser\` (outside the repo, never committed, never
  copied). Headed browser, human pace, one title at a time.
- **It never presses Publish.** A guard refuses any click on a control whose name matches publish/submit/"Publish your
  paperback" in any KDP UI language, with a unit test; the robot uses only "Save as Draft" and "Save and Continue".
- **Fields are found by their visible label and role** (`getByRole`, `getByLabel`), from one selector map per KDP page
  (`tools/kdp-robot/src/pages/`), so a KDP change is fixed in one file. A field it cannot find stops the run with a small
  screenshot and the field's name; it never guesses or types into another field.
- **Login, captcha or an unexpected page stops the run** and tells the owner what to do in the open browser window; it
  resumes when the owner says so. No password is stored anywhere.
- Each run writes `tmp/kdp-robot/<book>-<lang>/summary.md` (git-ignored): every field, its old and new value, the files
  uploaded, and KDP's own messages (for example the cover-check result), plus small screenshots.

#### Work

- [ ] A. context7 for Playwright (persistent context, `getByRole`/`getByLabel`, file upload, headed mode) before any code;
  cite the answers here.
- [ ] B. `kdp/catalog.json` with all 16 books from `D:\ReadTheStupidText\content\dino\`, and a schema check script
  (`tools/kdp-robot/src/catalog.ts`) with tests: unknown book folder, a language with no `titles.json` entry or no
  listing, duplicate series numbers.
- [ ] C. `login` command: opens the kept browser at https://kdp.amazon.com/en_US/bookshelf, waits for the owner to sign
  in, confirms the bookshelf shows.
- [ ] D. `status` command: reads the bookshelf and prints each title, format, language, state and KDP id; writes
  `kdp/state/`.
- [ ] E. `draft <book> <lang>` command for a paperback: creates the title (or opens the existing one from `kdp/state/`),
  fills *Paperback Details* from the listing (language, title, subtitle, series fields left for Slice 40, authors,
  description, publishing rights, keywords, categories, reading age, AI-content answers), *Paperback Content* (free KDP
  ISBN, premium colour, 8.5 x 8.5 in, bleed, matte or glossy from the catalog, upload `interior.pdf` and `cover.pdf` from
  `tmp/kdp-out/<book>-<lang>/`, wait for KDP's file check and copy its messages), *Pricing* (territories and price from
  the catalog). Save as Draft after each page.
- [ ] F. Summary file and the Publish guard with its tests.
- [ ] G. Skill `.claude/skills/kdp-publish/SKILL.md`: for one book and language, run `kdp-build` (makes and checks the
  files), run `check-listing.mjs`, run the robot's `draft`, then read the summary to the owner with the direct link to
  that title's bookshelf page. It never publishes.
- [ ] H. CLAUDE.md rule: KDP is changed only through `tools/kdp-robot`, which never presses Publish and reads live state
  first.

#### Verification

- Unit tests: catalog checks, the Publish guard (every known Publish label refused), the diff between live and repo.
- Live, by this session `[session]`, with the owner signed in: `draft freddy-and-the-slipper en` makes a draft; a second
  `status` shows it as a draft; the summary lists every field; opening the draft's page shows the same values. Nothing is
  published.
- `[owner]` presses Publish on the Freddy draft when they want it live.

#### Not yet verified

_(none yet)_
