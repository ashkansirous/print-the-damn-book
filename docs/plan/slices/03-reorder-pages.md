### Slice 03 — Reorder, add and remove pages

*Built 2026-10-22, desktop-tested on the sample folder (50 tests green); awaiting merge. Not yet verified: see the end of this file.*

**Touches:** `src/PrintTheDamnBook.App/Views/PageStrip.xaml`, `src/PrintTheDamnBook.App/Views/PageStrip.xaml.cs`,
`src/PrintTheDamnBook.App/MainWindow.xaml` (swap the inline strip for the PageStrip view), `src/PrintTheDamnBook.App/ViewModels/**`,
`src/PrintTheDamnBook.Core/Ordering/**`, `src/PrintTheDamnBook.Core/PageOrdering.cs`, `tests/PrintTheDamnBook.Core.Tests/Ordering*`

**Why.** Filenames get the order right most of the time; the user still needs to fix a
mis-numbered page, drop one, or add a file from elsewhere without renaming on disk.

**Decisions.**

- The page strip supports drag-and-drop reorder, a Remove button per page, and "Add pages"
  (file picker, multi-select, appended before the back cover).
- Cover and back cover are pinned to the ends; dragging another page past them is refused.
- Blank padding pages are shown in the strip (greyed, labelled "blank") and recomputed after
  every change; they cannot be dragged or removed by hand.
- The sheet preview updates live after every change (Core recomputes; the view binds).
- Ordering logic (`PageList.Move`, `Remove`, `Insert`, pinning rules) lives in Core and is
  unit-tested; the view only raises intents.

**Work.**

- [x] Core `PageList` with move/remove/insert, pinned ends, re-padding; tests.
- [x] Drag-and-drop in the strip (WPF drag events, drop indicator), Remove, Add pages.
- [x] Mark pages whose filename was not recognised (slice 1) so the user sees what to check. (Hand-added pages, `PageKind.Added`, get no warning or badge.)

**Verification.**

- `dotnet test` green.
- Manual: load the sample, drag page 3 before page 2, preview shows 9+3 / 2+8 on sheet 2;
  remove page 10, preview shows 11 pages padded to 12 with one blank before the back cover.

*Changed while building:*

- Core `Ordering/PageList` does identity-based `Move`/`MoveToEnd`/`Remove`/`Add`; hand-added pages are `PageKind.Added`, placed before the back cover. `Imposition.Impose(padded)` now throws unless the count is a multiple of 4.
- Remove is allowed on pinned pages (the spec pins drag only). Added files are never auto-classified as covers.
- Touched `Core/Imposition.cs`, `App.xaml` and a new shared `Views/PageResources.xaml` (merged in `App.xaml`) beyond the Touches line (approved).
- App: `Views/PageStrip` has drag-drop with highlight and edge auto-scroll, a Remove button per page, and "Add pages..." (disabled until a folder is open). `MainViewModel.Refresh` recomputes sheets and warnings.

*From review:* 2 passes, 18 findings applied, including an index-vs-padded-strip bug that unpinned the back cover, a stale add after a folder change, add/load races and drag state.

*Desktop test 2026-10-22 (sample folder):* drag 3 to 1 reorders strip and sheets; covers refuse drag; removing 5 pads a blank before the back cover; adding 5.jpg lands before the back cover with no banner; Add is disabled before a folder is open.

#### For CLAUDE.md (lifted 2026-10-22)

- `PageList` (`src/PrintTheDamnBook.Core/Ordering/`) is the single source of page order; App items carry the `BookPage` identity, never positions. (Slice 03: docs/plan/slices/03-reorder-pages.md)

#### Not yet verified

- Remove buttons are absent from the UI Automation tree (screen readers); needs `AutomationProperties`.
- Whether Cover and Back cover should be removable.
