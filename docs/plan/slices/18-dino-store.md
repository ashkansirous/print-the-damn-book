### Slice 18 — Get a story from Dino's store

*Planned 2026-11-01.*

**Touches:** `src/PrintTheDamnBook.Core/Dino/**` (store catalog parsing, new), `tests/PrintTheDamnBook.Core.Tests/**`,
`src/PrintTheDamnBook.App/Services/DinoStoreClient.cs` (new), `src/PrintTheDamnBook.App/Services/DinoBookCache.cs`,
`src/PrintTheDamnBook.App/Views/OpenStepView.xaml*`, `src/PrintTheDamnBook.App/Views/DinoStoreDialog.xaml*` (new),
`src/PrintTheDamnBook.App/ViewModels/MainViewModel.Dino.cs`, `src/PrintTheDamnBook.App/ViewModels/DinoStoreViewModel.cs`
(new), `site/public/privacy.html`

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

- [ ] Core: `DinoStoreCatalog` parser (entries, validation, URL allow-list, schema filter, language list). Tests from
      fixture JSON: good catalog, missing fields, a foreign host, schema 2, oversize.
- [ ] App: `DinoStoreClient` (`HttpClient`, timeouts, size cap, cancel) downloading to
      `%LOCALAPPDATA%\PrintTheDamnBook\store\<id>-v<version>.dino`, reusing a finished download of the same version;
      partial files are deleted on failure or cancel.
- [ ] App: Open-step button and `DinoStoreDialog` (Nocturne tokens, FocusRing, `AutomationProperties.Name` on every
      control; no mock exists, reuse the Open step's card style): list, language filter, "Loading stories...", an
      offline/failed state with Retry, then download progress with Cancel, then the Check step.
- [ ] App: covers in the list, loaded lazily with a placeholder.
- [ ] Site: if `site/public/privacy.html` says the app makes no network requests, add that it fetches Dino's story
      list and books only when the store is opened, and sends no identifiers.

**Verification.**

- `dotnet test`: catalog parser tests.
- Desktop (device-test, scripted): open the store, see the list with covers, filter by language, download one book,
  land on the Check step with its pages and text; cancel a download mid-way and see no partial file; disconnect the
  network and see the failed state with Retry.
