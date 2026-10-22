# Design: print-the-damn-book

Copied from Claude Design on 2026-10-22. Source project:
https://claude.ai/design/p/71f50eae-822f-45b1-9a5f-287e52c02bdc (project id `71f50eae-822f-45b1-9a5f-287e52c02bdc`).

Read visual values (colors, sizes, spacing, copy) from these files; never guess them, and never call
`DesignSync` for them. To refresh, re-run the import (main thread, `import-claude-design` skill) and
overwrite these copies.

## Files

| File | What it is |
| --- | --- |
| `Redesign Directions.dc.html` | Canvas comparing the wizard directions (1a stepper, 1b left rail, 1c focus, 1d rail on As printed) with today's UI (0a) |
| `BookWizard.dc.html` | The working prototype the directions render: Open → Check → Print → Done, all copy and state logic in its script |
| `Store Kit.dc.html` | App icon in the shell (2a), .ico ladder (2b), MSIX assets (2c), Store logos (2d), hero art (2e), screenshots (2f) |
| `icons/app-icon.svg`, `icons/app-icon-sm.svg` | The app icon (48 px and up) and the simplified glyph (32 px and below) |
| `nocturne/styles.css` | The Nocturne design-system tokens and component classes (dark theme, Inter, accent #9184d9) |
| `nocturne/readme.md` | How Nocturne is meant to be used (outlined primary buttons, fading rules, Phosphor icons) |
| `nocturne/_ds_manifest.json`, `nocturne/_adherence.oxlintrc.json` | Token list and the lint rules the design system enforces |

Not copied: `_ds_bundle.js` and `support.js` (they only render the mock in Claude Design), and
`book/*.jpg`, which are the user's sample pages (the same images as the git-ignored `Book sample/`).

## Sections

### Redesign Directions.dc.html

| Id | Title | Line |
| --- | --- | --- |
| t1 | Three takes on the Open → Check → Print wizard | 18 |
| 1b | Left rail, Windows 11 settings-style; the prototype's default | 25 |
| 1a | Top stepper. The closest to today's single-window layout: steps run across the top, action | 22 |
| 1c | Focus. No step list, just a glowing progress line, a "Step 3 of 3" kicker and a bigger hea | 30 |
| 1d | The 1b rail on the As printed view (front and back of every sheet) | 34 |
| t0 | Today: the current app, recreated from MainWindow.xaml | 42 |
| 0a | Current UI, default WPF styling | 46 |

### Store Kit.dc.html

| Id | Title | Line |
| --- | --- | --- |
| 2a | In the Windows shell: dark and light taskbar at 24px, title bar at 16px, a Start pin at 32 | 24 |
| 2b | The .ico ladder. 32 and smaller use a simplified glyph: two photos, no clip or arrow. | 55 |
| 2c | MSIX package assets at 100% scale. Export at 1×–4× for the scale-125…400 variants. Win 11 | 67 |
| 2d | Store logos. App tile 300 (export 1×), 1:1 box art (540 → export 4× = 2160), 2:3 poster (3 | 80 |
| 2e | 16:9 Super hero art (960×540 → export 2× = 1920×1080, 4× = 3840×2160). No product title (S | 103 |
| 2f | Screenshots (960×540 → export 2× = 1920×1080). Built from the live prototype, so they stay | 115 |

### BookWizard.dc.html

The prototype is one component with props `variant` (stepper | rail | focus, default rail),
`scenario` (clean | issues), `startStep` (0-3) and `defaultView` (book | sheets). Its step copy
(headings, sub-headings, button labels, fold steps, warning notes) lives in `renderVals()` near the end
of the file; the markup above it is the layout.

## The user's import prompts (verbatim)

> Use the claude_design MCP (https://api.anthropic.com/v1/design/mcp, auth via /design-login) to import this project:
> https://claude.ai/design/p/71f50eae-822f-45b1-9a5f-287e52c02bdc?file=Redesign+Directions.dc.html
>
> Focus on these files (the whole project is readable): `Redesign Directions.dc.html`,
> `_ds/nocturne-f068244e-5107-4aa9-b6a1-47e310b20b06/_adherence.oxlintrc.json`, `_ds_bundle.js`,
> `_ds_manifest.json`, `readme.md`, `styles.css`, `BookWizard.dc.html`, `icons/app-icon-sm.svg`,
> `icons/app-icon.svg`, `Store Kit.dc.html`, `support.js`. Also read these files the selection imports:
> `book/1.jpg`, `book/10.jpg`, `book/2.jpg` … `book/5.jpg`, `book/cover.jpg`. Implement: the selected files

> Use the claude_design MCP … to import this project:
> https://claude.ai/design/p/71f50eae-822f-45b1-9a5f-287e52c02bdc?file=Store+Kit.dc.html
> Focus on these files: `Store Kit.dc.html`. Also read these files the selection imports: the Nocturne
> `_ds_bundle.js` and `styles.css`, `book/2.jpg`, `book/3.jpg`, `book/6.jpg`, `book/back.jpg`,
> `book/cover.jpg`, `icons/app-icon-sm.svg`, `icons/app-icon.svg`, `support.js`. Implement: `Store Kit.dc.html`
