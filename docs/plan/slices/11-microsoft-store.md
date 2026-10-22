### Slice 11 — Microsoft Store: MSIX package, listing and submission

**Dropped 2026-10-22 (user): the app will not be submitted to the Microsoft Store.** The user already has a Partner Center account but decided against a new Store app. The MSIX build stays in `release.yml`, dormant until the `MSIX_*` repo variables are set.

*Built 2026-10-22 (awaiting merge); Partner Center steps pending with the user. Not yet verified: WACK, signed sideload install, the CI msix job.* Needs Slice 08 (the assets) and Slice 10 (the privacy URL).

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

- [x] `AppxManifest.xml`, the MSIX build step and its artifact on each tag.
- [x] `docs/store/listing.md` with all listing text, and the asset checklist.
- [ ] Guided Partner Center steps 1–5 with the user.
- [ ] README: a Get-it-from-the-Store link once the app is live.

**Verification.**

- Windows App Cert Kit (WACK) passes on the MSIX locally; the MSIX installs and runs from a sideload test.
- The Store submission passes certification, and the Store page shows the listing text and images.

*Built:* `packaging/msix/AppxManifest.xml` (full trust, Windows.Desktop MinVersion 10.0.17763.0, identity and version tokens) and `build-msix.ps1` (stages the exe and an explicit asset list incl. Square44x44Logo targetsize/unplated, runs MakePri and MakeAppx, fails on a missing asset). `release.yml` has a separate `msix` job: stable tags only, and only when repo variables `MSIX_IDENTITY_NAME`, `MSIX_PUBLISHER`, `MSIX_PUBLISHER_DISPLAY_NAME` are all set; version X.Y.Z.0. The release job is never blocked by MSIX, logs skipped/failed, and removes a stale `.msix` on re-run. `docs/store/listing.md` holds name, tagline "Picture books, in the right order.", descriptions, features, keywords, Productivity, free, privacy/support URLs, asset checklist, IARC and certification notes. Store screenshots were re-shot from the real app with `tools/store-assets/capture-app.ps1` (UIA + Win32 messages, 1540x1065, HP Smart Printing for Automatic) and composited into the 2f frame; 53 PNGs verified.

*Changed while building:* the screenshot re-shoot moved here from Slice 8. MSIX version rules are documented in `release.yml` and `listing.md`.

*From review:* 2 passes, 12 findings applied. Deferred to follow-ups (Slice 7 code on main): SnapLayoutHook hover state not cleared without non-client mouse tracking; NocturneDialog CoverOwner sizing/null owner; ClearStatusUnlessBusy not resetting PrintStatusKind; TestSheetSent mid-send check covers only RotateBacks.

*Verified locally:* MSIX built (0.1.0.0), registered unpacked via Add-AppxPackage -Register, launched and ran, then removed.

#### For CLAUDE.md (lifted 2026-10-22)

- Store releases come from stable `vX.Y.Z` tags only (MSIX version X.Y.Z.0; tags only ever increase). (Slice 11: docs/plan/slices/11-microsoft-store.md)
- MSIX identity lives in repo variables `MSIX_*`, never in `AppxManifest.xml`. (Slice 11: docs/plan/slices/11-microsoft-store.md)
- After UI changes, re-run `tools/store-assets` (render + `capture-app.ps1`) so Store screenshots match the app. (Slice 11: docs/plan/slices/11-microsoft-store.md)

#### Not yet verified

- WACK on the MSIX.
- Signed sideload install.
- The `msix` job in CI (needs a stable tag and the three repo variables from Partner Center).
- Partner Center steps with the user: account, name reservation, age rating, upload, submission.
