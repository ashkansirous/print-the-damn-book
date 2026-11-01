### Slice 19 — Open a book handed over by Dino Reader

*Built 2026-11-01, merged as PR #35 (0a364a1).*

**Touches:** `src/PrintTheDamnBook.App/App.xaml`, `src/PrintTheDamnBook.App/App.xaml.cs`,
`src/PrintTheDamnBook.App/Services/LaunchArguments.cs` (new), `src/PrintTheDamnBook.App/Services/OpenWithRegistration.cs`
(new), `src/PrintTheDamnBook.App/ViewModels/MainViewModel.Dino.cs`, `src/PrintTheDamnBook.App/MainWindow.xaml.cs`,
`src/PrintTheDamnBook.Core/Dino/**` (argument parsing, if kept in Core), `tests/PrintTheDamnBook.Core.Tests/**`,
`README.md`, `src/PrintTheDamnBook.App/ViewModels/MainViewModel.cs`, `src/PrintTheDamnBook.App/Views/OpenStepView.xaml*`
(added at the claim, wave 2026-11-01)

**Wave note (2026-11-01).** Open points take the defaults written below. Slices 17 and 18 also edit
`MainViewModel.Dino.cs`: keep this slice's edits to `LoadDinoAsync`/`ReadDino` and new members. Merge order 17, 19, 18.

**Why.** The user wants to open a book from Dino Reader and print it (2026-11-01). **This slice is only this app's
side**: it gives Dino Reader a way in. The Dino Reader button ("Print this book") is a follow-up in Dino Reader's own
plan (`D:\ReadTheStupidText`), not built here.

**What exists (grounded 2026-11-01).**

- `App.xaml.cs` is an empty `Application` class: no command-line handling. A `.dino` opens only by the file picker or
  by dropping it on the Open step (`MainViewModel.Dino.cs:36,98`, `OpenStepView.xaml:6`). `LoadDinoAsync(path)` is
  public and cancels an earlier load.
- No file association or protocol in `packaging/msix/AppxManifest.xml`; no single-instance lock. The Store submission
  is dropped, so users run the self-contained exe from `release.yml`, not an MSIX.
- Dino Reader's Windows head is an MSIX (`AshkanSirous.DinoReader`) with no file association or protocol; it can share
  a `.dino` only in developer mode (`StoryActions.cs:88-102`). How Dino will launch us is not checked.

**Decisions** (locked with the user via RefineScope, 2026-11-01):

- The app accepts `PrintTheDamnBook.exe <path-to.dino> [--language <code>]`.
- On every start it adds itself to Windows' "Open with" list for `.dino`, for the current user only (HKCU), without
  making itself the default. Dino Reader can then save a `.dino` and ask Windows to open it with this app.
- The book is copied into this app's cache at once (`DinoBookCache`), so Dino may delete its temporary file.
- A handed-over book lands on the Check step, in the language passed (falling back to the book's own).
- Each hand-over opens a new window; no single-instance lock.

**Rules for this slice.**

- The path is untrusted: accept only an existing local file ending in `.dino`, opened read-only and parsed only by
  `DinoBookReader`; unknown arguments are ignored; `--language` must be one of the book's languages, else the book's
  own is used. A bad path shows the normal "couldn't open" message on the Open step, never a crash.
- Registration writes only `HKCU\Software\Classes\.dino\OpenWithProgids\PrintTheDamnBook.dino`,
  `HKCU\Software\Classes\PrintTheDamnBook.dino` (friendly name, icon, `shell\open\command` = `"<exe>" "%1"`) and
  `HKCU\Software\Classes\Applications\PrintTheDamnBook.exe\SupportedTypes\.dino`. It never touches `UserChoice` or the
  default handler, rewrites the command when the exe has moved, and a failure is logged and ignored. Query context7 /
  Microsoft docs for the exact keys before coding.

**Work.**

- [x] Launch arguments parser (path, `--language`), with tests for quotes, spaces, missing file, wrong extension,
      unknown flags.
- [x] `App.xaml.cs` reads the arguments; `MainWindow` calls `LoadDinoAsync` after it shows, then goes to the Check step
      with the chosen language; the loading state from Slice 15 shows while it reads.
- [x] `OpenWithRegistration` on start, off the UI thread; idempotent; updates a moved exe path.
- [x] README: a short "Open from Dino Reader" section with the command line, for the Dino Reader side to use.
- [ ] Follow-up (not built here): add a slice to Dino Reader's plan for a "Print this book" action on Windows that
      writes the `.dino` and opens it with `PrintTheDamnBook.exe` (and how it finds the exe, or points to the download
      page when it isn't there).

#### Design (Story Text mock, sections 2g-2h, added 2026-11-01)

Two frames, live renders of `docs/design/StoryCheck.dc.html`: 2g the hand-over loading state (`Story Text.dc.html:48`,
launched with a `.dino` path and `--language fa`) and 2h a hand-over that can't be opened (`:52`). Markup
`StoryCheck.dc.html:91-124`, copy `:625-628`. The mock agrees with the plan, and adds the points below.

- **2g, loading.** The Open step's drop card with the folder icon swapped for a spinner (`ph circle-notch` 40 px,
  `--color-accent`, 0.9 s). Title (17 px, 500): "Opening a book from Dino Reader...". Text: "Dino Reader handed me
  Good Enough.dino in Farsi. I'm copying it into my own cache first, so Dino can tidy up its copy." (file name in
  `--color-neutral-200`). The three buttons are disabled and a ghost "Cancel" appears (the Slice 15 loading state).
  When it finishes the app goes to the Check step.
- **2h, can't be opened.** The Open step in its normal state, with an alert above the card: `role="alert"`, max 620 px,
  `--color-surface`, `--radius-md`, `--shadow-sm`, 12/14 px padding; `ph-fill warning` 18 px, `oklch(0.82 0.12 78)`; title
  (500) "I couldn't open what Dino Reader sent"; body "<file> is missing or isn't a .dino book. Open a folder or a Dino
  book here instead." with the file name in a monospace face (`ui-monospace, Consolas`, `--color-neutral-300`); dismiss
  `btn-ghost btn-icon`, `ph x`, title and label "Dismiss". No crash, no dialog.
- **Simplified in the mock:** the file name ("notes.txt") and the book name ("Good Enough.dino") are placeholders.

**New strings:** every quoted string above (English only).

**Open point: error copy.** Plan: "the normal 'couldn't open' message on the Open step". Mock: a dismissible alert with its
own text. **Default:** use the mock for hand-overs; the picker keeps its existing message.
**Open point: language in the loading text.** Mock names the language ("in Farsi"). Plan: a language not in the book falls
back to the book's own. **Default:** name the language actually used; drop "in <language>" when none was passed.
**Open point: Cancel.** Plan does not mention it for hand-overs. Mock shows Cancel. **Default:** keep it (same loader as Slice 15).
Settled by the mock: the loading text tells the user the book is copied into the cache.

**Verification.**

- `dotnet test`: argument parser tests.
- Desktop (device-test, scripted): run the exe with a sample `.dino` path and `--language`; it opens on the Check step
  in that language. Run it with a missing file and a `.txt`; it shows the Open step with the message. Check the HKCU
  keys exist and the default `.dino` handler is unchanged; right-click a `.dino` in Explorer, *Open with* lists the app.
  Two hand-overs open two windows.

#### Cache across windows (design, wave 2026-11-01)

- Folders are content-addressed (`<id>-<hash>`) and finished by a `.complete` manifest (size + name per file, written last), so a verified folder never changes: open reuses it with no writes. No per-open marker files or claims.
- Else extract into `<folder>.tmp-<guid>` (no lock), then under a `Local\` named mutex: another process won and verifies, drop ours; target exists but does not verify, copy its `book.json` in, rename it to `<folder>.old-<guid>`, rename ours in, rename back on failure; else just rename in. Our temp is deleted on any failure.
- Sweeping (older versions of the book, `*.tmp-*`, `*.old-*`) runs only under the mutex and only when no other PrintTheDamnBook process runs; folders this process shows or is opening are skipped. Otherwise the next lone start sweeps.
- Discard deletes only a folder this open created, only when nothing else in this process holds it and no other instance runs, on `Task.Run` so the UI thread never waits on the mutex.
- Pure pieces (manifest format/verify, leftover and older-version name matching) are `Core/Dino/DinoCacheLayout.cs`, unit-tested.

#### Changed while building

- The argument parser and the Open-with key plan live in Core (`HandoverArguments`, `OpenWithRegistrar`, `DinoCacheLayout`), with tests.
- An extra `HKCU\Software\Classes\Applications\PrintTheDamnBook.exe\shell\open\command` is written: Microsoft's Application Registration page says a verb is needed.
- Registration is skipped for exes under `\bin\Debug\`, `\bin\Release\`, `\obj\` or `\WindowsApps\`, and `SHChangeNotify` runs only when something changed.
- The hand-over starts from the `MainWindow` constructor; any failure becomes the hand-over alert.
- The cache was redesigned for several windows (see "Cache across windows" above). `DinoBookCache.cs` and `DinoBookReader.cs` were touched outside Touches (approved).
- Known behaviour: the loading text names the requested language before the book is read; registration only happens from a non-dev exe path; the loading text and Cancel appear after 500 ms (Slice 15 rule); friendly name "Dino Reader book"; two exe copies flip the Open-with command to the last one started.

#### From review

Files in `D:\print-the-damn-book-worktrees\reviews\`.

- High (`slice-19-high.md`): 1-7, 9, 10 applied. 8 skipped: Cancel changed to a ghost button because the design mock asks for a ghost Cancel.
- Medium (`slice-19-medium.md`): 1, 3, 4, 5, 7, 9 applied. Skipped: 2 (one window per process by design); 6 and 10 (low); 8 (as in high).
- Check (`slice-19-check.md`): two review rounds kept finding defects in the cross-process cache, so the coordinator replaced the patches with the by-construction design above. 5, 6, 7 applied. Skipped: 8 (empty marker values written as REG_SZ instead of REG_NONE, low, uncertain) and 9 (relative and `\?\` paths rejected, low).
- Check 2 (`slice-19-check2.md`): 1, 2, 4, 5, 9 applied. 3 is Slice 15 behaviour already on main (deleting older versions of a book also deletes their `book.json`); now an Outstanding item. Skipped: 6 (any drive letter accepted, mapped network drives pass), 7 (whole `.dino` hashed on every open), 8 (Regex built per folder per sweep), 10 (check script lives in the test project dir).
- Check 3 (`slice-19-check3.md`): both applied.
- Check 4 (`slice-19-check4.md`): no findings.

#### Desktop run 2026-11-01: PASS

A `.dino` with `--language fa` and without opens on the Check step; a missing file and a `.txt` show the alert; two hand-overs give two windows; the HKCU keys are written with the right command; `UserChoice` is untouched; the keys were removed afterwards. `handover-check.ps1` had a parse error and was fixed after the run, not re-run.

#### For CLAUDE.md (lifted 2026-11-01)

- Launch arguments are parsed only by Core `HandoverArguments`. (Slice 19: docs/plan/slices/19-open-from-dino-reader.md)
- Open-with registration writes only the three HKCU keys via Core `OpenWithRegistrar` (plus the Applications `shell\open\command`), never `UserChoice`. (Slice 19: docs/plan/slices/19-open-from-dino-reader.md)
- The Dino cache (`Services/DinoBookCache.cs`) is content-addressed and shared by windows: extract to a temp folder and rename under the `Local\` mutex; sweep only when no other instance runs. (Slice 19: docs/plan/slices/19-open-from-dino-reader.md)

#### For README.md (lifted 2026-11-01)

The "Open from Dino Reader" section is already added on this branch.

#### Outstanding (lifted to plan.md 2026-11-01)

- Sweeping older versions of a book also deletes that version's `book.json` (Slice 15 behaviour, found in check 2).
- Dino Reader "Print this book" follow-up (the open Work item above).

#### Not yet verified

- That `--language fa` shows the Farsi text on the Check step (only the step was checked).
- That Explorer's Open with menu lists the app.
- The fixed `handover-check.ps1` end to end.
- The `.alt-` fallback (needs a locked legacy folder).
