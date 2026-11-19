### Slice 30 — Amazon KDP: `kdp-build` skill and a visual-check agent

*Planned 2026-11-19 from the Scope Inbox doc
[2026-11-18-amazon-kdp-export.md](https://drive.google.com/file/d/16B6KoIGXtHRXaRS8VvQcopP5RyJhcUIM/view?usp=drivesdk)
(milestone 4).*

**Touches:** `.claude/skills/kdp-build/**` (new), `.claude/agents/kdp-visual-check.md` (new),
`src/PrintTheDamnBook.Cli/**` (new, from an earlier KDP slice) (page previews for the agent), `CLAUDE.md` (names the repo's agent)

**Depends on:** Slices 27–29.

#### Why

Owner, 2026-11-18: "I should probably have skills and subagents that would take over the work from me / store and do all
the work until delivering it." Acceptance (doc): an agent produces a proof-ready book with no human step.

#### Decisions

- Approved (doc): skills and agents live in this repo's own `.claude/` folder, so they change with the tool they drive.
- Decided by this planning session, 2026-11-19:
  - `kdp-build` skill: takes a book and a language, runs `print-the-damn-book kdp`, reads the check report, dispatches
    `kdp-visual-check` on the page previews, fixes what it can (a crop offset for a flagged page through `--offset`), and
    reruns until the report is clean or only owner questions are left. It never uploads to KDP.
  - `kdp-visual-check` agent, pinned to Sonnet: looks at a contact sheet of small page previews (never full-size
    pictures, per the global "read pictures small" rule) and reports cut characters, text over the safe margin, wrong
    page order or a text page not facing its picture.
  - The command line gains `--previews` to write the contact sheet and per-page small PNGs.

#### Work

- [ ] `--previews` (contact sheet plus small per-page PNGs).
- [ ] `.claude/agents/kdp-visual-check.md` (model pinned, read-only).
- [ ] `.claude/skills/kdp-build/SKILL.md` with the loop above and its stop rules.
- [ ] Root `CLAUDE.md` names the agent (repo-specific agents rule).

#### Verification

- A fresh Claude Code session runs `kdp-build` on Freddy (English) and a second book, ends with a clean report and the
  files, with no human step `[session]`.
