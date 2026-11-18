### Slice 24 — Paid Dino books must be bought before they print ($0.99 print purchase)

*Parked: the user said "skip this slice" on 2026-11-18, asked whether to run the Dino server change it needs (ReadTheStupidText Slice 408) that night. Comes back only when the user asks in an interactive session.*

**Touches:** `src/PrintTheDamnBook.Core/Net/` (new), `src/PrintTheDamnBook.Core/Dino/DinoStoreCatalog.cs`, `src/PrintTheDamnBook.Core/Dino/PrintAccess.cs` (new), `src/PrintTheDamnBook.App/Purchases/` (new), `src/PrintTheDamnBook.App/Services/DinoStoreClient.cs`, `src/PrintTheDamnBook.App/ViewModels/DinoStoreViewModel.cs`, `src/PrintTheDamnBook.App/ViewModels/MainViewModel.Wizard.cs`, `src/PrintTheDamnBook.App/Printing/PrintViewModel.cs`, `src/PrintTheDamnBook.App/Views/DinoStoreDialog.xaml`, `src/PrintTheDamnBook.App/Views/DinoStoreDialog.xaml.cs`, `src/PrintTheDamnBook.App/Views/PrintStepView.xaml`, `src/PrintTheDamnBook.App/PrintTheDamnBook.App.csproj`, `packaging/msix/**`, `.github/workflows/release.yml`, `site/public/privacy.html`, `tests/**`, `tools/desktop-test/**`

**Server side:** ReadTheStupidText Slice 408 (`docs/plan/slices/408-print-app-purchases.md` in that repo) adds the print products and the print check to the Dino Worker. This slice's purchase check cannot pass against the real server until 408 is merged and the Partner Center steps below are done; the test-build bypass works without it.

## Why

The user, 2026-11-18: "most of the books now are behind a filter making sure they are bought. we should add the same feature here so that then we can communicate with the store and make sure they bought the product before they can print the books other than the free books. the error handling should also be working so that in case of error, the api errors from the store comes back with proper message just like the handling we have for the dino book reader. while I have not rolled it out to test, I'd like to have a way to by pass the payment so I can just test the printing features on windows. the price for the books to print is $0.99 when we go live".

Today `DinoStoreEntry` has no access or product field (`Core/Dino/DinoStoreCatalog.cs:8-9`), so the app can't tell a paid book from a free one. A paid book's download gets a 402 from the Worker, and `DinoStoreClient.SendAsync` (`:256-263`) turns every non-2xx into one vague "Dino's store did not give that book."

## Decisions (user, 2026-11-18)

1. **Copy the Dino reader's Windows store pattern exactly.** The user said: "do ... exactly as we do in the dino reader ... app to get the ... access to the ... store! copy the ... behaviour. create your own ... purchase pattern and products." The source to copy is ReadTheStupidText `src/Dino/DinoReader.App/Platforms/Windows/Purchases/WindowsPurchases.cs`:
   - `StoreContext.GetDefault()` and `InitializeWithWindow.Initialize` with the WPF window's hwnd (`WindowInteropHelper`).
   - `GetAssociatedStoreProductsAsync(["Durable"])` for prices from `Price.FormattedPrice`, and `GetUserCollectionAsync(["Durable"])` for owned `InAppOfferToken`s.
   - `GET /api/stories/windows-ticket` (with the print app's reference, see Slice 408), then `GetCustomerCollectionsIdAsync(ticket, <publisher user id>)` for the key.
   - The `X-Purchase-Proof: windows::<key>` header, as in `DinoReader.Core/BookAccess.cs:303-324`.
   - `RequestPurchaseAsync()` for buying, mapped Succeeded/AlreadyPurchased to Bought, NotPurchased to Cancelled, and anything else to Failed.
   - The interface shape is `IBookPurchases` (`Purchases/IBookPurchases.cs:40-66`) without the subscription members.
2. **Printing is its own purchase.** Buying a book in the Dino reader does not unlock printing, and a print purchase does not unlock reading. Each paid book gets its own print product, a Durable add-on of the print app in the Microsoft Store, priced $0.99 at go-live. Free books (`access: free` in the Worker's listing) print with no check. Picture folders print with no check.
3. **No subscription for printing.** None of Dino's subscription code (`SubscriptionProducts`, lapse, trial) is copied.
4. **Payment bypass only in test builds.** Copy Dino's `DINO_TEST_BOOKS` pattern (`Testing/FakePurchases.cs:1`, `DinoReader.App.csproj:161-166`):
   - The bypass is a `PRINT_TEST_BOOKS` compile symbol, on in Debug and in Release only with `-p:PrintTestBooks=true`.
   - With it on, a fake purchase service treats every book as owned and never asks the Worker.
   - A banner reads "Test build: payment is skipped".
   - A normal Release build contains none of this code.
5. **Store errors read like the Dino reader's.** Copy `HttpErrorKind.cs` and `HttpErrorClassifier.cs` from ReadTheStupidText `src/Shared/Sirous.Reading.Application/Net/` into `Core/Net/`. It is copied, not referenced, because that library is net11.0 and has InternalsVisibleTo for RTST only. Map the kinds as in Dino's `StorePage.xaml.cs:325-330`:
   - Offline gives the offline message.
   - 400 gives the server's `{"message"}` sentence, else the server message.
   - 402 gives "You need to buy printing for this book first."
   - 404 gives "This book isn't in the Store any more."
   - 429 gives "Too many tries. Wait a minute, then try again."
   - Everything else (401, 403, 5xx) gives "Something went wrong on the server. Please try again later."

   The messages show only for actions the user starts (ReadTheStupidText Slice 357, Decision 4). The print app has no translations, so the text is English.
6. **The print app goes back into the Microsoft Store.** Durable add-ons and `StoreContext` work only in an MSIX-packaged app associated with a Store listing (unpackaged gives 0x803F6107, `WindowsPurchases.cs:11`). This reverses the 2026-10-22 drop of the submission (Slice 11). The Slice 11 MSIX tooling in `release.yml` and `packaging/msix/` is reused. The GitHub-release exe has no Store context, so it can print free books and picture folders only, and says why on a paid book.

## Items

### A. Errors and the catalog (works with no purchases yet)
- [ ] Copy `HttpErrorKind`/`HttpErrorClassifier` into `Core/Net/` with their tests, and replace the blanket `DinoStoreException` text in `DinoStoreClient.SendAsync` with the classified kind and the Decision 5 messages.
- [ ] `DinoStoreEntry` gains `Access` (free/paid) and `PrintProductId` from the Worker's `/api/stories` listing (fields added by Slice 408). Missing `access` counts as free, as on the Worker (`stories.ts:358`).
- [ ] The store dialog shows "Free" or the print price on each book.
- [ ] The store dialog shows a message, not silence, when a download answers HTTP 402 (found 2026-11-18: 'Goldilocks and the Three Bears' showed no progress bar or result in 60 s; log `D:/print-the-damn-book-worktrees/reviews/s21-store-dialog.log`).
- [ ] The `tools/desktop-test` `store-dialog` check picks a free book (or expects the paid-book message) instead of the first story (same log).

### B. The purchase service (copy of Dino's)
- [ ] Add `App/Purchases/IPrintPurchases.cs` (Decision 1 shape, minus subscriptions) and `WindowsPrintPurchases.cs`, a line-for-line port of `WindowsPurchases.cs` to WPF. The TFM moves to `net10.0-windows10.0.19041.0` for `Windows.Services.Store`; context7 first.
- [ ] Add `Core/Dino/PrintAccess.cs`, ported from `BookAccess.cs` without subscriptions: free/paid, owned print product, and the proof header.
- [ ] `DinoStoreClient` sends `X-Purchase-Proof` only on the print-access and download calls of a paid book, and adds the ticket and print-access URLs to `DinoStoreCatalog.IsAllowedUrl`.

### C. The gate
- [ ] On the Check-to-Print move (`MainViewModel.Wizard.cs:250-251`) and in `PrintViewModel.CanPrint` (`:603`), a paid Dino book must have a print-access `200` from the Worker (Slice 408 route).
- [ ] Without it, the Print step shows the error message and a **Buy printing ($0.99)** button, which calls `RequestPurchaseAsync` and checks again.
- [ ] The reprint-backs command (`PrintViewModel.cs:687`) follows the same gate.
- [ ] A `.dino` opened from disk or handed over by Dino (Slice 19) is matched to the store by `DinoBook.Id` (`Core/Dino/DinoBook.cs:22`). First check that `DinoBook.Id` equals the listing's id; if it doesn't, map them in `PrintAccess`.
- [ ] A book whose id isn't in the listing counts as paid only when the listing can't be read and the book came from the store cache; otherwise free.

### D. Test bypass
- [ ] Add `PRINT_TEST_BOOKS` in the csproj (Decision 4), `App/Purchases/FakePrintPurchases.cs` under `#if PRINT_TEST_BOOKS`, and the banner on the Print step.
- [ ] A test proves the Release build without the switch has no fake type (reflection over the built assembly).

### E. Store and privacy
- [ ] `site/public/privacy.html` and the store dialog footer both say, in the same words: paid books are checked with the Microsoft Store through Dino's server, which sends a store key and nothing about the child (Slice 18 rule: they stay in step).
- [ ] Update the CLAUDE.md Slice 18 rule ("plain GETs, no identifiers") to name the proof header as the one exception.
- [ ] Re-enable the MSIX job in `release.yml` once the `MSIX_*` repo variables are set from the new Partner Center reservation.

## Human steps (the user's Partner Center login)
- Reserve the print app in Partner Center again, or reuse the Slice 11 reservation if it still exists: https://partner.microsoft.com/dashboard/apps-and-games/overview. The app id is not known yet; once it is, this line gets the app's direct link.
- Create one Durable add-on per paid book at $0.99, with the product ids that Slice 408 lists.
- Add the Worker's Entra app under "Product collections and purchases" for the print app (ReadTheStupidText `worker/CLAUDE.md:53`). It can take up to 16 hours to work.

## Verification
- Unit: classifier mapping for each status; `PrintAccess` free/paid/owned; catalog parsing with and without `access`; proof header only on paid calls; allow-list.
- App tests: the gate blocks Print for a paid book without access, allows it with access, and never blocks free books or folders; each error kind shows its message.
- Desktop (VM, `tools/desktop-test/`): a `PrintTestBooks=true` Release build prints a paid book with the banner shown.
- After 408 and the Partner Center steps: a real Store build refuses a paid book with the 402 message, buys printing, then prints (goes to `docs/plan/unverified.md` until run).
