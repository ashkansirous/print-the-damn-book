// The kept browser: a headed Chromium with its own profile outside the repo, human pace, every click through the Publish guard.
import { homedir } from 'node:os';
import { join } from 'node:path';
import { mkdirSync } from 'node:fs';
import { ALLOWED_BUTTONS, assertAllowedControl, assertNotPublish, assertSafeClick } from './guard.mjs';
import { classifyPage, ownerInstruction } from './bookshelf.mjs';

export const log = (msg) => console.log(`[kdp-robot] ${msg}`);

export function profileDir() {
  const base = process.env.LOCALAPPDATA ?? join(homedir(), '.local', 'share');
  return join(base, 'PrintTheDamnBook', 'kdp-browser');
}

export const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
/** Human pace: a short random pause between steps. */
export const pace = () => (process.env.KDP_ROBOT_NO_PACE ? Promise.resolve() : sleep(350 + Math.random() * 650));

export async function openSession() {
  const { chromium } = await import('playwright'); // Playwright docs: /microsoft/playwright, BrowserType.launchPersistentContext
  const dir = profileDir();
  mkdirSync(dir, { recursive: true });
  const context = await chromium.launchPersistentContext(dir, { headless: false, viewport: null, args: ['--start-maximized'] });
  const page = context.pages()[0] ?? (await context.newPage());
  log(`browser open, profile ${dir}`);
  return { context, page, close: () => context.close() };
}

export const OWNER_WAIT_MS = 10 * 60 * 1000;
const MAX_OWNER_WAITS = 3;

export class OwnerNeeded extends Error {
  constructor(reason) {
    super(`The owner has to act in the browser window (${reason}); run stopped. Run "login" in a terminal, then start this command again.`);
    this.name = 'OwnerNeeded';
  }
}

export const OWNER_POLL_MS = 5000;

/** Prints one line for the owner, then polls check() every pollMs until it says 'ok'; no keyboard input. Gives up with OwnerNeeded after timeoutMs (now and wait are injectable). */
export async function waitForOwner(message, { check, timeoutMs = OWNER_WAIT_MS, pollMs = OWNER_POLL_MS, now = Date.now, wait = sleep } = {}) {
  log(message);
  const deadline = now() + timeoutMs;
  while (now() < deadline) {
    await wait(pollMs);
    if ((await check()) === 'ok') return;
  }
  throw new OwnerNeeded(`no sign-in seen within ${Math.round(timeoutMs / 1000)} s`);
}

export class UnknownPage extends Error {
  constructor(url) {
    super(`The browser is on a page the robot does not recognise (${url}); run stopped`);
    this.name = 'UnknownPage';
  }
}

const UNKNOWN_PAGE_TRIES = 8;

async function classifyNow(page) {
  const bodyText = await page.locator('body').innerText().catch(() => '');
  return classifyPage({ url: page.url(), title: await page.title().catch(() => ''), bodyText });
}

/** A page still loading reads as unknown for a few seconds; after that an unrecognised page stops the run. */
async function settledKind(page) {
  let kind = await classifyNow(page);
  for (let i = 1; i < UNKNOWN_PAGE_TRIES && kind === 'unexpected'; i++) {
    await sleep(1000);
    kind = await classifyNow(page);
  }
  return kind;
}

/** A sign-in or captcha waits for the owner (a few times at most) until the page is a KDP page again; any other page stops the run. */
export async function ensureKdpPage(page, { waitOwner = (msg) => waitForOwner(msg, { check: () => classifyNow(page) }), maxWaits = MAX_OWNER_WAITS } = {}) {
  for (let waits = 0; ; waits++) {
    const kind = await settledKind(page);
    if (kind === 'ok') return;
    log(`stopped: page is "${kind}" (${page.url()})`);
    if (kind === 'unexpected') throw new UnknownPage(page.url());
    if (waits >= maxWaits) throw new OwnerNeeded(`the page is still "${kind}" after ${maxWaits} tries`);
    await waitOwner(ownerInstruction(kind));
  }
}

const tryText = async (fn) => {
  try {
    return String((await fn()) ?? '');
  } catch {
    return '';
  }
};

/** The texts the page resolved a locator to: its visible text, aria-label, value and wrapping label. */
async function resolvedTexts(locator, intended) {
  return [
    intended,
    await tryText(() => locator.innerText({ timeout: 2000 })),
    await tryText(() => locator.getAttribute('aria-label', { timeout: 2000 })),
    await tryText(() => locator.getAttribute('value', { timeout: 2000 })),
    await tryText(() => locator.evaluate((el) => el.closest('label')?.innerText ?? '')),
  ];
}

/** Every interaction (click, check, fill, select) goes through here: no path reaches a control whose resolved text looks like Publish or Submit. */
async function assertControl(locator, intended, allowed) {
  const texts = await resolvedTexts(locator, intended);
  assertNotPublish(texts.slice(1), allowed);
  assertAllowedControl(texts, allowed);
}

/** `allowed`: the labels the resolved control may show (strings or RegExps); a control showing none of them is refused. */
export async function safeClick(locator, intended, allowed) {
  await assertControl(locator, intended, allowed);
  log(`click "${intended}"`);
  await locator.click();
  await pace();
}

export async function safeCheck(locator, intended, allowed) {
  await assertControl(locator, intended, allowed);
  log(`check "${intended}"`);
  await locator.check();
  await pace();
}

export async function safeUncheck(locator, intended, allowed) {
  await assertControl(locator, intended, allowed);
  log(`uncheck "${intended}"`);
  await locator.uncheck();
  await pace();
}

/** A text box or select: only its name, aria-label and wrapping label are checked (its value or options are the book's own text). */
export async function guardField(locator, intended, allowed) {
  const label = await tryText(() => locator.evaluate((el) => [...(el.labels ?? [])].map((l) => l.innerText).join(' ') || (el.closest('label')?.innerText ?? '')));
  const texts = [intended, await tryText(() => locator.getAttribute('aria-label', { timeout: 2000 })), label];
  assertNotPublish(texts.slice(1), allowed);
  assertAllowedControl(texts, allowed);
}

export async function clickButton(page, name) {
  assertSafeClick(name);
  await safeClick(page.getByRole('button', { name, exact: true }).first(), name, ALLOWED_BUTTONS);
}

/** A small JPEG of the visible page, for the summary. Returns the file path. */
export async function shot(page, folder, name) {
  mkdirSync(folder, { recursive: true });
  const file = join(folder, `${name}.jpg`);
  await page.screenshot({ path: file, type: 'jpeg', quality: 45, scale: 'css' });
  return file;
}
