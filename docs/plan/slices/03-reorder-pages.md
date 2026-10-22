### Slice 03 — Reorder, add and remove pages

*Planned.*

**Touches:** `src/PrintTheDamnBook.App/Views/PageStrip.xaml`, `src/PrintTheDamnBook.App/Views/PageStrip.xaml.cs`,
`src/PrintTheDamnBook.App/ViewModels/**`, `src/PrintTheDamnBook.Core/Ordering/**`,
`tests/PrintTheDamnBook.Core.Tests/Ordering*`

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

- [ ] Core `PageList` with move/remove/insert, pinned ends, re-padding; tests.
- [ ] Drag-and-drop in the strip (WPF drag events, drop indicator), Remove, Add pages.
- [ ] Mark pages whose filename was not recognised (slice 1) so the user sees what to check.

**Verification.**

- `dotnet test` green.
- Manual: load the sample, drag page 3 before page 2, preview shows 9+3 / 2+8 on sheet 2;
  remove page 10, preview shows 11 pages padded to 12 with one blank before the back cover.
