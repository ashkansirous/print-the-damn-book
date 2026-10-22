# Microsoft Store listing

Source of truth for the text pasted into Partner Center (or sent by the submission API). Limits are
the Store's: short description 200 characters, keywords 7 terms of 30 characters, features 20 lines of 200.
Slice 11: `docs/plan/slices/11-microsoft-store.md`.

## Product

- **Name:** Print the damn book
- **Tagline** (art and social, not a Store field): Picture books, in the right order.
- **Category:** Productivity
- **Price:** Free, no in-app purchases, no ads
- **Languages:** English (en-US)
- **Platform:** Windows 10 (1809) and Windows 11, desktop, x64
- **Website:** https://print-the-damn-book.sirous.uk
- **Privacy policy URL:** https://print-the-damn-book.sirous.uk/privacy
- **Support URL:** https://print-the-damn-book.sirous.uk/support
- **Copyright:** Ashkan Sirous (use the name registered in Partner Center)

## Short description (Store "short description", at most 200 characters)

Turn a folder of page images into a booklet and print it double-sided in the right order. Fold the stack and the book reads in order.

## Description

Print a home-made picture book without working out the page order yourself.

Give Print the damn book a folder of page images named Cover, 1, 2 ... Back cover. It works out which page goes where on every sheet, shows you each sheet front and back, and sends the whole book to your printer. Fold the stack in the middle and it reads in order.

How it works
- Open the folder with your pages (JPG or PNG).
- Check the order. Drag a page to move it, or add and remove pages. Switch to "As printed" to see every sheet exactly as it comes out of the printer.
- Print. Do a test sheet the first time to check which way your printer flips the paper, then print the whole book in one job.

Built for printers at home
- Two pages per landscape sheet, printed on both sides, A4 or Letter.
- Two-sided printing in one job when your printer supports it. If it doesn't, manual mode prints the fronts, then the backs, and tells you how to reload the paper.
- Fit the whole page or fill and crop, set the margin and the flip edge. Your printer and options are remembered on this PC.
- Blank pages are added before the back cover when the page count doesn't fill a sheet.

Private and offline
- Your pictures stay on your PC. The app only reads your page images, needs no account and makes no network connections.

## Features (one per line, at most 20)

- Folder of images to a saddle-stitch booklet in the right page order
- Preview every sheet, front and back, before you print
- Drag pages to reorder; add or remove pages
- Prints double-sided in one job, or manual two-sided for any printer
- Test sheet to check the flip edge before the whole book
- A4 or Letter, fit or fill, adjustable margin
- Remembers your printer and options on this PC
- Works offline: no account, no network, files never leave your PC

## Keywords (at most 7 terms)

booklet printer
print booklet
duplex printing
picture book
saddle stitch
double-sided print
imposition

## Release notes (first submission)

First release. Open a folder of page images, check the order, and print a double-sided booklet.

## Assets checklist

All files are rendered by `tools/store-assets` from `docs/design/` and live in `assets/store/`. Upload in Partner Center, Store listing, Store logos and screenshots.

| Slot | File | Size |
| --- | --- | --- |
| App tile (1:1 logo) | `assets/store/app-tile-300.png` | 300x300 |
| Box art (1:1 logo) | `assets/store/box-art-2160.png` | 2160x2160 |
| Poster art (2:3) | `assets/store/poster-1440x2160.png` | 1440x2160 |
| Hero / promo art 16:9 | `assets/store/hero-1920x1080.png` and `hero-3840x2160.png` | 1920x1080, 3840x2160 |
| Screenshot 1 (Check, reading order) | `assets/store/screenshot-1-check-1920x1080.png` | 1920x1080 |
| Screenshot 2 (Check, as printed) | `assets/store/screenshot-2-as-printed-1920x1080.png` | 1920x1080 |
| Screenshot 3 (Print) | `assets/store/screenshot-3-print-1920x1080.png` | 1920x1080 |

Screenshot captions (optional, 200 characters each): "Drop a folder. Get a book.", "See every sheet before you print.", "One job. Both sides. Done."

The MSIX tile and logo images (Square44/71/150/310, Wide310x150, StoreLogo, splash) are inside the package, from `src/PrintTheDamnBook.App/Assets/`.

## Age rating (IARC questionnaire)

Answer every question "No" or "None": no violence, sexual content, language, controlled substances or gambling. The app does not share user-generated content, does not let users interact, shares no location, makes no purchases and uses no network. Expected rating: 3+ / Everyone. Category for the questionnaire: Utility, productivity, collaboration or other.

## Package properties for the submission form

- Capabilities: `runFullTrust` (a packaged desktop app); the Store asks for a justification: "Desktop WPF application that prints through the Windows print system."
- Notes for certification: "Open a folder of JPG or PNG pages named Cover, 1, 2 ... Back cover (or any images), then Next, then pick the Microsoft Print to PDF printer and print. No sign-in is needed."

## After the app is live

Add a "Get it from the Microsoft Store" line (with the Store link) to `README.md` next to the Download section. Not before the listing is live.

## Packaging notes and what is not yet verified

- **Settings storage:** settings are a small file in the app's local app data (`%LOCALAPPDATA%\PrintTheDamnBook` for the exe). In the Store (MSIX) version Windows redirects that to the package's private local store, so the exe and the Store version keep separate settings. The listing says only that choices are remembered on this PC.
- **Versions:** the release workflow builds the MSIX only for stable tags (no `-` in the tag) and only when the `MSIX_IDENTITY_NAME`, `MSIX_PUBLISHER` and `MSIX_PUBLISHER_DISPLAY_NAME` repository variables are all set; otherwise it skips the step with a notice. The package version is the tag's `X.Y.Z` plus a fourth part `.0` (the Store requires it to be 0). Tags only ever increase, and the first Store version must be at least the minimum reserved in Partner Center.
- **Not yet run:** the Windows App Cert Kit (WACK) on the MSIX, and a signed sideload install of it. The single-file exe's self-extraction (it uses `%TEMP%`) and printing under the package identity are therefore untested in the packaged app. Run both before the first submission. The MSIX could not be built in the review environment either (no Windows SDK there); CI builds it.
