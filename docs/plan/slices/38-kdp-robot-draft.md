### Slice 38 — Amazon KDP robot: the book catalog, and one paperback saved as a KDP draft

*Built 2026-11-20, review gate passed (high, medium, six check passes), merged as PR #90 (dea21a4). `draft` stays blocked until the page maps are confirmed from real captures; `login`, `status`, `capture`, `catalog` and `draft --dry-run` work.*

*Planned 2026-11-20 with the owner (interactive session, RefineScope). First of three robot slices (38, 39, 40).*

**Touches:** `kdp/catalog.json` (new), `kdp/state/` (new), `tools/kdp-robot/` (new), `.claude/skills/kdp-publish/` (new),
`.github/workflows/ci.yml`, `CLAUDE.md`, `docs/plan/slices/38-kdp-robot-draft.md`

Claim-time premise check (2026-11-20, decided by the coordinator): `tools/store-assets` is plain ESM JavaScript (`.mjs`,
no TypeScript, no test runner), and the repo's only node tests are `*.test.mjs` run with `node --test`. So the robot is
**plain ESM `.mjs` with `node --test`**, not TypeScript; read "TypeScript" below as that, and `catalog.ts` as
`catalog.mjs`. `tools/kdp-robot/.gitignore` ignores its `node_modules/`. No CI runs node tests today, so `ci.yml` gets
one step running `node --test` over `tools/kdp-robot` (no browser needed: the unit tests never open KDP). The listing
has no territories or matte/glossy field: both come from the catalog. The `kdp` output folder is
`tmp/kdp-out/<dino file name without extension>-<lang>/`.

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
- Owner, 2026-11-20: "all should cost the same" — every book gets one price, today Freddy's USD 9.99 (Slice 31's 60%
  royalty choice). The catalog carries it once as a default, not per book. Cover finish: owner, 2026-11-20, "ok, then glossy it
  is" — every book is glossy (KDP help calls glossy typical for children's books,
  https://kdp.amazon.com/en_US/help/topic/G201834180), carried once as the catalog default.

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

- [x] A. context7 for Playwright (persistent context, `getByRole`/`getByLabel`, file upload, headed mode) before any code;
  cite the answers here. Library id `/microsoft/playwright` (v1.64.1 pinned): `chromium.launchPersistentContext(userDataDir, {headless:false})`
  (one browser per user-data dir), `page.getByLabel`/`getByRole`, `locator.setInputFiles(path)`.
  Built 2026-11-20 (items B-H as code, `.mjs` per the premise check); the page selector labels are from KDP's help pages and are
  unconfirmed on the live site until the `[session]` run below.
- [x] B. `kdp/catalog.json` with all 16 books from `D:\ReadTheStupidText\content\dino\`, and a schema check script
  (`tools/kdp-robot/src/catalog.ts`) with tests: unknown book folder, a language with no `titles.json` entry or no
  listing, duplicate series numbers.
- [x] C. `login` command: opens the kept browser at https://kdp.amazon.com/en_US/bookshelf, waits for the owner to sign
  in, confirms the bookshelf shows.
- [x] D. `status` command: reads the bookshelf and prints each title, format, language, state and KDP id; writes
  `kdp/state/`.
- [x] E. (built, blocked on confirmed page maps: `draft` refuses until they are rewritten from `capture` files) `draft <book> <lang>` command for a paperback: creates the title (or opens the existing one from `kdp/state/`),
  fills *Paperback Details* from the listing (language, title, subtitle, series fields left for Slice 40, authors,
  description, publishing rights, keywords, categories, reading age, AI-content answers), *Paperback Content* (free KDP
  ISBN, premium colour, 8.5 x 8.5 in, bleed, matte or glossy from the catalog, upload `interior.pdf` and `cover.pdf` from
  `tmp/kdp-out/<book>-<lang>/`, wait for KDP's file check and copy its messages), *Pricing* (territories and price from
  the catalog). Save as Draft after each page.
- [x] F. Summary file and the Publish guard with its tests.
- [x] G. Skill `.claude/skills/kdp-publish/SKILL.md`: for one book and language, run `kdp-build` (makes and checks the
  files), run `check-listing.mjs`, run the robot's `draft`, then read the summary to the owner with the direct link to
  that title's bookshelf page. It never publishes.
- [x] H. CLAUDE.md rule: KDP is changed only through `tools/kdp-robot`, which never presses Publish and reads live state
  first.

#### Design after three review passes (coordinator, 2026-11-20, *Two strikes*)

Three passes (`reviews/pr-90-high.md`, `pr-90-medium.md`, `pr-90-check.md`) kept reopening the same four areas: the KDP id,
the free ISBN, categories/trim, and "did the save work". The cause is one thing: every selector and success marker was
written blind, without one real KDP page. More guessing will not converge. So:

- **The KDP id has one owner and is set once.** It is taken only from the bookshelf row that matches title AND language
  AND format exactly once (or from the URL right after Create, read once). It is written to `kdp/state` at that moment and
  never re-pointed: a later URL or row with a different id stops the run (`IdConflict`). Two matching rows stop the run
  (`Ambiguous`); `status` never overwrites a stored id. A title that shows no id after Create stops the run before any
  further save, so a retry finds the draft on the bookshelf instead of making a second one.
- **The free ISBN is recorded only when a 978/979 number is read back from the page** after the assign step, never at the
  click. A confirm dialog is part of the assign step.
- **A save counts only when a new success marker appears after the click** (baseline taken before the click) and the
  fields it saved read back with the new values.
- **Selectors come from real pages.** A new read-only `capture` command (owner signed in) saves the HTML and a small
  screenshot of the bookshelf and of each create/edit page it can reach without saving, to `tmp/kdp-robot/capture/`.
  Categories, trim and content-page selectors are rewritten from those captures in the live session. Until a page map is
  marked `confirmed: true` (set only after it was checked against a capture), `draft` refuses to run on that page. This
  keeps the merged robot safe: `login`, `status` and `capture` work; `draft` waits for the captures.

#### Verification

- Unit tests: catalog checks, the Publish guard (every known Publish label refused), the diff between live and repo.
- Live, by this session `[session]`, with the owner signed in: `draft freddy-and-the-slipper en` makes a draft; a second
  `status` shows it as a draft; the summary lists every field; opening the draft's page shows the same values. Nothing is
  published.
- `[owner]` presses Publish on the Freddy draft when they want it live.

#### Not yet verified

Built: `tools/kdp-robot` (plain ESM `.mjs`, playwright 1.64.1, 73 node tests pass), `kdp/catalog.json` (16 books, one top-level price 9.99 and finish glossy; only Freddy has languages `["en"]`), the `kdp-publish` skill, the `kdp-robot` job in `ci.yml`.

- `[session]` With the owner signed in to KDP: `login`, then `status`, then `capture` saves the bookshelf and the page HTML and screenshots; the page maps are rewritten from them and marked `confirmed: true`.
- `[session]` After the maps are confirmed: `draft freddy-and-the-slipper en` makes a draft, and a second `status` shows it as a draft; the summary lists every field and the draft's page shows the same values.
- `[owner]` Presses Publish on the Freddy draft when they want it live (the robot never does).

#### From review

- Deferred #4 (`reviews/pr-90-check.md`): "`pickCategory` clicks every branch name for every category..." - category selectors are guesses; rewritten from the `capture` files. Until then every page map has `confirmed: false` and `draft` refuses.
- Deferred #5 (`reviews/pr-90-check.md`): "`untickUnlisted` unticks every checked box whose name is not a listed leaf..." - same: rewritten from the capture, `draft` refuses meanwhile.
- Deferred #6 (`reviews/pr-90-check.md`): "`liveTrim` and the trim click use whole-page body text..." - same: trim control selector comes from the capture.
- Deferred #9 (`reviews/pr-90-check.md`): "`writeFinalState` stores hashes for all of `run.diff`, including content fields..." - needs the confirmed content/details page maps to read fields back; done with them.
- To settle against the real captures (`reviews/pr-90-check2.md`, dropped there as out of that pass's scope; not code now): "planDraft skips catalog checks (null price typed as 0.00)" - decide what a null price does once the pricing page is captured.
- To settle against the real captures (`reviews/pr-90-check2.md`): "UPLOADED regexes match 'being processed'" - whether KDP's "is being processed" text counts as an upload success is read from the real file-check page.
- To settle against the real captures (`reviews/pr-90-check2.md`): "createTitle waits for an id on the /new page before any save" - what URL KDP shows right after Create is read from the capture.
- Applied with a note (`reviews/pr-90-check2.md` #1): `draft` edits only a title whose bookshelf row reads exactly as a draft; `readBookshelfRows` still reads bookshelf page 1 only, so a draft on a later page stops the run with NotEditable until reading all pages is built from the capture.
- Applied with a note (`reviews/pr-90-check2.md` #2): `safeClick`/`safeCheck`/`safeUncheck`/`guardField` need an allowed-label list; the category-leaf, cover-choice and field-label matches against the resolved control are guesses until the capture shows what the live controls expose, and a false refusal there stops the run (safe side).
- Applied (`reviews/pr-90-high.md`): findings 1-13 (guarded clicks, save confirmation, file-check wait, KDP id after save, category matching, category/keyword/contributor handling, radio names, state read, page classification, cleanup, contributors, dead config, upload skip). 14 of 14 read.
- Skipped #14 (`reviews/pr-90-high.md`): "The slice file's "Not yet verified" section still says "(none yet)"" - not a code change; the `[session]` lines are written in this file's *Not yet verified* section instead.
- Applied (`reviews/pr-90-medium.md`): findings 1-9 (id pattern, language match in rows, ISBN detection, owner wait without a terminal, ISBN plan only when changed, field guards, category unticking, function size, orphan draft). 9 of 9 read.
- Applied (`reviews/pr-90-check.md`): findings 1, 2, 3, 7, 8, 10; 4, 5, 6 and 9 deferred (listed above). 10 of 10 read.
- Applied (`reviews/pr-90-check2.md`): findings 1 and 2 (with the notes above); the dropped out-of-scope items are listed above. 2 of 2 read.
- Applied (`reviews/pr-90-check3.md`): findings 1-3 (capture opens only a draft row, atomic state write, kdpId pattern check). 3 of 3 read.
- Applied (`reviews/pr-90-check4.md`): findings 1, 2, 3, 5, 6. 6 of 6 read.
- Skipped #4 (`reviews/pr-90-check4.md`): "The `kdpId` pattern check runs only when the parsed JSON has a non-null `kdpId`" - kdp/state is written only by the robot's own validated writeState; a hand-corrupted file is out of scope and the kdpId pattern check already stops a wrong id.
- Applied (`reviews/pr-90-check5.md`): findings 1 and 2, through one guard function. 2 of 2 read.
- Deferred #1 (`reviews/pr-90-check6.md`): "`untickUnlisted` iterates `dialog.locator(TICKED_BOXES).all()`" - categories are an unconfirmed page map and `draft` refuses until it is confirmed; fixed in the capture session. 1 of 1 read.
- Noted (`reviews/pr-90-check3.md` out-of-scope note, guard.mjs:5): "publishing" in the rights radio no longer stops every run; after check5 the guard's one exemption is a static page-map label equal to the control's name. Whether the rights radio's static label matches KDP's real name is checked in the capture session.
- To settle in the capture session: the Follow-ups sections of `reviews/pr-90-check5.md` (rights/listing text checks, `planDraft` shape check order, `id` and language file-name checks, temp-file cleanup and no lock, substring-refusal over-reach on "Publication"/"Submission" labels, `tmp/` git-ignored) and `reviews/pr-90-check6.md` (rights radio value against the page map `options`, `.first()` resolving twice on trim-size locators); bookshelf pages beyond page 1; a retry after a Create that shows no id may make a second blank draft.
