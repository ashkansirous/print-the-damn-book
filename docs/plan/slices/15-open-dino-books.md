### Slice 15 — Open a Dino Reader book (`.dino`)

*Planned.* Comes after Slice 14 (it fills Slice 14's text model).

**Touches:** `src/PrintTheDamnBook.Core/Dino/**` (new), `tests/PrintTheDamnBook.Core.Tests/Dino*`,
`tests/PrintTheDamnBook.Core.Tests/TestData/**`, `src/PrintTheDamnBook.App/ViewModels/MainViewModel.cs`,
`src/PrintTheDamnBook.App/Views/OpenStepView.xaml*`, `src/PrintTheDamnBook.App/Services/**`

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

- [ ] Core `DinoBookReader` → a book (title, language, cover, pages with text), with tests on a small `.dino` built
      in the test from fixture bytes (no copyrighted art in git), plus tests for a schema 2 file, a missing image
      entry and a missing `story.json`.
- [ ] Translations: list them, pick one, RTL ones flagged. Tests.
- [ ] App: open and drop `.dino`; extract to the cache folder; load into the existing wizard; loading state over 500 ms.
- [ ] Title on the front cover.

**Verification.**

- `dotnet test` is green.
- Open `ReadTheStupidText/src/DinoReader.App/Resources/Raw/starters/three-little-pigs` (`.dino`, 16 pages): all pages
  show with their text, the cover shows the title; switch to another translation; print to PDF and check by eye.
