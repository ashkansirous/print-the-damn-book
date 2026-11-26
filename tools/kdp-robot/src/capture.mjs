// `capture`: read-only. Saves the HTML and a small screenshot of the bookshelf and of each create/edit page it can reach
// without saving, to tmp/kdp-robot/capture/<page>.{html,png}. It never fills or saves anything: only navigation, plus
// the dialogs, lists and choices in PROBES (opened through the Publish guard, closed with Escape, the page reloaded after). It never
// presses Save, Assign ISBN or Publish. The page maps are rewritten from these files.
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { ensureKdpPage, log, openSession, pace, safeOpen } from './browser.mjs';
import { PublishRefused } from './guard.mjs';
import { readBookshelfRows } from './commands.mjs';
import { parseBookshelfRows } from './bookshelf.mjs';
import { BOOKSHELF_URL, titleUrl } from './pages/bookshelf.mjs';

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

const SETTLE_TIMEOUT_MS = 30 * 1000;

/** KDP's setup pages are a script-built app: right after goto the body is empty (the 2026-11-20 draft-content capture held only the footer). Wait until the page shows form controls; a timeout is not fatal, the capture is then what there is. */
export async function settlePage(page, timeout = SETTLE_TIMEOUT_MS) {
  if (typeof page.waitForFunction !== 'function') return false;
  try {
    await page.waitForFunction(() => document.querySelectorAll('input:not([type=hidden]), select, textarea').length > 0, null, { timeout });
    await page.waitForLoadState?.('networkidle', { timeout: 10 * 1000 }).catch(() => {});
    return true;
  } catch {
    log('capture: no form controls appeared before the timeout; saving the page as it is');
    return false;
  }
}

/** HTML of the page, and a PNG of its top-left corner no wider than CAPTURE_MAX_WIDTH. Returns the two file paths. */
export async function capturePage(page, dir, name) {
  mkdirSync(dir, { recursive: true });
  if (!/bookshelf/.test(name)) await settlePage(page);
  const html = join(dir, `${name}.html`);
  const png = join(dir, `${name}.png`);
  writeFileSync(html, await page.content());
  const size = await page.evaluate(() => ({ w: window.innerWidth, h: window.innerHeight }));
  const clip = { x: 0, y: 0, width: Math.min(size.w, CAPTURE_MAX_WIDTH), height: Math.min(size.h, CAPTURE_MAX_HEIGHT) };
  await page.screenshot({ path: png, type: 'png', clip });
  log(`captured ${name}`);
  return { html, png };
}

/** Dialogs, lists and choices opened (never saved) on a page, one capture each; the page is reloaded before every probe so nothing carries over. `open` is a css selector; `wait` is what must show before the markup is saved. */
const DIALOG = '[role="dialog"], dialog[open], .a-popover-modal';
const AI_YES = 'label:has(input[name="has-ai-content"][value="yes"])';
export const PROBES = {
  'title-details': [
    { name: 'title-categories', label: 'Choose categories', open: '#categories-modal-button', wait: DIALOG },
    { name: 'title-series', label: 'Add series details', open: '[data-test-id="add-series-details-button"]', wait: DIALOG },
  ],
  'title-content': [
    { name: 'title-content-trim', label: 'Trim Size', open: '#select--r9-', wait: '[role="listbox"], [role="option"]' },
    // "Yes" opens Texts, Images and Translations comboboxes (ids select--rf-, --rh-, --rj-: capture 2026-11-20); each is opened in turn.
    // Choosing "Yes" only changes the form on screen; the page is reloaded after the probe and nothing is saved.
    { name: 'title-content-ai', label: 'AI content: Yes', open: AI_YES, wait: '#select--rf-' },
    { name: 'title-content-ai-texts', label: 'AI texts', open: [AI_YES, '#select--rf-'], wait: '[role="listbox"]' },
    { name: 'title-content-ai-images', label: 'AI images', open: [AI_YES, '#select--rh-'], wait: '[role="listbox"]' },
  ],
};

const PROBE_WAIT_MS = 15 * 1000;
const PRICE_INPUT = 'input[id*="price" i], input[name*="price" i]';
const PRICE_WAIT_MS = 45 * 1000;

const REDIRECT_MODAL = '#redirect-modal-message';
const REDIRECT_WAIT_MS = 4 * 1000;

/** What the page flags as wrong (alert boxes, fields marked invalid with their id), one line each. */
async function readFlagged(page) {
  const boxes = (await page.locator('.a-alert-inline-error:visible, .a-form-error:visible, [role="alert"]:visible').allInnerTexts()).map((t) => t.trim()).filter(Boolean);
  const fields = await page.locator('[aria-invalid="true"]').evaluateAll((els) => els.map((el) => `invalid field ${el.id || el.name}`));
  return [...new Set([...boxes, ...fields])];
}

/**
 * KDP covers the Content and Pricing pages with a modal ("We found an issue on an earlier page of your title setup ... Go Back") while
 * the Details page has a problem. The modal text is recorded, "Go Back" (navigation only) is pressed, and the Details page is
 * captured with what it flags. Nothing is saved. Returns true when the modal was there.
 */
export async function handleRedirectModal(page, dir, item, errors) {
  const modal = page.locator(REDIRECT_MODAL);
  try {
    await modal.waitFor({ state: 'visible', timeout: REDIRECT_WAIT_MS });
  } catch {
    return false;
  }
  const text = (await modal.innerText()).trim();
  const goBack = page.getByRole('button', { name: 'Go Back', exact: true });
  await safeOpen(goBack, 'Go Back');
  await settlePage(page);
  const flagged = await readFlagged(page);
  mkdirSync(dir, { recursive: true });
  writeFileSync(join(dir, `${item.name}-issue.txt`), [`${item.name}: "${text}"`, 'The page after Go Back flags:', ...flagged.map((f) => `- ${f}`), ''].join('\n'));
  await capturePage(page, dir, `${item.name}-flagged-details`);
  errors.push(`${item.name}: KDP showed "${text}"; after Go Back the details page flags: ${flagged.join(' | ') || 'nothing visible'}`);
  return true;
}

async function loadItem(page, item, dir, errors) {
  await page.goto(item.url);
  await ensureKdpPage(page);
  await pace();
  await settlePage(page);
  return !(await handleRedirectModal(page, dir, item, errors));
}

/** One probe: open the control through the Publish guard, wait for what it shows, save the markup, Escape. A failure is returned for the run to report; a Publish refusal is never swallowed. */
async function runProbe(page, dir, item, probe, errors) {
  try {
    if (!(await loadItem(page, item, dir, errors))) return;
    for (const sel of [probe.open].flat()) await safeOpen(page.locator(sel).first(), probe.label);
    await page.locator(`:is(${probe.wait}):visible`).first().waitFor({ state: 'visible', timeout: PROBE_WAIT_MS }); // KDP keeps hidden modal templates in the DOM
    await capturePage(page, dir, probe.name);
    await page.keyboard.press('Escape');
  } catch (e) {
    if (e instanceof PublishRefused) throw e;
    errors.push(`${probe.name} not captured: ${String(e.message).split('\n')[0]}`);
  }
}

/** The pricing page builds its price boxes after the page loads: wait for one before the markup is saved. A missing box is reported, the page is still saved. */
async function waitForPrice(page, errors) {
  try {
    await page.waitForSelector(PRICE_INPUT, { state: 'attached', timeout: PRICE_WAIT_MS });
    await pace();
  } catch {
    errors.push('title-pricing: no price input appeared before the timeout');
  }
}

async function capturePlanned(page, dir, item, errors) {
  if (!(await loadItem(page, item, dir, errors))) return;
  if (item.name === 'title-pricing') await waitForPrice(page, errors);
  await capturePage(page, dir, item.name);
  for (const probe of PROBES[item.name] ?? []) await runProbe(page, dir, item, probe, errors);
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
