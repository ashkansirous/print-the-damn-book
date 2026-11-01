### Slice 19 — Open a book handed over by Dino Reader

*Planned 2026-11-01.*

**Touches:** `src/PrintTheDamnBook.App/App.xaml`, `src/PrintTheDamnBook.App/App.xaml.cs`,
`src/PrintTheDamnBook.App/Services/LaunchArguments.cs` (new), `src/PrintTheDamnBook.App/Services/OpenWithRegistration.cs`
(new), `src/PrintTheDamnBook.App/ViewModels/MainViewModel.Dino.cs`, `src/PrintTheDamnBook.App/MainWindow.xaml.cs`,
`src/PrintTheDamnBook.Core/Dino/**` (argument parsing, if kept in Core), `tests/PrintTheDamnBook.Core.Tests/**`,
`README.md`

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

- [ ] Launch arguments parser (path, `--language`), with tests for quotes, spaces, missing file, wrong extension,
      unknown flags.
- [ ] `App.xaml.cs` reads the arguments; `MainWindow` calls `LoadDinoAsync` after it shows, then goes to the Check step
      with the chosen language; the loading state from Slice 15 shows while it reads.
- [ ] `OpenWithRegistration` on start, off the UI thread; idempotent; updates a moved exe path.
- [ ] README: a short "Open from Dino Reader" section with the command line, for the Dino Reader side to use.
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
