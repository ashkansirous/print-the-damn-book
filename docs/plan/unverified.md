# Not yet verified

Every open "Not yet verified" check, one block per slice, newest first. Remove a line when it passes on a device or the user confirms it.

### Slice 21: Scripted desktop checks ([slice](slices/21-desktop-test-harness.md))

- `store-dialog` (list, download, Cancel) has not run: the VM has no network; it needs the host run with `-HostStore -AcknowledgeHostDesktop`.
- `open-dino` and `handover-check` ran with the `-Dino` file given at the run; no sample .dino is in the repo.

### Slice 20: Dino open and store hardening ([slice](slices/20-dino-open-hardening.md))

- The failed state with Back to list on a real download failure (cannot be forced without changing live state).
- The hand-over alert for an `IOException` hand-over (unit-tested only).
- The "in use by another window" message when two windows download the same book (unit-tested only).

### Slice 18: Get a story from Dino's store ([slice](slices/18-dino-store.md))

- The language filter with more than one language, and Farsi/Arabic store cards (the live store has only English books; with English only, the filter and all 16 cover thumbnails passed on 2026-11-01).

### Slice 17: Right-to-left books (Farsi and Arabic) ([slice](slices/17-right-to-left-books.md))

- Paper print and fold of one Farsi book (user).
- The font the Farsi text is drawn in (Vazirmatn): the 2026-11-01 run saw Arabic-script glyphs but did not read the font name from the UI tree.
