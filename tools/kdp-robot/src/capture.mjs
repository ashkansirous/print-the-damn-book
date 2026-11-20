// `capture`: read-only. Saves the HTML and a small screenshot of the bookshelf and of each create/edit page it can reach
// without saving, to tmp/kdp-robot/capture/<page>.{html,png}. It never fills or saves anything: only navigation, plus
// "Choose categories" (opens a dialog) and "Cancel" through the Publish guard. The page maps are rewritten from these files.
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { clickButton, ensureKdpPage, log, openSession, pace } from './browser.mjs';
import { PublishRefused } from './guard.mjs';
import { readBookshelfRows } from './commands.mjs';
import { parseBookshelfRows } from './bookshelf.mjs';
import { BOOKSHELF_URL, titleUrl } from './pages/bookshelf.mjs';
import { CATEGORY } from './pages/details.mjs';

export const CAPTURE_MAX_WIDTH = 800;
const CAPTURE_MAX_HEIGHT = 1200;

export const captureDir = (root) => join(root, 'tmp', 'kdp-robot', 'capture');

/** The pages to open: the bookshelf, the empty new-title form, and the three pages of one paperback whose status is exactly draft (never an unknown, live or in-review one). */
export function capturePlan(rows) {
  const plan = [{ name: 'bookshelf', url: BOOKSHELF_URL }, { name: 'new-details', url: titleUrl('new', 'details') }];
  const row = rows.find((r) => r.format === 'paperback' && r.state === 'draft');
  if (row) for (const p of ['details', 'content', 'pricing']) plan.push({ name: `title-${p}`, url: titleUrl(row.kdpId, p) });
  return plan;
}

/** HTML of the page, and a PNG of its top-left corner no wider than CAPTURE_MAX_WIDTH. Returns the two file paths. */
export async function capturePage(page, dir, name) {
  mkdirSync(dir, { recursive: true });
  const html = join(dir, `${name}.html`);
  const png = join(dir, `${name}.png`);
  writeFileSync(html, await page.content());
  const size = await page.evaluate(() => ({ w: window.innerWidth, h: window.innerHeight }));
  const clip = { x: 0, y: 0, width: Math.min(size.w, CAPTURE_MAX_WIDTH), height: Math.min(size.h, CAPTURE_MAX_HEIGHT) };
  await page.screenshot({ path: png, type: 'png', clip });
  log(`captured ${name}`);
  return { html, png };
}

/** The categories dialog: opened and closed again, never saved. A failure is returned for the run to report; a Publish refusal is never swallowed. */
async function captureCategories(page, dir, errors) {
  try {
    await clickButton(page, CATEGORY.openButton);
    await pace();
    await capturePage(page, dir, 'title-categories');
    await clickButton(page, 'Cancel');
  } catch (e) {
    if (e instanceof PublishRefused) throw e;
    errors.push(`categories dialog not captured: ${e.message}`);
  }
}

async function capturePlanned(page, dir, item, errors) {
  await page.goto(item.url);
  await ensureKdpPage(page);
  await pace();
  await capturePage(page, dir, item.name);
  if (item.name === 'title-details') await captureCategories(page, dir, errors);
}

export async function runCapture(root) {
  const dir = captureDir(root);
  const s = await openSession();
  try {
    const rows = parseBookshelfRows(await readBookshelfRows(s.page));
    const errors = [];
    for (const item of capturePlan(rows)) await capturePlanned(s.page, dir, item, errors);
    log(`captures are in ${dir}`);
    return errors;
  } finally {
    await s.close();
  }
}
