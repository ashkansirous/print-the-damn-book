# Plan: print-the-damn-book

A Windows desktop app that turns a folder of page images (`Cover`, `1`..`N`, `Back cover`)
into a saddle-stitch booklet and sends it to a duplex printer in the right order, two pages
per landscape sheet, so the printed stack only needs folding in the middle.

The one rule: **the app, not the user, decides which page goes where.** All imposition maths
lives in the Core library and is unit-tested; the UI only shows and prints what Core computed.

Decisions locked on 2026-10-22 (RefineScope): C# on the latest GA .NET (10, LTS; .NET 11 is
RC2 until its November 2026 GA, when `global.json` rolls forward) with WPF, single
self-contained exe; A4 landscape default with Letter selectable; fit each page in its half of
the sheet, aspect kept; single folded stack with a warning above 6 sheets; JPG and PNG input
only; folder load with natural sort and drag reorder, no project file; blanks inserted before
the back cover; direct printing with app-set duplex (short edge) and a test-sheet button;
optional separate job for the cover sheet; sheet preview before printing; GitHub Actions
builds the exe on release, no Dockerfile.

## Status

Every slice except 12, 13, 22, 24, 26, 32, 33 and 35 is built (merge dates in [slices-done.md](docs/plan/notes/slices-done.md)); 22 and 26 are planned again and 35 is new (feedback loop of night 2026-11-19-2055); 12, 13, 24, 32 and 33 are parked (the table below). 36 merged 2026-11-20 (PR #85), with 34's work. 30 merged 2026-11-20 (PR #77). 29 merged 2026-11-19 (PR #76). 28 merged 2026-11-19 (PR #72). 31 merged 2026-11-19 (PR #70). 27 merged 2026-11-19 (PR #67). 25 merged 2026-11-18 (PR #58). The Microsoft Store submission was dropped by the user on 2026-10-22: the MSIX tooling from Slice 11 stays in place but dormant (it skips without the `MSIX_*` variables). 12 and 13 are parked. 21 merged 2026-11-01 (PR #44). 20 merged 2026-11-01 (PR #45). 23 merged 2026-11-18 (PR #59).

The repo is public (2026-10-22), so the site and the app download from public GitHub releases. The landing site is live at https://print-the-damn-book.sirous.uk (deployed 2026-10-22 via local wrangler, all pages 200).

| Slice | Title | Status | PR | Issue | File |
| --- | --- | --- | --- | --- | --- |
| 12 | Android app, .NET MAUI (parked) | parked | | | [12](docs/plan/slices/12-android-app.md) |
| 13 | iOS app, .NET MAUI (parked) | parked | | | [13](docs/plan/slices/13-ios-app.md) |
| 22 | Dino follow-ups from Slice 20's reviews and tests | planned | | | [22](docs/plan/slices/22-dino-review-followups.md) |
| 24 | Paid Dino books must be bought before they print ($0.99 print purchase, Store errors, test-build bypass) | parked: skipped by the user 2026-11-18; comes back only when the user asks in an interactive session | | | [24](docs/plan/slices/24-buy-to-print.md) |
| 26 | Follow-ups from Slices 23 and 25 reviews | planned | | | [26](docs/plan/slices/26-s23-s25-review-followups.md) |
| 32 | Amazon KDP: Kindle edition | parked: starts only when the owner says in an interactive session that the first two paperbacks sold enough (doc's kill rule) | | | [32](docs/plan/slices/32-kdp-kindle-edition.md) |
| 33 | Amazon KDP: other languages, right-to-left, Farsi/Arabic Kindle | planned: paperback languages unparked by the owner 2026-11-20 (all languages from day one, Slice 39); Farsi/Arabic Kindle keeps Slice 32's gate | | | [33](docs/plan/slices/33-kdp-other-languages.md) |
| 35 | Amazon KDP follow-ups from Slices 27-31 reviews and the 2026-11-20 live check | planned | | | [35](docs/plan/slices/35-kdp-followups.md) |
| 39 | Amazon KDP robot: every language, and keeping published books up to date | planned | | | [39](docs/plan/slices/39-kdp-robot-languages-and-updates.md) |
| 40 | Amazon KDP: series, "Also by" page, author page, short link to the Dino app | planned | | | [40](docs/plan/slices/40-kdp-series-and-cross-links.md) |
| 41 | Amazon KDP: "More books" on the last page, and the crop agent places the back-cover words and the Dino QR | verified: built (PR #93), 16 books rebuilt, owner accepted 2026-11-20 | 93 | | [41](docs/plan/slices/41-kdp-more-books-and-back-placement.md) |
| 42 | Amazon KDP: one text size for every story page of a book | built (PR #100) | 100 | | [42](docs/plan/slices/42-kdp-one-text-size-per-book.md) |
| 44 | Amazon KDP: the build skills report their steps to the agent board, with process keys | planned (owner 2026-11-22; waits for agent-progress-board Slice 55) | | | [44](docs/plan/slices/44-kdp-board-steps.md) |

Notes:

- [slices-done.md](docs/plan/notes/slices-done.md): finished slice rows (every built slice).
- [2026-11-01-1141-feedback-loop-w1.md](docs/plan/notes/2026-11-01-1141-feedback-loop-w1.md): feedback loop after night round 1 (Slices 17-19).
- [2026-11-01-1141-feedback-loop-final.md](docs/plan/notes/2026-11-01-1141-feedback-loop-final.md): final feedback loop of that night (worktree locks traced to check timers and same-call removals).
- [2026-11-01-2024-feedback-loop-w1.md](docs/plan/notes/2026-11-01-2024-feedback-loop-w1.md): feedback loop after night 2026-11-01-2024 round 1 (Slice 20 merged, Slice 21 parked on the shared-VM permission).
- [2026-11-18-1302-feedback-loop.md](docs/plan/notes/2026-11-18-1302-feedback-loop.md): feedback loop of night 2026-11-18-1302 (Slices 23 and 25 merged; their review follow-ups parked as Slice 26).
- [2026-11-19-2055-feedback-loop.md](docs/plan/notes/2026-11-19-2055-feedback-loop.md): feedback loop of night 2026-11-19-2055 (KDP Slices 27-31 merged; their follow-ups planned as Slice 35; Slices 22 and 26 planned again).
- [2026-11-01-2024-feedback-loop-final.md](docs/plan/notes/2026-11-01-2024-feedback-loop-final.md): final feedback loop of that night (four repeated mistakes, a device-test budget guard, and the lost `.dino` Open-with entry).

## Outstanding work

- **Not yet tried on a device:** 30 checks across 16 slices, in [docs/plan/unverified.md](docs/plan/unverified.md).
- Text size answered: every story page of every book is 24 pt (Slice 42, built PR #100; [slice 42](docs/plan/slices/42-kdp-one-text-size-per-book.md)).
- `tools/kdp-robot` should refuse a folder whose `check-report.md` has an `## Errors` section: `planDraft` (`src/draft.mjs:45-52`) checks only that `interior.pdf` and `cover.pdf` exist ([slice 42](docs/plan/slices/42-kdp-one-text-size-per-book.md), Follow-ups).
- Slice 38 (KDP robot) is built but `draft` refuses to run until its page maps are confirmed from a capture; next step needs the owner signed in to KDP for the capture session: [KDP bookshelf](https://kdp.amazon.com/en_US/bookshelf) ([slice 38](docs/plan/slices/38-kdp-robot-draft.md)).
- KDP robot open points (owner can change; [slice 40](docs/plan/slices/40-kdp-series-and-cross-links.md)): the other 14 books stay in series "Dino Stories"; Love from Seagull Island is book 1 of "Little Seagull’s Big Adventures" and X Marks the Treasure book 2. Arabic and Farsi paperbacks don't exist on KDP, so those wait for the Kindle edition (Slice 32's gate).
- Amazon KDP spin-offs that belong to other repos (Scope Inbox doc of 2026-11-18, *Spin-offs*), not planned there yet: print-resolution art for Three Little Pigs, Town Mouse, Ugly Duckling and X Marks the Treasure, and author/credit fields per book (ReadTheStupidText / dino-reader); audiobook collections (dino-reader). Later here: hardcover treasury and 3-in-1 collection, KDP upload is now planned as the Playwright robot, Slices 38-40.
- Slice 26, planned again by the feedback loop of night 2026-11-19-2055 (parked by the loop of 2026-11-18-1302): the unfixed last-pass review findings of Slices 23 and 25 (an Own page choice lost on retype, a language select left on the wrong language after a cancelled switch, a possible `book.json` write on open), the unexplained blank-line collapse, and the `$GuestChecks` list gap. The next night builds it ([slice 26](docs/plan/slices/26-s23-s25-review-followups.md)).
- Seven ClaudeSkills tooling gaps from the feedback loop of night 2026-11-18-1302 are open (user files copied into public test data, check-pass rule vs global fix rule, timer-kill clash, "read in full" claims, unchecked branch-sweep, inline Python writes, host-window approval guard): see the note's *Open findings* ([note](docs/plan/notes/2026-11-18-1302-feedback-loop.md)).
- Agent scratch files are not ignored on `main`: no `tmp/` and no `tmp_*` line in `.gitignore` (feedback loops of night 2026-11-01-2024, see their notes). `main` has `/tmp/` only (from PR #44); `tmp_*` is still missing. Verify: `git check-ignore tmp/x tmp_fix.py` prints both.
- `.dino` "Open with" on this PC is empty (read 22:26 on 2026-11-01: `HKCU\Software\Classes\.dino\OpenWithProgids` has no values, and the `PrintTheDamnBook.dino` key is absent). A test agent deleted the three HKCU keys during the checks of 2026-11-01 (PR #46) without saving them first. The app re-registers only when `PrintTheDamnBook.exe` runs from outside a build output or packaged install (`Services/OpenWithRegistration.cs:33-35`, `Core/Dino/OpenWithRegistrar.cs:34`). No release exists yet (`v0.1.0` not cut), so a dev run doesn't bring it back. Dino Reader is installed as an MSIX package (`AshkanSirous.DinoReader`, read 22:50). An audit pass found no `.dino` association in its repo's manifest on `origin/main`, and the installed package's manifest couldn't be read (empty install location). So nothing of Dino Reader's is known to be lost. Slice 21's runs must restore what they change (`agents/device-test.md`, reg export).
- Slice 20, waiting on the user: words typed into a book opened in an `.alt-<guid>` folder (another window held the cache) are saved only there and go when the lone-instance sweep runs (Slice 19 behaviour on main, kept); decide whether to warn or merge them back ([slice 20](docs/plan/slices/20-dino-open-hardening.md)).
- Slice 22, planned again by the feedback loop of night 2026-11-19-2055 (parked by an earlier loop): Slice 20's App tests likely use the real `%LOCALAPPDATA%` cache, failures are still classified by message text in one place, ten low review notes, and the store dialog's failed states were written without a design. The next night builds it; the failed states follow the existing Nocturne dialog styles unless a mock arrives first ([slice 22](docs/plan/slices/22-dino-review-followups.md)).
- Slice 19: sweeping older versions of a book also deletes that version's `book.json` (Slice 15 behaviour, found in check 2); Dino Reader follow-up: add a "Print this book" slice to its plan (`D:\ReadTheStupidText`) now that Slice 19 has merged ([slice 19](docs/plan/slices/19-open-from-dino-reader.md)).
- Slice 15: memory not returned after a very large book (about 1.6 GB held after a 420-page stress book is replaced, also on main for folder opens; needs a profiler); about 3 in 18 cancelled opens leave a cache folder behind (cleaned on reopen); drop-zone drag checked with a real Explorer drag only once ([slice 15](docs/plan/slices/15-open-dino-books.md)).
- Slice 16: pre-print status and Cancel on a large folder (wait over 500 ms); suggestion quality on real noisy photos, thresholds tuned only on synthetic images ([slice 16](docs/plan/slices/16-text-band-suggestion.md)).
- Slice 14: a real paper print and fold; performance with many story pages (re-layout on the UI thread on each typing pause) ([slice 14](docs/plan/slices/14-typed-text-pages.md)).
- Letter paper verified 2026-10-27 for Slices 14-16 (Print to PDF: page size, bands within 30%, own page, shrink, Dino cover title). Seen then: three-little-pigs.dino warns "5 pages have more words than fit" on Letter (Dino pages default to the bottom band, so long text is cut at 14 pt; consider Own page or a smaller minimum for Dino books); "Print test sheet" on a Dino book printed page 1, not the cover (not investigated).
- Slice 9: a real printed page with numbers (print to PDF or paper) ([slice 09](docs/plan/slices/09-page-numbers.md)).
- Slice 7 code follow-ups from Slice 11 review: `SnapLayoutHook` hover state not cleared without non-client mouse tracking; `NocturneDialog` CoverOwner sizing / null owner; `ClearStatusUnlessBusy` doesn't reset `PrintStatusKind`; `TestSheetSent` mid-send check only covers RotateBacks ([slice 11](docs/plan/slices/11-microsoft-store.md)).
- First release tag `v0.1.0` is not yet cut; it unblocks Slice 5's `release.yml` and the site download link ([slice 05](docs/plan/slices/05-release-pipeline.md)).
- Slice 7: focus ring on the A4/Letter radios and switches, done-step footer wrap, the stuck drag tooltip (not reproduced), snap layout on a high-DPI monitor, all seen on screen ([slice 07](docs/plan/slices/07-wizard-redesign.md)).
- Slice 8: the icon by eye in Explorer, title bar and taskbar, light and dark. The screenshot re-shoot from the real app is carried by Slice 11 ([slice 08](docs/plan/slices/08-icon-and-store-assets.md)).
- Slice 10: the site is deployed from this PC with the logged-in wrangler (`cd site && npx --yes wrangler@4 deploy`), not by CI: no `CLOUDFLARE_API_TOKEN` was ever created, so the Site workflow's deploy step skips. Redeploy that way after any `site/` change. Slice 18's privacy text went live this way on 2026-11-01 (version 632c6b15). First deploy and custom domain are live (done 2026-10-22). Still open: Lighthouse run, download link once the first release exists ([slice 10](docs/plan/slices/10-landing-site.md)).
- Slice 2: a real duplex print and fold; an EXIF-rotated JPEG checked by eye; busy-disable of the buttons seen in the UI ([slice 02](docs/plan/slices/02-print-booklet.md)).
- Slice 2: the user's Brother HL-L3220CW has no duplex; resolved by Slice 6 (manual two-sided), see the next item ([slice 06](docs/plan/slices/06-manual-duplex.md)).
- Slice 6: test sheet and a full booklet on the user's Brother HL-L3220CW, then fold (needs paper; ask the user) ([slice 06](docs/plan/slices/06-manual-duplex.md)).
- Slice 3: Remove buttons are absent from the UI Automation tree (needs `AutomationProperties`); decide whether Cover and Back cover should be removable ([slice 03](docs/plan/slices/03-reorder-pages.md)).
- Slice 5: `release.yml` has never run on GitHub (needs a first tag, e.g. `v0.1.0`); app icon is missing ([slice 05](docs/plan/slices/05-release-pipeline.md)).
- Slice 4: long-edge flip test sheet on a real duplex printer (none available); an explicit Manual choice saved on a duplex-capable printer ([slice 04](docs/plan/slices/04-options-and-settings.md)).
- Slice 1: visual check by eye of thumbnail aspect ratio and scrolling on the sample folder ([slice 01](docs/plan/slices/01-folder-to-sheet-preview.md)).

## Out of Scope

- PDF or WebP input (images only; PDF is the first thing to add if a generator produces it).
- Multiple signatures or perfect binding: one folded stack per book.
- Saving a book project file: reopen the folder instead. (Exception from Slice 14: story text is saved in `book.json` in the folder; page order is not.)
- Trim marks, bleed, crop marks: pages are fit inside the half-sheet, nothing is cut. (Exception approved 2026-11-18: the Amazon KDP exporter crops and adds bleed for KDP output only, Slices 27–28.)
- Printing through another app's dialog, or a Dockerfile / GHCR image (desktop app, no backend).
- macOS or Linux builds. (Android and iOS are parked slices 12–13, not out of scope. Exception approved 2026-11-18: the Amazon KDP command-line tool builds and runs on Linux too, Slice 27; the WPF app stays Windows-only.)
- A light theme for the redesign (Nocturne is dark only for now).
- Translating or mirroring the app's own screens (Slice 17 changes only the book). Hebrew and other non-Arabic-script right-to-left languages.
- The Dino Reader side of the hand-over (its "Print this book" action); Slice 19 builds only this app's side.
