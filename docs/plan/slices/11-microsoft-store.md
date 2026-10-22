### Slice 11 — Microsoft Store: MSIX package, listing and submission

*In progress (wave 2026-10-22b), draft PR.* Needs Slice 08 (the assets) and Slice 10 (the privacy URL).

**Touches:** `packaging/msix/**`, `.github/workflows/release.yml`, `docs/store/**`, `README.md`, `assets/store/**`, `tools/store-assets/**` (re-shoot the screenshots from the real app, carried over from Slice 8)

**Why.** User request, 2026-10-22: register the app in the Microsoft Store. The user chose the full route:
an MSIX package, the listing and the submission. The parts that need the user's own login are guided one
step at a time.

**Decisions** (recommended; confirm against the live Partner Center docs when building):

- **Package:** an MSIX built in `release.yml` from the self-contained publish output, with a hand-written
  `packaging/msix/AppxManifest.xml` and the Windows SDK's `MakeAppx`/`MakePri`. It uses the visual assets
  from Slice 08, the `runFullTrust` capability (a desktop WPF app), and is unsigned for Store upload, since
  the Store signs it. The tag's version maps to the MSIX `Version` (X.Y.Z.0).
- **Identity:** the manifest's `Identity Name`, `Publisher` and `PublisherDisplayName` come from Partner
  Center after the app name is reserved. They are kept as repo variables, not hard-coded.
- **Listing** (`docs/store/listing.md`, the source of truth for the text the user pastes or that the API
  sends): name "Print the damn book", a short and a long description, features, keywords, the category
  (Productivity), the screenshots, logos and hero art from `assets/store/`, the privacy URL
  `https://print-the-damn-book.sirous.uk/privacy` and the support URL. Pricing: free.
- **Account and submission (the user's login):**
  1. create or confirm the Partner Center developer account;
  2. reserve the name;
  3. fill in the age rating questionnaire;
  4. upload the first MSIX and the listing;
  5. submit for certification.
  The `guide-step-by-step` skill guides each step; Claude does every part a tool can, such as building and
  checking the package and writing the listing.
- **Later:** publishing new versions through the Microsoft Store submission API or `msstore` CLI from
  `release.yml`, once the account is linked to an Entra app. That is optional, and not in this slice.

**Work.**

- [ ] `AppxManifest.xml`, the MSIX build step and its artifact on each tag.
- [ ] `docs/store/listing.md` with all listing text, and the asset checklist.
- [ ] Guided Partner Center steps 1–5 with the user.
- [ ] README: a Get-it-from-the-Store link once the app is live.

**Verification.**

- Windows App Cert Kit (WACK) passes on the MSIX locally; the MSIX installs and runs from a sideload test.
- The Store submission passes certification, and the Store page shows the listing text and images.
