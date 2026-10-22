### Slice 08 — App icon and Store Kit assets

*Built 2026-10-22, awaiting merge. Screenshots and the icon by eye are not yet verified.*

**Touches:** `tools/store-assets/**`, `assets/store/**`, `src/PrintTheDamnBook.App/Assets/**`,
`src/PrintTheDamnBook.App/PrintTheDamnBook.App.csproj`

**Why.** The app has no icon. The Microsoft Store listing (Slice 11) and the website (Slice 10) also
need the logo, tiles, box art, poster, hero art and screenshots that the Store Kit design defines.

**Decisions** (2026-10-22):

- Every asset is **rendered from the design files**, never redrawn. A script in `tools/store-assets/`
  renders `docs/design/icons/*.svg` and the Store Kit artboards (`docs/design/Store Kit.dc.html`,
  sections 2b–2f) with headless Chromium (Playwright), at the export scales the design notes on each
  artboard. It writes the PNGs to `assets/store/` (committed) and the MSIX visual assets to
  `src/PrintTheDamnBook.App/Assets/`.
- `.ico`: sizes 256, 64 and 48 come from `app-icon.svg`, and 32, 24 and 16 from `app-icon-sm.svg` (design 2b). They are packed into
  `app.ico`, which is set as `ApplicationIcon`, so the exe, the title bar and the taskbar all use it.
- MSIX assets at scales 100, 125, 150, 200 and 400 (design 2c): App list 44, Store logo 50, Small 71,
  Medium 150, Wide 310×150, Large 310, Splash 620×300.
- Store images (design 2d and 2e): app tile 300, box art 2160, poster 1440×2160, and hero art at 1920×1080 and
  3840×2160. The Store rules are kept: no transparency, no title on the hero, and the poster title stays out of the bottom third.
- Screenshots (design 2f, 1920×1080) are rendered from the design's framing first. Once Slice 07 is built,
  the inner window is replaced by real captures of the redesigned app, so the Store shows the real UI.
- The design uses `book/*.jpg`, which are the user's sample pages. The script reads them from the
  git-ignored `Book sample/` folder and fails with a clear message if that folder is missing.

**Work.**

- [x] Render script, plus a `README` in `tools/store-assets/` on how to re-run it after a design refresh.
- [x] `app.ico` and `ApplicationIcon`; the MSIX visual assets.
- [x] Store PNGs in `assets/store/`.
- [ ] Re-shoot the screenshots from the real app after Slice 07 (screenshots are rendered from the design framing for now).

**Verification.**

- The script checks the pixel size of every PNG against the list above, and that no Store image has an alpha channel.
- The built exe shows the icon in Explorer, the title bar and the taskbar (light and dark), compared side by side
  with design 2a.

#### Design (Store Kit mock, sections 2a-2f, added 2026-10-22)

Six frames in `docs/design/Store Kit.dc.html` (section ids at lines 23, 54, 66, 79, 102, 114). The sizes, scales and file split agree with the plan; four things do not (open points below). The icon files are `docs/design/icons/app-icon.svg` (48 px and up) and `app-icon-sm.svg` (32 px and below); both use a 256 viewBox, and the artwork is a rounded square (x/y 16, 224 wide, rx 56), so it has a transparent margin. 2a is a comparison board, not an export. In 2a to 2e a "card" is the artboard; the `.dv-card` wrapper (8 px radius, clipped) is canvas chrome, not part of the asset.

- **2a, in the shell (line 23).** Illustration only. Dark taskbar 48 px (#202020) and light taskbar (#eeeeee), each with the `-sm` icon at 24 px in a 40 px button and a 14×3 px running-app mark (#9184d9 dark, #5d5294 light). Title bar row 32 px with the `-sm` icon at 16 px, then the text "Print the damn book" 12 px. Start pin: `-sm` icon at 32 px under 11 px text. Use it as the by-eye check in Verification.
- **2b, .ico ladder (line 54).** Labels 256, 64, 48 use `app-icon.svg`; 32, 24, 16 use `app-icon-sm.svg`. Artboard background `--color-surface`.
- **2c, MSIX at 100% (line 66).** Backgrounds are opaque. Export each at 100, 125, 150, 200 and 400%.
  - App list 44: 44×44 on `--color-bg`, `-sm` glyph at 36.
  - Store logo 50: 50×50 on `--color-bg`, full icon at 50.
  - Small 71: 71×71, gradient 180° `--color-accent-900` to `--color-bg`, full icon at 52.
  - Medium 150: 150×150 on the same gradient; icon 80 at left 35, top 22; "Print the damn book" 11 px at left 8, bottom 6.
  - Wide 310×150: same gradient; icon 80 at left 115, top 22; same text (labelled "Win 10").
  - Large 310: 310×310; icon 160 at left 75, top 60; text 13 px at left 10, bottom 8 (labelled "Win 10").
  - Splash 620×300: `radial-gradient(60% 80% at 50% 45%, --color-accent-900, --color-bg)`, icon 150 centred.
- **2d, Store logos (line 79).** App tile 300×300 (export 1×): `radial-gradient(70% 70% at 50% 40%, --color-accent-900, --color-bg)`, icon 210 centred. Box art 540×540 (export 4× = 2160): `radial-gradient(70% 60% at 50% 36%, --color-accent-900, --color-bg 75%)`; icon 240 at left 150, top 72; title "Print the damn book" in `--font-heading` 40 px / 500, letter-spacing -0.02em, at top 344, centred; tagline "Picture books, in the right order." 17 px `--color-neutral-400`. Poster 360×540 (export 4× = 1440×2160): gradient 180° `--color-accent-900` to `--color-bg` at 60%; icon 72 at left 28, top 34; title "Print the<br>damn book" 38 px / 500, line 1.02, letter-spacing -0.02em, under it; three page cards in the lower two thirds: `book/3.jpg` 138×184 at (58, 250) rotated -9°, `book/6.jpg` 138×184 at (170, 236) rotated 7° (both `--shadow-md`), and `book/cover.jpg` 156×208 at (104, 270), `--shadow-lg`, on top. The title block ends above the bottom third (y 360).
- **2e, hero (line 102).** 960×540, export 2× (1920×1080) and 4× (3840×2160). Background `radial-gradient(60% 80% at 70% 50%, --color-section, --color-bg 70%)`. A fold line at left 640, top 70, 2×400, a vertical accent gradient with a 24 px accent glow. `book/2.jpg` 220×300 at (400, 120) rotated -8°; `book/back.jpg` 220×300 at (700, 104) rotated 9°; `book/cover.jpg` 300×400 at (490, 80), `--shadow-lg`. No text. Left third kept clear.
- **2f, screenshots (line 114).** Three at 960×540, export 2× (1920×1080). Background `radial-gradient(80% 90% at 85% 30%, --color-accent-900, --color-bg 70%)`. Copy column at left 44, top 56, 250 wide, gap 14: kicker 11 px upper-case accent, title 34 px / 1.08 `--font-heading` 500, body 15 px / 1.5 `--color-neutral-400`. The wizard (1b rail, 1100×760) sits at left 318, top 60, scaled 0.56 (about 616×426). The three shots:
  - "Step 1" / "Drop a folder. Get a book." / "Name your pages Cover, 1, 2 … Back cover. It works out what goes where on every sheet." Wizard on step 1 (Check), clean folder, Reading order.
  - "Step 2" / "See every sheet before you print." / "Front and back, exactly as they come out. Fold the stack and it reads in order." Check, As printed.
  - "Step 3" / "One job. Both sides. Done." / "Print a test sheet once to check the flip edge, then send the whole book." Print step.
- Fonts: Inter 400/500/600 for all text in the art (the mock loads it from Google Fonts).

New strings for both locales: none for the app. The art copy above is image text only, not localised.

**Open point: The design files do not render on their own.** Plan: the script renders `Store Kit.dc.html` with headless Chromium. Mock: the file needs `support.js` and `_ds_bundle.js`, loads `_ds/nocturne-…/styles.css`, and draws 2f with `<dc-import name="BookWizard">`. README says `support.js` and `_ds_bundle.js` were not copied, and the stylesheet sits at `docs/design/nocturne/styles.css`. Default: the script builds its own plain HTML page per artboard (the markup is plain inline-styled divs and `<img>`), links `docs/design/nocturne/styles.css` and a local Inter, and does not use `dc-import`. 2f takes the real-capture route from the start for the inner window, or the BookWizard markup pasted into a harness; the plan already says real captures replace it after Slice 07.

**Open point: Corner radius would add alpha.** Mock: every artboard sits in `.dv-card`, which has `border-radius: 8px; overflow: hidden`. Exporting that element gives transparent corners, against "no Store image has an alpha channel". Default: the script screenshots the artboard element itself, not the card, and the alpha check already in Verification stays.

**Open point: Image names.** Mock: `book/cover.jpg`, `book/back.jpg`, `book/2.jpg`, `3.jpg`, `6.jpg`. `Book sample/` holds `Cover.jpg`, `Back cover.jpg`, and `1.jpg` to `10.jpg`. Default: the script maps `cover` to `Cover.jpg` and `back` to `Back cover.jpg`, and the missing-folder error names the files it needs.

**Open point: Non-integer export scales.** Mock says "export at 1×-4×". MSIX wants 125% and 150%. Default: the script uses a device scale factor of 1.25 and 1.5 for those, and rounds the pixel size as MSIX does (for example 44 × 1.25 = 55); the size check in Verification reads the same table.

**Open point: MSIX tiles are opaque.** Mock: 44, 50 and 71 sit on `--color-bg` or the gradient, not transparent. Windows normally draws the unplated 44 variant on its own plate. Default: follow the mock (opaque); add the transparent `altform-unplated` 44 variants only if the taskbar looks wrong in the device check.

*Changed while building:*

- `tools/store-assets` is Node + Playwright with pinned exact deps and a lockfile (`npm ci`, `npx playwright install chromium`, `npm run render`). Plain-HTML artboards per section, no `dc-import`; the wizard template is expanded by `lib/wizard.mjs`.
- Lossless PNG optimisation with `@napi-rs/image` 1.15.0. Verification covers every PNG (exact size, RGB colour type, no alpha) and every `.ico` entry (size, source SVG, transparent corner).
- Outputs: `assets/store/` (tile 300, box art 2160, poster 1440x2160, hero 1920x1080 and 3840x2160, three screenshots 1920x1080), `src/PrintTheDamnBook.App/Assets/app.ico` (256/64/48 from `app-icon.svg`, 32/24/16 from `app-icon-sm.svg`) set as `ApplicationIcon`, and 7 MSIX assets x 5 scales. About 11.4 MiB in total.
- The 125% and 150% scales use device scale = outW/cssW with 0.25 px nudges.
- Sample pages are looked up in the worktree `Book sample/`, the primary checkout, or `BOOK_SAMPLE_DIR`. The user approved publishing the sample book pages in Store art (2026-10-22).

*From review:* two passes, 17 findings applied. Deferred: MSIX `targetsize` / `altform-unplated` variants, to Slice 11.

#### For CLAUDE.md (lifted 2026-10-22)

- Store and MSIX images are generated by `tools/store-assets` from `docs/design`. Re-run `npm run render` there after a design refresh; never hand-edit the PNGs in `assets/store/` or `src/PrintTheDamnBook.App/Assets/`. (Slice 8: docs/plan/slices/08-icon-and-store-assets.md)

#### Not yet verified

- Screenshots re-shot from the real redesigned app (after Slice 7); they are design-framing renders for now.
- The icon by eye in Explorer, the title bar and the taskbar, light and dark (device check running).
