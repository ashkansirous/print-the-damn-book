### Slice 15 — Open a Dino Reader book (`.dino`)

*Built 2026-10-27 (PR #29); desktop acceptance 8 of 10, then the two failures (Cancel, drop) fixed or confirmed on re-check; memory follow-up in Not yet verified.* Slices 14 (PR #26) and 16 (PR #27) merged, so the cover title can use the top band.

**Touches:** `src/PrintTheDamnBook.Core/Dino/**` (new), `src/PrintTheDamnBook.Core/Text/**`,
`src/PrintTheDamnBook.Core/Ordering/**`, `tests/PrintTheDamnBook.Core.Tests/**`,
`src/PrintTheDamnBook.App/ViewModels/**`, `src/PrintTheDamnBook.App/Views/OpenStepView.xaml*`,
`src/PrintTheDamnBook.App/Views/CheckStepView.xaml*`, `src/PrintTheDamnBook.App/Services/**`

**Why.** The user has written 10+ books in Dino Reader (`D:\ReadTheStupidText`) and wants to print them, and other
people's, without retyping the text.

**Format (grounded 2026-10-26).** A `.dino` file is a zip with top-level entries: `package.json`
(`schemaVersion` 1, `id`, `title`, `language`), `story.json` (PascalCase: `Title`, `Language`, `CoverFile`,
`Pages[]` of `{Text, ImageFile, Lines, Regions}`, optional `Translations`, plus `Cast`, voices and `Recordings`), and
the JPEGs (`page-1.jpg`…, `cover-*.jpg`). Pictures are portrait 3:4 (896×1200) with no text drawn in them. The
reader and writer in `ReadTheStupidText/src/DinoReader.Core/StoryPackageReader.cs` is the reference.

**Decisions** (locked with the user via RefineScope, 2026-10-26):

- Our own small reader in Core (`System.IO.Compression`), no reference to the ReadTheStupidText projects. It reads
  only `package.json` `schemaVersion` (refuse anything above 1 with a clear message), `Title`, `Language`,
  `CoverFile`, `Pages[].Text`, `Pages[].ImageFile` and `Translations`. `Cast`, voices, recordings, `Lines` and
  `Regions` are ignored.
- **Cover:** `CoverFile` becomes the front cover with the title drawn on it (Slice 14's band text, top band if Slice 16
  has landed, otherwise bottom). No back cover in the file: the book gets a blank back cover.
- Each page becomes a picture page with its text *On the picture*, bottom band (the Dino Reader default); the user can
  switch any page to *Own page* in the Check step.
- **Language:** the book's own `Language` by default; a picker lists the `Translations` in the file. Right-to-left
  languages (Farsi) are listed but disabled with "right-to-left books are not supported yet".
- Images are extracted to a per-book folder under `%LOCALAPPDATA%\PrintTheDamnBook\dino\<id>\`, and the text edits
  are saved to `book.json` there, so reopening the same `.dino` keeps the edits.
- Open step: "Open a folder" gains "or open a Dino book" (file picker for `*.dino`, and dropping a `.dino` file).

**Work.**

- [x] Core `DinoBookReader` → a book (title, language, cover, pages with text), with tests on a small `.dino` built
      in the test from fixture bytes (no copyrighted art in git), plus tests for a schema 2 file, a missing image
      entry and a missing `story.json`.
- [x] Translations: list them, pick one, RTL ones flagged. Tests.
- [x] App: open and drop `.dino`; extract to the cache folder; load into the existing wizard; loading state over 500 ms.
- [x] Title on the front cover.

#### Design (Story Text mock, section 1e and 1f, added 2026-10-26)

Two frames (1e Open step, 1f Check step for a Dino book), live renders of `StoryCheck.dc.html`. The mock agrees with
the Decisions above.

- **Open step.** Drop zone "Drop a folder or a Dino book here" (while reading: "Reading the book..."). Buttons "Open
  folder..." (primary) and "Open a Dino book..." (secondary). Sub-line: "Name them Cover, 1, 2, 3 ... Back cover, or
  open a book you made in Dino Reader. Its words come along." The example row gains a `.dino` tag. Rail sub-text for
  step 1: "A folder or a Dino book".
- **Check step for a Dino book.** Heading "Check the order and the words"; sub-line "Every page came with its words.
  Click one to change them or move them to a page of their own." The cover is selected first. Save status "Saved with
  this Dino book".
- **Language select** (Phosphor `translate`) left of the Reading order / As printed switch. The book's own language
  first ("English (the book's own)"), translations after ("Deutsch"). Right-to-left ones are listed but disabled:
  "right-to-left books are not supported yet" (Farsi in the mock).
- **Simplified in the mock:** the book is seeded placeholder data; the 900 ms load is a timer.

**New strings:** the quoted copy above.

**Verification.**

- `dotnet test` is green.
- Open `ReadTheStupidText/src/DinoReader.App/Resources/Raw/starters/three-little-pigs.dino` (16 pages): all pages
  show with their text, the cover shows the title; switch to another translation; print to PDF and check by eye.

*Changed while building:*

- The language select sits above the warning chips in `CheckStepView` (the view switch is in `MainWindow.xaml`, outside Touches).
- A `.dino` dropped outside the Open step's drop zone isn't taken (`MainWindow.xaml.cs` is outside Touches).
- Icons are local to `OpenStepView` / `CheckStepView` (`Icons.xaml` is outside Touches).
- The cover title stays in the book's own language (translations are keyed by sentence, no titles).
- Pages with no picture are skipped with a notice. Dino pages get the Bottom band explicitly.
- The chosen language is saved in an optional `Language` field in `book.json`. A saved language no longer offered stays as "(as saved with this book)".
- RTL own language starts on the first LTR translation with a note; none available means the book is refused.
- The blank back cover is a generated white PNG in the cache. Cover-from-page books are supported.
- Cache folder is `<id>-<16 hex file hash>`; older hash folders of the same id are deleted on open.
- Caps: 500 pages, 20,000 chars per page, 5,000 translation entries, 200 MB written.
- A failed or cancelled open keeps the open book. Open and language switch show a spinner and Cancel after 500 ms.

*From review:* high found 14, applied 1-10 and 14, skipped 11, 12, 13. Medium found 10, applied all (9 was already handled). 293 tests pass, including hostile-zip tests.

#### For CLAUDE.md

- .dino files are untrusted: parse only through `Core/Dino` (`DinoBookReader`) with its caps and name checks; extracted books live in the per-hash cache (`DinoBookCache`), never next to the .dino. (Slice 15: docs/plan/slices/15-open-dino-books.md)

#### For README.md

- Open a Dino Reader book (`.dino`): pictures, words and the title on the cover, and choose a language.

#### Not yet verified

- Memory is not returned after a very large book: about 1.6 GB stays held after a 420-page stress book is replaced (also on main for folder opens; thumbnails alone don't explain it; forced GC didn't help). Needs a profiler investigation.
- About 3 in 18 cancelled opens leave that open's cache folder behind (the delete swallows IOException); it is cleaned when the same book is reopened.
- The drop-zone drag was checked with a real Explorer drag only once.
