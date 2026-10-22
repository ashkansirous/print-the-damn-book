### Slice 10 — Landing site at print-the-damn-book.sirous.uk

*Built 2026-10-22, awaiting merge; not yet verified live (first deploy, custom domain, Lighthouse).*

**Touches:** `site/**`, `.github/workflows/site.yml`

**Why.** The app needs a public home: what it does, how to get it, and the privacy and support pages
that the Microsoft Store listing (Slice 11) has to link to.

**Decisions** (user, 2026-10-22: a landing page, not a web app; the rest are recommended defaults):

- **Static site** in `site/`: plain HTML and CSS on the Nocturne tokens (`docs/design/nocturne/styles.css`,
  copied in), with Phosphor icons. There is no framework and no client JavaScript beyond what a page needs.
  The visuals reuse the Store Kit artwork from Slice 08:
  - hero art (2e) at the top;
  - the three screenshot panels (2f) as "Open, Check, Print";
  - the app icon.
- The pages:
  - `/` with the hero, the three steps, and Download;
  - `/privacy` (the app collects nothing; its settings stay in `%LOCALAPPDATA%`; there is no network access);
  - `/support` (contact email and the known printer notes, such as manual two-sided printing).
- **Hosting:** a Cloudflare Worker with static assets (`wrangler.jsonc` in `site/`) on the custom domain
  `print-the-damn-book.sirous.uk`. The `sirous.uk` zone is already on Cloudflare (nameservers
  `eve`/`vern.ns.cloudflare.com`, checked 2026-10-22).
- **Download** (updated 2026-10-22: the repo is now public): the button links to the latest GitHub release
  asset, `https://github.com/ashkansirous/print-the-damn-book/releases/latest/download/PrintTheDamnBook.exe`,
  and the Microsoft Store badge joins it once Slice 11 is live. No R2 bucket and no `release.yml` change.
- **Deploy:** `site.yml` runs `wrangler deploy` on pushes to `main` that touch `site/**`. The Cloudflare API
  token and account id are repo secrets, created and recorded through the `manage-secrets` skill.

**Work.**

- [x] `site/` pages, styles and assets; responsive down to phone width.
- [x] Worker config and the custom domain.
- [x] `site.yml` deploy workflow. The deploy step skips with a notice until `CLOUDFLARE_API_TOKEN` is set (secrets still to be created).

**Verification.**

- `https://print-the-damn-book.sirous.uk/`, `/privacy` and `/support` load over HTTPS, and Lighthouse reports no
  accessibility errors.
- The Download link resolves to the exe from the latest release (once the first `v*` tag exists).

#### Design (Nocturne + Store Kit mock, sections 2d-2f, added 2026-10-22)

There is no landing-page frame in the mock. The page layout is open; what the design gives is the tokens, the components and the Store Kit art to reuse. The plan agrees with this, apart from the points below.

- **Tokens and components.** Copy `docs/design/nocturne/styles.css` into `site/`. Bg `--color-bg` #161826, text `--color-text` #e9e9ed, accent `--color-accent` #9184d9; Inter for `--font-heading` (500 only, never bolder) and `--font-body`; spacing `--space-*` (density 0.7), radii `--radius-sm/md/lg` 4/8/14; shadows `--shadow-sm/md/lg`. Use the classes `.nav` + `.nav-brand` (header), `.btn-primary` for Download, `.btn-secondary`, `.card` (`.card-kicker`, `.card-title`, `.card-body`), `.tag`, `.table` if needed, and `.lighten` around every photograph. The rules in `nocturne/readme.md`: left-aligned layouts, outlined (not filled) primary buttons, rules that fade over 48 px, focus ring `:focus-visible` 2 px accent, paragraph-size accent text uses `--color-accent-300`, disabled controls at 45% opacity.
- **Hero.** Art 2e (`Store Kit.dc.html` line 102): 960×540 at 2×/4×. It carries no text, so the site puts the headline and Download in HTML. The left third is kept clear in the art, which is where the page text goes. Background of the section: `--color-section` radial, as in the art. The copy line from art 2d: "Picture books, in the right order." (17 px `--color-neutral-400` there).
- **Three steps.** Art 2f (line 114), three 960×540 panels. Their copy is "Step 1" / "Drop a folder. Get a book." / "Name your pages Cover, 1, 2 … Back cover. It works out what goes where on every sheet."; "Step 2" / "See every sheet before you print." / "Front and back, exactly as they come out. Fold the stack and it reads in order."; "Step 3" / "One job. Both sides. Done." / "Print a test sheet once to check the flip edge, then send the whole book."
- **Icon.** `docs/design/icons/app-icon.svg` for the header brand and the favicon (`app-icon-sm.svg` at 16/32 px for the favicon).
- Fonts: bundle Inter locally (the mock links Google Fonts); Phosphor regular/fill as local files or inline SVG.

New strings for both locales: none known; the site is English-only unless Slice 10 adds a second language.

**Open point: Panel names.** Plan: the three screenshot panels read as "Open, Check, Print". Mock: the panels are titled "Drop a folder. Get a book.", "See every sheet before you print." and "One job. Both sides. Done.", and panel 1 shows the Check step, not Open. Default: use the mock's titles and kickers; make the three-step heading on `/` say "Open, Check, Print" in HTML, with the panels beneath it.

**Open point: Art is re-rendered.** Plan: reuse the Store Kit art. Mock: the panels embed the wizard at 0.56 scale. Default: use the PNGs Slice 08 renders, and swap in the real-app captures when Slice 08 re-shoots them; serve them as 1× and 2× variants (the exports are 2×) and compress, so the page stays light.

**Open point: Landing starter template is not copied.** `nocturne/readme.md` points to `templates/landing/` and `components/*.html`; the import did not include them. Default: build the page from `styles.css` and the class table; re-import if the template is wanted.


#### Built (2026-10-22)

`site/` (`wrangler.jsonc`: Worker `print-the-damn-book-site`, assets `./public`, custom domain `print-the-damn-book.sirous.uk`, `workers_dev` and preview URLs off). Pages `/`, `/privacy`, `/support`, `/licenses`, 404. Nocturne `styles.css` plus `site.css`; Inter bundled locally (`OFL.txt`) and a Phosphor sprite (MIT licence); hero art and the three screenshots from Slice 8 as WebP with PNG fallback. `_headers` sets a strict CSP (`default-src 'none'`), nosniff, a referrer policy and `/assets` caching. Contact is `ashkan.sirous@gmail.com` (user's choice). Download links to `releases/latest/download/PrintTheDamnBook.exe`, with an All releases link.

`.github/workflows/site.yml`: a PR check job (local link check and `wrangler@4.152.0 deploy --dry-run`, no secrets); deploy on push to `main` touching `site/**` via `cloudflare/wrangler-action@v4` with `wranglerVersion` 4.152.0.

*Changed while building:* no R2 (the repo is public). The step cards are the screenshots with a "Step N" label (the text is baked into the images; the alt text carries it). A `/licenses` page was added.

*From review:* two passes, 17 findings applied. A visual check at 1280 and 375 px fixed the hero overlap and the mobile stacking.

#### For CLAUDE.md (lifted 2026-10-22)

- The site lives in `site/` (`wrangler.jsonc` and `public/`). `site/public/_headers` sets the CSP to `default-src 'none'`: add any new script, style, font, image or connect source there. Deploy needs the repo secrets `CLOUDFLARE_API_TOKEN` and `CLOUDFLARE_ACCOUNT_ID`. (Slice 10: docs/plan/slices/10-landing-site.md)

#### For README.md (lifted 2026-10-22)

- Website: https://print-the-damn-book.sirous.uk

#### Not yet verified

- First deploy, and the custom domain live over HTTPS.
- `CLOUDFLARE_API_TOKEN` repo secret (a Workers-edit token the user creates in the Cloudflare dashboard).
- Lighthouse run (no accessibility errors).
- Download link, once the first release exists.
