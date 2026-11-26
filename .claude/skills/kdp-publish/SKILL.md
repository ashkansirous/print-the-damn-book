---
name: kdp-publish
description: Save one Dino book and language as an Amazon KDP paperback DRAFT with the repo's Playwright robot (tools/kdp-robot): build and check the files, check the listing, run the robot's draft command, then read its summary to the owner with the link to the title's bookshelf page. Never publishes. Use when the user says "put <book> on KDP", "make the KDP draft for <book>", "upload <book> to KDP", or a slice asks for a KDP draft.
---

# kdp-publish

KDP is changed only through `tools/kdp-robot`. It never presses Publish (a guard refuses it) and reads KDP's live page before it changes
anything. The owner presses Publish themselves. (Slice 38: docs/plan/slices/38-kdp-robot-draft.md)

Needs the book in `kdp/catalog.json` with the language listed (`node tools/kdp-robot/src/cli.mjs catalog` must print PASS), and one
`npm ci` in `tools/kdp-robot` plus `npx playwright install chromium` the first time.

## Steps (one book `<book>`, one language `<lang>`; run from the repo or worktree root)

1. **Build and check the files:** run the `kdp-build` skill for the catalog's `.dino` (`kdp/catalog.json`, field `dino`, in `D:\high-res-books\`)
   and `<lang>`. It leaves `tmp/kdp-out/<dino name without extension>-<lang>/interior.pdf` and `cover.pdf`. Stop if its report has owner questions.
2. **Check the listing:** `node .claude/skills/kdp-listing/check-listing.mjs kdp/listings/<book>/<lang>.md` must print PASS.
3. **Dry run:** `node tools/kdp-robot/src/cli.mjs draft <book> <lang> --dry-run` shows every field it would set and any missing file (no browser).
4. **Sign in once:** `node tools/kdp-robot/src/cli.mjs login`. The owner types the password and any two-step code in the browser window; no password is stored.
5. **Draft:** `node tools/kdp-robot/src/cli.mjs draft <book> <lang>`. A sign-in, captcha or unknown page stops it and prints what the owner does in the
   open window; the robot watches the page and carries on by itself once the owner has finished there (no key to press). A field it cannot find stops the run with the field's name; fix the selector in
   `tools/kdp-robot/src/pages/`, never by guessing.
   **Bounded runs (Slice 43 I, owner 2026-11-21): one fix, one rerun, about 50k tokens per book.** A stopped run saves what it did and prints one
   `STOP:` line (also the first line of the summary). Make at most one fix (the page maps in `src/pages/recorded/` are generated from the owner's
   recording with `gen-maps`, never hand-edited) and one rerun; the robot refuses a third run until the owner passes `--reset-stops`. If the rerun
   stops too, or the book has used about 50k tokens, stop and hand that one `STOP:` line to the owner. Never run `capture` again to hunt for controls;
   a missing control means a new recording: `node tools/kdp-robot/src/cli.mjs record <book> <lang>` (the owner fills the title in by hand).
6. **Read the summary** `tmp/kdp-robot/<book>-<lang>/summary.md` to the owner (fields old and new, files uploaded, KDP's own messages, e.g. the cover check),
   with the direct link it prints: `https://kdp.amazon.com/en_US/title-setup/paperback/<KDP id>/details`. Run `status` afterwards to confirm the title shows as a draft.
7. Tell the owner: nothing is published; they press Publish on that page when they want it live.

Never click Publish, Submit or "Publish your paperback", even if the owner seems to ask in passing: say the robot does not do it.
