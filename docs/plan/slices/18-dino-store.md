### Slice 18 — Get a story from Dino's store

*Built (wave 2026-11-01), merged as PR #34 (e83ab51).*

**Touches:** `src/PrintTheDamnBook.Core/Dino/**` (store catalog parsing, new), `tests/PrintTheDamnBook.Core.Tests/**`,
`src/PrintTheDamnBook.App/Services/DinoStoreClient.cs` (new), `src/PrintTheDamnBook.App/Services/DinoBookCache.cs`,
`src/PrintTheDamnBook.App/Views/OpenStepView.xaml*`, `src/PrintTheDamnBook.App/Views/DinoStoreDialog.xaml*` (new),
`src/PrintTheDamnBook.App/ViewModels/MainViewModel.Dino.cs`, `src/PrintTheDamnBook.App/ViewModels/DinoStoreViewModel.cs`
(new), `site/public/privacy.html` (line 7 meta description too), `src/PrintTheDamnBook.App/MainWindow.xaml*`,
`src/PrintTheDamnBook.App/ViewModels/MainViewModel.cs` (added at the claim, wave 2026-11-01)

**Wave note (2026-11-01, coordinator's choice).** The downloaded `.dino` lives in
`%LOCALAPPDATA%\PrintTheDamnBook\store\<id>-v<version>.dino` and is then opened through `LoadDinoAsync(path)` exactly like a
picked file, which extracts it into `DinoBookCache` as usual; `DinoBookCache.cs` changes only if that path needs it. Why:
one open path, and the store folder is the reuse cache for re-downloads. Card enablement for right-to-left languages uses
the same Core language-support check the Dino language select uses, so it follows Slice 17 (merged first) without a
second rule. Open points take the defaults written below. Slices 17 and 19 also edit `MainViewModel.Dino.cs`: keep
this slice's edits to `OpenDinoAsync`/the picker block and new members. Merge order 17, 19, 18.

**Why.** The user wants to pick and print a book from Dino Reader's free story store without first getting the `.dino`
into Dino Reader (2026-11-01).

**What exists (grounded 2026-11-01, from `D:\ReadTheStupidText`; the live endpoint was not called).**

- Catalog: `GET https://dinoreader.sirous.uk/api/stories` (`src/DinoReader.App/StoreClient.cs:32,34`; server
  `worker/stories.ts`, R2 bucket `readthestupidtext-downloads`, 60 s cache). Entry fields
  (`StoreCatalog.cs:6-9`): Id, Title, Language, SchemaVersion, Version, PageCount, Minutes, CoverUrl, DownloadUrl,
  Credits, AgeMin, AgeMax, Author, Titles (per language), CreditsByLanguage. No price, no byte size.
- Download: `GET /api/stories/<id>/download` (optional `?v=n`) returns a `.dino` zip; cover at `/api/stories/<id>/cover`.
- No auth, key, device id or purchase. Dino sends plain GETs with no cookies (`StoreClient.cs:26-27`). Dino's only gate
  is its in-app grown-up check.
- The format and size caps match our `Core/Dino/DinoBookReader.cs:18-27` (schema 1, 200 MB total).
- Our app makes no network calls today (not checked beyond this slice's grounding; confirm while building).

**Decisions** (locked with the user via RefineScope, 2026-11-01):

- A *Get a story from Dino* button on the Open step, beside "Open a Dino book...". It opens a dialog listing each
  book's cover, title, language, page count and age range, filterable by language. No grown-up gate.
- Choosing a book downloads its `.dino` into the existing cache and opens it through the same path as a picked
  `.dino` (`LoadDinoAsync`). Every wait over 500 ms shows what it waits for, with Cancel.
- Store books in a right-to-left language behave as in Slice 17 (disabled until it lands, enabled after).

**Rules for this slice.**

- The downloaded file is untrusted: parse only through `DinoBookReader` (Slice 15 rule). HTTPS only; follow only URLs
  on `dinoreader.sirous.uk` under `/api/stories/` (the catalog's `CoverUrl`/`DownloadUrl` are checked against that).
  Stop a download past 200 MB (by `Content-Length` and by counting bytes). Catalog JSON capped (e.g. 1 MB) and
  validated field by field. Entries with `SchemaVersion` above 1 are hidden.
- Send nothing but plain GETs: no identifiers, no cookies, no analytics.
- Cover images are decoded through `Services/ImageDecoder`, off the UI thread.

**Work.** First item: the end-to-end path with a plain text list.

- [x] Core: `DinoStoreCatalog` parser (entries, validation, URL allow-list, schema filter, language list). Tests from
      fixture JSON: good catalog, missing fields, a foreign host, schema 2, oversize.
- [x] App: `DinoStoreClient` (`HttpClient`, timeouts, size cap, cancel) downloading to
      `%LOCALAPPDATA%\PrintTheDamnBook\store\<id>-v<version>.dino`, reusing a finished download of the same version;
      partial files are deleted on failure or cancel.
- [x] App: Open-step button and `DinoStoreDialog` (Nocturne tokens, FocusRing, `AutomationProperties.Name` on every
      control; no mock exists, reuse the Open step's card style): list, language filter, "Loading stories...", an
      offline/failed state with Retry, then download progress with Cancel, then the Check step.
- [x] App: covers in the list, loaded lazily with a placeholder.
- [x] Site: if `site/public/privacy.html` says the app makes no network requests, add that it fetches Dino's story
      list and books only when the store is opened, and sends no identifiers.

#### Design (Story Text mock, sections 2d-2f, added 2026-11-01)

Three frames, live renders of `docs/design/StoryCheck.dc.html`: 2d the list (`Story Text.dc.html:36`), 2e downloading
(`:40`), 2f offline (`:44`). The mock agrees with the plan, and adds the points below. Dialog markup is
`StoryCheck.dc.html:274-337`; card data `:389-400`; state logic `:538-558`, `:661-675`.

- **Open step.** Third button after "Open folder..." and "Open a Dino book...": `btn-secondary`, `ph storefront`,
  "Get a story from Dino" (`:122`). All three buttons are disabled while a load runs. The drop text reads "A folder
  of page images (JPG or PNG), a .dino book from Dino Reader, or a free story from Dino's store. I don't touch the files."
  and the sub-heading "Name them Cover, 1, 2, 3 ... Back cover, open a book you made in Dino Reader, or get a free one
  from Dino's store. Its words come along."
- **Dialog frame.** Modal `.dialog`, `role="dialog"`, `aria-label="Stories from Dino"`, `width: min(880px, 100%)`,
  60% `--color-neutral-900` scrim over the window, 40 px padding. Title "Stories from Dino" (`dialog-title`), sub-line "Free
  books from Dino Reader's store. Pick one and I'll open it here, words and all." Close: `btn-ghost btn-icon`, `ph x`,
  title and label "Close". Footer line, always shown, 11 px `--color-neutral-500`, `ph shield-check` 13 px: "I fetch
  only the list, the covers and the book you pick. Nothing about you is sent."
- **Loading (list).** Spinner `ph circle-notch` 32 px `--color-accent` (0.9 s), "Loading stories...", "Asking
  dinoreader.sirous.uk for its list.", a ghost "Cancel" that closes the dialog. Body min-height 420 px.
- **List (2d).** Language select top right with a `ph translate` icon, `aria-label="Language"`: "All languages · 10" then
  one entry per language ("English · 4", "فارسی · 2" ...). Grid of 5 columns, 14 px / 10 px gaps, scrolls past 540 px. Each
  card is a button: 3:4 cover on `--color-neutral-800` with `--shadow-md`, then title (13 px, 500, one line, ellipsis; own
  direction and Vazirmatn for Arabic script), then "<Language> · 12 pages · ages 4–7" (11 px, `--color-neutral-500`). Hover
  tints 6% text colour. Tooltip "Download and open".
- **Lazy covers.** Covers appear one by one (~110 ms apart); until then a `ph image` 22 px, `--color-neutral-600` on the box.
- **Not-supported card.** Hebrew is listed, opacity 0.45, not clickable, tooltip "Hebrew books are not supported yet", and
  the line "This language is not supported yet". Farsi and Arabic cards are enabled.
- **Downloading (2e).** Body: 132 px 3:4 cover; "Downloading <title>..." (title isolated, own direction); 4 px progress
  bar (`role="progressbar"`, `aria-label="Download"`, `--color-neutral-800` track, `--color-accent` fill with glow);
  "3.2 of 8.4 MB"; `btn-secondary` "Cancel". On finish the dialog closes and the Check step opens on the cover.
- **Cancel (adds behaviour).** Cancel during a download returns to the list with a note above the grid, `ph info` 14 px
  `--color-accent-300`, 12 px: "Download cancelled. Nothing was kept." Cancel while loading the list closes the dialog.
- **Failed (2f).** `ph wifi-slash` 32 px `--color-neutral-400`; "I couldn't reach Dino's store"; "Check the internet
  connection and try again. Folders and .dino files on this PC still open as usual."; `btn-primary` with `ph
  arrow-clockwise` "Try again", and ghost "Close".
- **Simplified in the mock:** the ten catalog entries, covers (sample pages) and the byte count (8.4 MB) are invented.

**New strings:** every quoted string above.

**Open point: Retry label.** Plan: "Retry". Mock: "Try again" plus a "Close" button. **Default:** use the mock.
**Open point: cancelled download.** Plan: Cancel, nothing more. Mock: back to the list with the note. **Default:** use the mock.
**Open point: Hebrew card.** Plan: right-to-left books "disabled until [Slice 17] lands, enabled after". Mock: Hebrew
stays disabled, Farsi and Arabic enabled. **Default:** after Slice 17 enable only Arabic-script languages.
Settled by the mock: the language filter is a select with counts; the privacy line is in the dialog itself, so the
`privacy.html` text should say the same.

**Verification.**

- `dotnet test`: catalog parser tests.
- Desktop (device-test, scripted): open the store, see the list with covers, filter by language, download one book,
  land on the Check step with its pages and text; cancel a download mid-way and see no partial file; disconnect the
  network and see the failed state with Retry.

**Changed while building:**

- Catalog parser `DinoStoreCatalog`: 1 MB cap, 500 entries, BOM stripped, duplicate ids dropped, schema above 1 hidden,
  https allow-list on `dinoreader.sirous.uk` under `/api/stories/`, relative URLs resolved. A present but disallowed
  `downloadUrl` drops the entry.
- Capped transfer and `.part`/rename logic live in Core `DinoStoreTransfer` (tested). Stall timeout 30 s. No redirects are
  followed (live server checked 2026-11-01: it sends none).
- Downloads go to `%LOCALAPPDATA%/PrintTheDamnBook/store/<id>-v<version>.dino`, then open through `LoadDinoAsync`, which now
  returns `DinoLoadResult` (Opened/Failed/Rejected/Skipped). A file is deleted only when Rejected; older versions are pruned
  only when Opened.
- Card support uses Slice 17's `DinoBook.DescribeLanguage` check (Farsi and Arabic on, other right-to-left languages off).
- The scrim uses the existing `BackdropBrush` at 50% where the mock says 60% (no 60% token).
- Privacy text says no identifiers, cookies or analytics, and that the store sees the PC's internet address (`privacy.html`,
  dialog footer, `docs/store/listing.md`). `SubheadingOpen` updated in `MainViewModel.Wizard.cs`. An empty-catalog state was added.
- Known behaviour: a download that finishes at the moment of Cancel is kept and reused next time; the loaded Open card has no
  store button (as the mock); the dialog repeats `NocturneDialog`'s cover-the-owner code.

**From review** (files in `D:/print-the-damn-book-worktrees/reviews/`):

- `slice-18-high.md`: all 10 applied.
- `slice-18-medium.md`: 1-9 applied; 10 skipped (redirect handling; the live server has no redirects).
- `slice-18-check.md`: 1 and 3-8 applied; 2, 9 and 10 skipped.
- `slice-18-check2.md`: 1-4 and 6 applied; 5 left (low).
- `slice-18-check3.md`: all 4 applied (4 after merging Slice 17).
- `slice-18-check4.md`: 7 findings, all low, none applied.

**Desktop run 2026-11-01: PASS.** Button present; 15 live stories listed, covers fetched; downloading "Good Enough" lands
on the Check step with 12 pages and text; Cancel leaves no `.part` or `.dino`; offline (process-only proxy) shows "I couldn't
reach Dino's store" with Try again; `--language fa` hand-over of a store book opens with Right to left selected.

#### For CLAUDE.md (lifted 2026-11-01)

- Store network access goes only through `Services/DinoStoreClient` with the `DinoStoreCatalog` allow-list: plain GETs, no
  identifiers. Downloaded files go through `DinoBookReader` like any .dino. The site's privacy text (`site/public/privacy.html`)
  and the dialog footer must stay in step. (Slice 18: docs/plan/slices/18-dino-store.md)

#### Verified 2026-11-01 (host desktop, Release build of main 96cbf2f)

- All 16 cover thumbnails rendered. The language filter shows "All languages · 16" and "English · 16"; picking
  English keeps 16 cards. Right-aligned Farsi text on the Check step: covered by the Slice 17 check.

#### Not yet verified

- The language filter with more than one language, and Farsi/Arabic store cards (the live store has only
  English books).
