import { writeFileSync } from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { JobKind } from './jobs.mjs';
import { artboardPage } from './page.mjs';
import { WORK_DIR } from './paths.mjs';
import { PNG } from 'pngjs';
import { readPngHeader } from './png.mjs';

const ART_SELECTOR = '#art';
const INTER_WEIGHTS = [400, 500, 600, 700];
const ICON_FONT_FAMILIES = ['Phosphor', 'Phosphor-Fill'];
/** Device pixels added to the target width when Chromium rounds the scaled artboard to the wrong size. */
const SCALE_NUDGES = [0, 0.25, -0.25];

/** Runs in the page: fails on any broken image or font so a missing file cannot render a blank artboard. */
async function assertAssetsLoaded({ interWeights, iconFamilies }) {
  const loadFaces = (query) => document.fonts.load(query).then((faces) => ({ query, faces }));
  const results = await Promise.all([
    ...interWeights.map((w) => loadFaces(`${w} 16px Inter`)),
    ...iconFamilies.map((f) => loadFaces(`16px "${f}"`)),
  ]);
  const unloaded = results.filter((r) => !r.faces.length || r.faces.some((f) => f.status !== 'loaded'));
  if (unloaded.length) throw new Error(`Fonts not loaded: ${unloaded.map((r) => r.query).join(', ')}`);
  const decodeFailures = [];
  await Promise.all([...document.images].map((i) => i.decode().catch(() => decodeFailures.push(i.getAttribute('src')))));
  if (decodeFailures.length) throw new Error(`Images failed to decode: ${decodeFailures.join(', ')}`);
  const broken = [...document.images].filter((i) => i.naturalWidth === 0 || i.naturalHeight === 0).map((i) => i.getAttribute('src'));
  if (broken.length) throw new Error(`Images failed to load: ${broken.join(', ')}`);
}

async function waitUntilReady(page) {
  await page.evaluate(assertAssetsLoaded, { interWeights: INTER_WEIGHTS, iconFamilies: ICON_FONT_FAMILIES });
}

async function screenshotArtboard(context, job, pagePath) {
  const page = await context.newPage();
  try {
    await page.setViewportSize({ width: Math.ceil(job.cssW), height: Math.ceil(job.cssH) });
    await page.goto(pathToFileURL(pagePath).href, { waitUntil: 'load' });
    await waitUntilReady(page);
    return await page.locator(ART_SELECTOR).screenshot({ omitBackground: job.kind === JobKind.Ico || job.kind === JobKind.Unplated, animations: 'disabled' });
  } finally {
    await page.close();
  }
}

/** A render with a single colour everywhere means icons, images or text did not paint. */
function isFlatColour(png) {
  const { data } = PNG.sync.read(png);
  for (let i = 4; i < data.length; i += 4) {
    if (data[i] !== data[0] || data[i + 1] !== data[1] || data[i + 2] !== data[2] || data[i + 3] !== data[3]) return false;
  }
  return true;
}

function hasExactSize(png, job) {
  const { width, height } = readPngHeader(png);
  return width === job.outW && height === job.outH;
}

/**
 * Screenshots artboards at exactly outW x outH pixels. The device scale factor is outW / cssW
 * (the nearest the rounded MSIX size gets to the true 125% or 150%), nudged by a fraction of a
 * pixel if Chromium rounds the scaled artboard to a different size. One context per scale
 * factor is reused, so fonts and JPEGs are decoded once.
 */
export function createCapturer(browser) {
  const contexts = new Map();

  async function contextFor(scale) {
    if (!contexts.has(scale)) contexts.set(scale, await browser.newContext({ deviceScaleFactor: scale }));
    return contexts.get(scale);
  }

  async function capture(job) {
    const pagePath = path.join(WORK_DIR, `${job.id}.html`);
    writeFileSync(pagePath, artboardPage(job.html));
    for (const nudge of SCALE_NUDGES) {
      const context = await contextFor((job.outW + nudge) / job.cssW);
      const png = await screenshotArtboard(context, job, pagePath);
      if (!hasExactSize(png, job)) continue;
      if (isFlatColour(png)) throw new Error(`${job.id}: render is a single flat colour; assets did not paint`);
      return png;
    }
    throw new Error(`${job.id}: no device scale factor gave exactly ${job.outW}x${job.outH}`);
  }

  async function close() {
    await Promise.all([...contexts.values()].map((context) => context.close()));
  }

  return { capture, close };
}
