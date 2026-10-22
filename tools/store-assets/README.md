# store-assets

Renders the app icon, the MSIX visual assets and the Microsoft Store images from `docs/design/`
with headless Chromium (Playwright). Nothing is redrawn: the artboard markup is copied from
`docs/design/Store Kit.dc.html` (2b-2f), and the screenshots expand the `BookWizard.dc.html`
prototype to static HTML.

## Run

```powershell
cd tools/store-assets
npm ci
npx playwright install chromium   # once
npm run render
```

The script needs the git-ignored sample pages (`Book sample/`: `Cover.jpg`, `Back cover.jpg`,
`1.jpg` to `10.jpg`). It looks in the repo, then in the primary checkout, or set `BOOK_SAMPLE_DIR`.
It stages fonts (Inter, Phosphor from npm), the Nocturne stylesheet, the icons and the sample pages
in `.work/` (ignored), renders, then re-reads every output and fails if a size is wrong or a PNG has
an alpha channel.

## Output

- `src/PrintTheDamnBook.App/Assets/app.ico` (256, 64, 48 from `app-icon.svg`; 32, 24, 16 from `app-icon-sm.svg`)
- `src/PrintTheDamnBook.App/Assets/<Asset>.scale-<100|125|150|200|400>.png` (7 MSIX assets, opaque)
- `assets/store/*.png`: app tile, box art, poster, hero (1920 and 3840), three screenshots (no alpha)

## After a design refresh

Copy the new files into `docs/design/` (import-claude-design), run `npm run render`, and look at the
PNGs. If a design artboard changed, update `lib/artboards.mjs` to match. The screenshots are rendered
from the design's framing; replace them with captures of the real app when you have them.
