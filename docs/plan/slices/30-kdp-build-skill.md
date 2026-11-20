### Slice 30 — Amazon KDP: `kdp-build` skill and a visual-check agent

*Planned 2026-11-19 from the Scope Inbox doc
[2026-11-18-amazon-kdp-export.md](https://drive.google.com/file/d/16B6KoIGXtHRXaRS8VvQcopP5RyJhcUIM/view?usp=drivesdk)
(milestone 4).*

*Built 2026-11-20, merged as PR #77 (aa14be7). Tests, build and hands-on `--previews` runs pass; the `kdp-build` loop itself has not run in a fresh session yet.*

**Touches:** `.claude/skills/kdp-build/**` (new), `.claude/agents/kdp-visual-check.md` (new),
`src/PrintTheDamnBook.Cli/**` (new, from an earlier KDP slice) (page previews for the agent), `CLAUDE.md` (names the repo's agent),
`src/PrintTheDamnBook.Kdp/KdpExporter.cs`, `src/PrintTheDamnBook.Kdp/KdpCheckReport.cs`, `src/PrintTheDamnBook.Kdp/Pictures/**`,
`src/PrintTheDamnBook.Kdp/Previews/**` (new), `src/PrintTheDamnBook.Kdp/KdpInteriorDocument.cs` (previews share the interior's drawing), `.gitignore` (the skill's output folder), `src/PrintTheDamnBook.Core/Kdp/**` (preview sizing and contact-sheet grid maths), `tests/PrintTheDamnBook.Kdp.Tests/**`, `tests/PrintTheDamnBook.Core.Tests/Kdp/**`

*Night run, wave 2 (2026-11-19): Touches widened from the premise check (Slice 28 writes previews only for flagged pages, from
`KdpExporter` and `Pictures/CutLineInputs.cs`; there is no contact sheet yet). `.claude/settings.json` is not touched: the skill
runs the `kdp` command through `dotnet run` like any repo command, and permission lines are the owner's to add.*

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

- [x] `--previews` (contact sheet plus small per-page PNGs).
- [x] `.claude/agents/kdp-visual-check.md` (model pinned, read-only).
- [x] `.claude/skills/kdp-build/SKILL.md` with the loop above and its stop rules.
- [x] Root `CLAUDE.md` names the agent (repo-specific agents rule).

*Changed while building:*

- `--previews` writes `contact-sheet.png` (spread order, labelled, 1600 px) and `previews/interior/page-NN.png` (about 414 px, 48 dpi)
  from the interior's own drawing (`KdpInterior.DrawingFor`); grid maths in Core `KdpContactSheetLayout`, drawing in
  `Kdp/Previews/KdpPreviews.cs`.
- A preview failure never stops the interior, cover, report or `kdp.json` (saved before previews); the run exits 1. A run without
  `--previews` removes old previews.
- The skill writes to `<repo root>/tmp/kdp-out/<book>-<lang>/` (git-ignored), never outside the repo; 4-run cap, stop rules, never uploads.
- `kdp-visual-check` (Sonnet; Read, Glob, Grep) uses the contact sheet for order, facing and cuts, and the page PNGs for margins and cut words.
- Touches listed `KdpCheckReport.cs` and `Pictures/**`; both ended unchanged.
- Hands-on: `--previews` on gingerbread (34 pages) and lion (36 pages), exit 0, contact sheet legible; output in
  `D:/print-the-damn-book-worktrees/reviews/30-out/`. Tests 778 green (Core 631, App 77, Kdp 70); build, format and line endings exit 0.

#### Verification

- A fresh Claude Code session runs `kdp-build` on two free books, ends with a clean report and the files, with no human
  step `[session]`.
- The same run on Freddy (English), which needs the owner's paid copy `[owner]`.

#### From review

Findings 1-5 and 7 of `reviews/pr-77-high.md` were applied; the skips are listed here, one per line.

- Skipped #6 (`reviews/pr-77-high.md`): "All four Work items are still `[ ]` although code, agent, skill and CLAUDE.md line exist. Touches lists `.gitignore`, `KdpCheckReport.cs` and `Pictures/**`, none modified." - slice-file bookkeeping that update-docs does when it records the slice; extra Touches entries only widen the claim.
- Skipped #1 (`reviews/pr-77-medium.md`): "Doc comment says no preview files remain after a failure, but KdpPreviews.Write is not atomic." - the previews are a side output rebuilt on every `--previews` run and a failure is reported, so a partial set is never used silently.
- Skipped #2 (`reviews/pr-77-medium.md`): "A preview-only failure exits 1, the same code as". - a preview failure must stop the loop because the visual check needs the previews; the skill's stop rule is the slice's Decision.
- Skipped #3 (`reviews/pr-77-medium.md`): "ReplacePages deletes old page PNGs before writing new ones, and the contact sheet uses the default SKFont typeface." - the skill runs on the owner's Windows machine, where the default font exists, and previews are rebuilt each run; blank labels on Linux go to the follow-up list.

#### For CLAUDE.md (lifted 2026-11-20)

- `kdp-build` skill and `kdp-visual-check` agent live in `.claude/`. Previews come only from the interior's own drawing (`KdpInterior.DrawingFor`), and a preview failure never stops the PDFs, report or `kdp.json`. (Slice 30: docs/plan/slices/30-kdp-build-skill.md)

#### Not yet verified

- `[session]` A fresh Claude Code session runs `kdp-build` on two free books and ends with a clean report and the files.
- `[owner]` The same run on Freddy (English), which needs the owner's paid copy.

*Follow-up (not built):* contact-sheet labels may be blank on Linux without a default font (medium #3).
