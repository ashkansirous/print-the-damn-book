### Slice 01 — Open a folder and preview the imposed sheets

*Built 2026-10-22 (awaiting merge). Desktop-tested 2026-10-22; thumbnail look not yet checked by eye.*

**Touches:** `PrintTheDamnBook.slnx`, `global.json`, `Directory.Build.props`, `.editorconfig`,
`src/PrintTheDamnBook.Core/**`, `src/PrintTheDamnBook.App/**`,
`tests/PrintTheDamnBook.Core.Tests/**`, `.github/workflows/ci.yml`

**Why.** The whole pain is working out which page goes on which side of which sheet. The
smallest useful thing is: open the folder, see the sheets laid out exactly as they will print.

**Decisions.**

- Latest GA .NET (10, LTS, SDK 10.0.301 installed) pinned in `global.json` with
  `rollForward: latestMajor` so .NET 11 picks up after its GA; WPF; C# 14 with nullable and
  implicit usings on; `PrintTheDamnBook.slnx` solution format. MVVM via CommunityToolkit.Mvvm.
  Tests in xUnit.
- Three projects: `PrintTheDamnBook.Core` (net10.0, no WPF reference: models, ordering,
  imposition, layout maths), `PrintTheDamnBook.App` (net10.0-windows, WPF, the only project
  that touches printers or files), `PrintTheDamnBook.Core.Tests`.
- Page order from the folder: `Cover*` first, `Back cover*` last, everything else by natural
  numeric sort of the filename stem (`2` before `10`). Unrecognised names go after the numbered
  pages, before the back cover, in alphabetical order, flagged in the UI.
- Imposition (0-based `p[0..N-1]`, `p[0]` = cover, `p[N-1]` = back cover, N padded to a multiple
  of 4 with blanks inserted just before the back cover, `S = N / 4` sheets): sheet `k` front is
  `left = p[N-1-2k]`, `right = p[2k]`; back is `left = p[2k+1]`, `right = p[N-2-2k]`.
  For the 12-page sample this gives back cover+cover / 1+10, 9+2 / 3+8, 7+4 / 5+6, which is
  what the user prints by hand today.
- Every library used (CommunityToolkit.Mvvm, xUnit, WPF image loading) is checked in context7
  via `library-docs` before code is written.

**Work.**

- [x] Scaffold solution, three projects, `global.json`, `Directory.Build.props`
      (TreatWarningsAsErrors, nullable), `.editorconfig`, `ci.yml` running `dotnet build` and
      `dotnet test` on push and PR (windows-latest).
- [x] Core: `BookPage` (path, label, kind: Cover / Numbered / Other / BackCover / Blank),
      `PageOrdering.FromFolder(IEnumerable<string> fileNames)`, `Imposition.Compute(pages)`
      returning `Sheet[]` with `Front`/`Back` faces each holding `Left`/`Right` page refs.
- [x] Tests: ordering of the sample names; imposition for 4, 8, 12 pages; padding for 5, 6, 7
      and 10 pages puts blanks before the back cover; every page used exactly once.
- [x] App: "Open folder" button, loads `*.jpg|*.jpeg|*.png`, shows the ordered page strip
      (thumbnails with labels) and a sheet preview: one card per sheet, front and back, each
      face two thumbnails side by side with the page label under each.
- [x] Warning banner when the book needs more than 6 sheets (too thick to fold flat).

**Verification.**

- `dotnet test` green; CI green on the PR.
- Open `D:\print-the-damn-book\Book sample` (ignored by git, present on the user's machine):
  3 sheets previewed as back cover+cover / 1+10, 9+2 / 3+8, 7+4 / 5+6.

*Changed while building:*

- Scaffold: SDK 10.0.301 with `latestMajor`; the Microsoft Testing Platform runner is selected in `global.json`; tests use xUnit v3 (30 tests). CI (windows-latest) runs format verify, build and test.
- `Cover` and `Back cover` match by exact filename stem, case-insensitive (not a `Cover*` prefix). A second match is demoted to an ordinary page and raises a `DuplicateCover` / `DuplicateBackCover` warning.
- With no back cover, blanks are appended at the end of the book.
- The warning banner lists every issue, including unrecognised file names and images that cannot be decoded, not only the more-than-6-sheets case.
- LF line endings are enforced via `.editorconfig` and `.gitattributes`.

*From review:* two passes, 20 findings applied, including a crash on a corrupt image, thumbnail decoding on the UI thread, `Uri` handling of paths containing `#` or `%`, and newest-load-wins when folders are opened in quick succession.

*Desktop test 2026-10-22:* the 12-page sample folder gave 3 sheets in the expected order with no banner; a scratch folder produced the correct warnings.

#### For CLAUDE.md

- `dotnet format PrintTheDamnBook.slnx --verify-no-changes` is a check command and CI enforces it; run it before pushing. (Slice 01: docs/plan/slices/01-folder-to-sheet-preview.md)
- Files are LF (`.gitattributes`, `.editorconfig`); don't convert line endings. (Slice 01: docs/plan/slices/01-folder-to-sheet-preview.md)
- Thumbnail decoding happens off the UI thread in `LoadFolderAsync`; keep it there. (Slice 01: docs/plan/slices/01-folder-to-sheet-preview.md)

#### Not yet verified

- Visual check by eye of thumbnail aspect ratio and scrolling on the sample folder.
