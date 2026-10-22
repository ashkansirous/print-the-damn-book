# Design: print-the-damn-book

Copied from Claude Design on 2026-10-22; `Redesign Directions.dc.html` and the mobile files refreshed 2026-10-23. Source project:
https://claude.ai/design/p/71f50eae-822f-45b1-9a5f-287e52c02bdc (project id `71f50eae-822f-45b1-9a5f-287e52c02bdc`).

Read visual values (colors, sizes, spacing, copy) from these files; never guess them, and never call
`DesignSync` for them. To refresh, re-run the import (main thread, `import-claude-design` skill) and
overwrite these copies.

## Files

| File | What it is |
| --- | --- |
| `Redesign Directions.dc.html` | Canvas: iOS and Android frames (4a–4g), the Print-step options and reload dialog (3a–3b), the wizard directions (1a–1d) and today's UI (0a) |
| `MobileWizard.dc.html` | The working phone prototype the 4a–4g frames render: Open → Check → Print → Done for iOS and Android, all copy and state logic in its script |
| `ios-frame.jsx`, `android-frame.jsx` | Generic iOS 26 and Material 3 device frames (bezel, status bar, home indicator); mock chrome only, not app UI |
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
| t4 | iOS and Android (Slices 12–13, .NET MAUI) | 17 |
| 4a | iOS · Open | 21 |
| 4b | iOS · Check, a warned page selected | 25 |
| 4c | iOS · Print, More options open | 29 |
| 4d | iOS · AirPrint sheet, pass 1 (fronts) | 33 |
| 4e | Android · Check, As printed | 37 |
| 4f | Android · system print preview, pass 1 | 41 |
| 4g | Android · reload prompt between passes | 45 |
| t3 | Plan update: More options, manual two-sided and page numbers (Slices 4, 6, 9) | 52 |
| 3a | Print step on the Brother HL-L3220CW (no duplex), More options open | 56 |
| 3b | The reload dialog between the two passes | 60 |
| t1 | Three takes on the Open → Check → Print wizard | 67 |
| 1a | Top stepper | 71 |
| 1b | Left rail, Windows 11 settings-style; the prototype's default | 75 |
| 1c | Focus: progress line, Step 3 of 3 kicker, bigger heading | 79 |
| 1d | The 1b rail on the As printed view | 83 |
| t0 | Today: the current app, recreated from MainWindow.xaml | 91 |
| 0a | Current UI, default WPF styling | 95 |

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

### MobileWizard.dc.html

One component with props `platform` (ios | android, default ios), `scenario` (clean | issues),
`startStep` (0-3), `defaultView` (book | sheets), `overlay` (none | print-sheet | reload) and
`moreOpen`. Copy and state logic live in `renderVals()` (line ~503); the markup above it is the
layout, with `isIOS` / `isAndroid` branches for the native chrome.

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

> Use the claude_design MCP (https://api.anthropic.com/v1/design/mcp, auth via /design-login) to import this project:
> https://claude.ai/design/p/71f50eae-822f-45b1-9a5f-287e52c02bdc?file=Redesign+Directions.dc.html
> Focus on these files (the whole project is readable): `Redesign Directions.dc.html`. Also read these
> files the selection imports: the Nocturne `_ds_bundle.js` and `styles.css`, `android-frame.jsx`,
> `book/1.jpg`, `book/10.jpg`, `book/2.jpg` … `book/5.jpg`, `book/cover.jpg`, `ios-frame.jsx`,
> `support.js`. Implement: `Redesign Directions.dc.html`
