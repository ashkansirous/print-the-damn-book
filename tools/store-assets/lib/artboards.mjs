/**
 * Artboard markup, copied from docs/design/Store Kit.dc.html (sections 2b to 2f). Each function
 * returns the artboard element only, never the .dv-card wrapper, so the corners stay square.
 */

const ICON = 'icons/app-icon.svg';
const ICON_SM = 'icons/app-icon-sm.svg';
const ACCENT_GRADIENT = 'linear-gradient(180deg, var(--color-accent-900), var(--color-bg))';
const GRID_CENTER = 'display:grid;place-items:center';

function square(size) {
  return `width:${size}px;height:${size}px`;
}

function img(src, style) {
  return `<img src="${src}" alt="" style="${style};display:block;flex:none">`;
}

const pageCard = (src, box, rotate, shadow) =>
  `<div style="position:absolute;left:${box.left}px;top:${box.top}px;width:${box.w}px;height:${box.h}px;` +
  `background:var(--color-neutral-100);${GRID_CENTER};${rotate ? `transform:rotate(${rotate}deg);` : ''}` +
  `box-shadow:var(${shadow})"><img src="${src}" alt="" style="width:100%;display:block"></div>`;

export function icoArtboard(size, source) {
  const picture = img(source, square(size));
  return `<div id="art" style="${square(size)}">${picture}</div>`;
}

function msixTile(w, h, inner, background) {
  const box = `position:relative;width:${w}px;height:${h}px;overflow:hidden;background:${background}`;
  return `<div id="art" style="${box}">${inner}</div>`;
}

function msixLabel(size, left, bottom) {
  const position = `position:absolute;left:${left}px;bottom:${bottom}px;font-size:${size}px`;
  return `<span style="${position}">Print the damn book</span>`;
}

function abs(src, left, top, size) {
  const position = `position:absolute;left:${left}px;top:${top}px;${square(size)};display:block`;
  return `<img src="${src}" alt="" style="${position}">`;
}

function centered(src, size) {
  return `<div style="${GRID_CENTER};width:100%;height:100%">${img(src, square(size))}</div>`;
}

/** The seven MSIX visual assets at 100% scale (design 2c). */
export const MSIX_ASSETS = [
  { name: 'Square44x44Logo', w: 44, h: 44, html: () => msixTile(44, 44, centered(ICON_SM, 36), 'var(--color-bg)') },
  { name: 'StoreLogo', w: 50, h: 50, html: () => msixTile(50, 50, centered(ICON, 50), 'var(--color-bg)') },
  { name: 'Square71x71Logo', w: 71, h: 71, html: () => msixTile(71, 71, centered(ICON, 52), ACCENT_GRADIENT) },
  {
    name: 'Square150x150Logo', w: 150, h: 150,
    html: () => msixTile(150, 150, abs(ICON, 35, 22, 80) + msixLabel(11, 8, 6), ACCENT_GRADIENT),
  },
  {
    name: 'Wide310x150Logo', w: 310, h: 150,
    html: () => msixTile(310, 150, abs(ICON, 115, 22, 80) + msixLabel(11, 8, 6), ACCENT_GRADIENT),
  },
  {
    name: 'Square310x310Logo', w: 310, h: 310,
    html: () => msixTile(310, 310, abs(ICON, 75, 60, 160) + msixLabel(13, 10, 8), ACCENT_GRADIENT),
  },
  {
    name: 'SplashScreen', w: 620, h: 300,
    html: () => msixTile(620, 300, centered(ICON, 150),
      'radial-gradient(60% 80% at 50% 45%, var(--color-accent-900), var(--color-bg))'),
  },
];

const appTile = () =>
  `<div id="art" style="${GRID_CENTER};width:300px;height:300px;` +
  `background:radial-gradient(70% 70% at 50% 40%, var(--color-accent-900), var(--color-bg))">${img(ICON, square(210))}</div>`;

const boxArt = () =>
  `<div id="art" style="position:relative;width:540px;height:540px;` +
  `background:radial-gradient(70% 60% at 50% 36%, var(--color-accent-900), var(--color-bg) 75%)">` +
  `<img src="${ICON}" alt="" style="position:absolute;left:150px;top:72px;${square(240)};display:block">` +
  `<div style="position:absolute;left:0;right:0;top:344px;display:flex;flex-direction:column;align-items:center;gap:10px">` +
  `<span style="font-family:var(--font-heading);font-weight:500;font-size:40px;letter-spacing:-0.02em">Print the damn book</span>` +
  `<span style="font-size:17px;color:var(--color-neutral-400)">Picture books, in the right order.</span></div></div>`;

const poster = () =>
  `<div id="art" style="position:relative;width:360px;height:540px;` +
  `background:linear-gradient(180deg, var(--color-accent-900), var(--color-bg) 60%)">` +
  `<div style="position:absolute;left:28px;top:34px;right:28px;display:flex;flex-direction:column;gap:12px">` +
  `${img(ICON, square(72))}<span style="font-family:var(--font-heading);font-weight:500;font-size:38px;line-height:1.02;` +
  `letter-spacing:-0.02em">Print the<br>damn book</span></div>` +
  pageCard('book/3.jpg', { left: 58, top: 250, w: 138, h: 184 }, -9, '--shadow-md') +
  pageCard('book/6.jpg', { left: 170, top: 236, w: 138, h: 184 }, 7, '--shadow-md') +
  pageCard('book/cover.jpg', { left: 104, top: 270, w: 156, h: 208 }, 0, '--shadow-lg') +
  '</div>';

/** Design 2e wraps the hero cover in a flex box with an overflow-hidden cell, so a taller image cannot spill. */
function heroCover() {
  const box = 'position:absolute;left:490px;top:80px;width:300px;height:400px;display:flex;box-shadow:var(--shadow-lg)';
  const cell = 'flex:1;background:var(--color-neutral-100);display:grid;place-items:center;overflow:hidden';
  return `<div style="${box}"><div style="${cell}"><img src="book/cover.jpg" alt="" style="width:100%;display:block"></div></div>`;
}

const hero = () =>
  `<div id="art" style="position:relative;width:960px;height:540px;` +
  `background:radial-gradient(60% 80% at 70% 50%, var(--color-section) 0%, var(--color-bg) 70%)">` +
  `<div style="position:absolute;left:640px;top:70px;width:2px;height:400px;` +
  `background:linear-gradient(180deg, transparent, var(--color-accent), transparent);box-shadow:0 0 24px var(--color-accent)"></div>` +
  pageCard('book/2.jpg', { left: 400, top: 120, w: 220, h: 300 }, -8, '--shadow-md') +
  pageCard('book/back.jpg', { left: 700, top: 104, w: 220, h: 300 }, 9, '--shadow-md') +
  heroCover() +
  '</div>';

/** Image text for the three screenshots (design 2f). */
const SHOTS = [
  {
    name: 'screenshot-1-check', kicker: 'Step 1', title: 'Drop a folder. Get a book.',
    body: 'Name your pages Cover, 1, 2 … Back cover. It works out what goes where on every sheet.',
    capture: 'app-check-reading-order.png',
  },
  {
    name: 'screenshot-2-as-printed', kicker: 'Step 2', title: 'See every sheet before you print.',
    body: 'Front and back, exactly as they come out. Fold the stack and it reads in order.',
    capture: 'app-check-as-printed.png',
  },
  {
    name: 'screenshot-3-print', kicker: 'Step 3', title: 'One job. Both sides. Done.',
    body: 'Print a test sheet once to check the flip edge, then send the whole book.',
    capture: 'app-print.png',
  },
];

function screenshotCaption(shot) {
  const column = 'position:absolute;left:44px;top:56px;width:250px;display:flex;flex-direction:column;gap:14px';
  const kicker = `<span style="font-size:11px;letter-spacing:.1em;text-transform:uppercase;color:var(--color-accent)">${shot.kicker}</span>`;
  const title =
    `<span style="font-family:var(--font-heading);font-weight:500;font-size:34px;line-height:1.08;letter-spacing:-0.02em">${shot.title}</span>`;
  const body = `<span style="font-size:15px;line-height:1.5;color:var(--color-neutral-400)">${shot.body}</span>`;
  return `<div style="${column}">${kicker}${title}${body}</div>`;
}

/** The real app window (captures/, 1540x1065 px from capture-app.ps1) in the design's 2f frame: 616x426 at left 318, top 60. */
function screenshotWindow(shot) {
  const frame =
    'position:absolute;left:318px;top:60px;width:616px;height:426px;display:block;' +
    'border-radius:8px;box-shadow:var(--shadow-lg)';
  return `<img src="captures/${shot.capture}" alt="" style="${frame}">`;
}

function screenshot(shot) {
  const box =
    'position:relative;width:960px;height:540px;overflow:hidden;' +
    'background:radial-gradient(80% 90% at 85% 30%, var(--color-accent-900), var(--color-bg) 70%)';
  return `<div id="art" style="${box}">${screenshotCaption(shot)}${screenshotWindow(shot)}</div>`;
}

/** Square44x44Logo target-size variants (taskbar, Start list, Alt-Tab): plated on the brand colour, and unplated (transparent). */
export const TARGET_SIZES = [16, 24, 32, 48, 256];
const PLATED_ICON_RATIO = 36 / 44;

export function targetPlatedArtboard(size) {
  const iconSize = Math.round(size * PLATED_ICON_RATIO);
  return msixTile(size, size, centered(size <= 32 ? ICON_SM : ICON, iconSize), 'var(--color-bg)');
}

export function targetUnplatedArtboard(size) {
  return icoArtboard(size, size <= 32 ? ICON_SM : ICON);
}

/** Store images: file name, artboard markup, CSS size and export scale. */
export function storeArtboards() {
  const shots = SHOTS.map((s) => ({ file: `${s.name}-1920x1080.png`, html: screenshot(s), w: 960, h: 540, scale: 2, captures: [s.capture] }));
  const heroHtml = hero();
  return [
    { file: 'app-tile-300.png', html: appTile(), w: 300, h: 300, scale: 1 },
    { file: 'box-art-2160.png', html: boxArt(), w: 540, h: 540, scale: 4 },
    { file: 'poster-1440x2160.png', html: poster(), w: 360, h: 540, scale: 4 },
    { file: 'hero-1920x1080.png', html: heroHtml, w: 960, h: 540, scale: 2 },
    { file: 'hero-3840x2160.png', html: heroHtml, w: 960, h: 540, scale: 4 },
    ...shots,
  ];
}
