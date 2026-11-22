### Slice 44 — Amazon KDP: the build skills report their steps to the agent board, with process keys

*Planned.* Needs agent-progress-board Slice 55 (process keys and step counts) in the installed agent-board plugin.

**Touches:** `.claude/skills/kdp-build/SKILL.md`, `.claude/skills/kdp-crop/SKILL.md`, `.claude/skills/kdp-listing/SKILL.md`,
`.claude/agents/kdp-crop-check.md`, `.claude/agents/kdp-visual-check.md`

Source: the owner's phone scope "Process optimiser and true per-step telemetry" (agent-progress-board, 2026-11-21),
refined at the desk 2026-11-22; the owner approved every answer that day ("agree with all. plan it"), including that this
repo's book skills report their steps so the optimiser can see them.

## Why

The owner's first example of waste (2026-11-21): "instead of finalising one book and then applying the same rules to the
rest, the skill/agent applied every change to all 16 books, so each change cost 16x." Checked 2026-11-22: `kdp-build`
builds one book and language per run, and `kdp-crop` runs one agent per book, but nothing records how often each book was
rebuilt, and no skill reports to the board. The optimiser's "repeated fan-out" check needs one step per book build.

## Decisions

From the approved answers (2026-11-22):

1. A run over several books opens one card (outcome "KDP files ready for N books"); each book is a step with children.
   Process keys: `book/build`, `book/crop`, `book/visual-check`, `book/listing`, each step labelled with the book's stem
   and language.
2. A rerun of the same book in the same run is a new child step (not an update), so repeated fan-out is countable.
3. `kdp-visual-check` reports `counts: { findings }`; a build reports `counts: { warnings }` from `check-report.md`.
4. "One book first, then the rest" is a preventive gate in the skills, a separate scope (owner, 2026-11-21), not this
   slice. Reports fail open.

## Work

- [ ] **A.** Card and step reporting in `kdp-build` and `kdp-crop` with the keys above.
- [ ] **B.** The two agents report under their steps with counts.
- [ ] **C.** One real multi-book run, read back with `get_item`: one step per book build, reruns visible.

## Verification

- Live: the next multi-book KDP run's card shows a step per book with its process key, and a rerun shows as a second
  child under that book.
