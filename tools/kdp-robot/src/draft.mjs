// `draft <book> <lang>`: plan (pure, tested) and the browser run that saves one paperback as a KDP draft. Never publishes.
import { existsSync } from 'node:fs';
import { join, resolve, sep } from 'node:path';
import { checkDefaults, checkEntryShape, findBook, kdpOutFolder } from './catalog.mjs';
import { buildFields, changedOnly, diffFields, fieldHashes, hashValue } from './fields.mjs';
import { looksLikePublish, PublishRefused } from './guard.mjs';
import { readListing } from './repo.mjs';
import { nextState, readState, writeState } from './state.mjs';
import { writeSummary } from './summary.mjs';
import { clickAction, clickButton, ensureKdpPage, guardField, log, openSession, pace, safeCheck, safeClick, safeUncheck, shot, sleep } from './browser.mjs';
import { capturePage, captureDir } from './capture.mjs';
import { BOOKSHELF, BOOKSHELF_URL, CONTROL_CONFIRMED, PAGE as BOOKSHELF_PAGE, kdpIdFromUrl, titleUrl } from './pages/bookshelf.mjs';
import { CATEGORY, CONTRIBUTOR, DETAILS_SPECS, PAGE as DETAILS_PAGE, PRIMARY_AUTHOR, contributorRowSelector, contributorSpecs, keywordSpec, splitName } from './pages/details.mjs';
import { CONTENT, CONTENT_SPECS, PAGE as CONTENT_PAGE } from './pages/content.mjs';
import { PRICING_SPECS, PAGE as PRICING_PAGE, formatPrice } from './pages/pricing.mjs';
import { FieldNotFound, readField, readFields, single, writeField } from './fieldio.mjs';
import { readBookshelfRows } from './commands.mjs';
import { matchRows, parseBookshelfRows } from './bookshelf.mjs';
import {
  assertEditable, baselineOf, fileHash, hasIsbn, isbnPlan, judgeFileCheck, judgeSave, keepKdpId, liveCategories, liveTrim, needsUpload,
  requireConfirmedPages, requireKdpId, singleRow, StoppedAtUnconfirmedPage, stateForTitle, waitForVerdict,
} from './checks.mjs';

/** Every page map `draft` needs; each carries `confirmed: false` until it was checked against a capture. */
export const NEEDED_PAGES = [BOOKSHELF_PAGE, DETAILS_PAGE, CONTENT_PAGE, PRICING_PAGE];
/** The maps checked before a browser opens; the rest of NEEDED_PAGES is checked by `runSteps` when the run reaches each page. */
export const START_PAGES = [BOOKSHELF_PAGE];

const SAVE_TIMEOUT_MS = 60 * 1000;
const ID_TIMEOUT_MS = 10 * 1000;
const CREATE_ID_WAIT_MS = 5 * 1000;

/** Everything a run needs, with the problems found before any browser opens. */
export function planDraft(root, catalog, book, lang) {
  const entry = findBook(catalog, book);
  const errors = [];
  if (!entry) return { errors: [`book "${book}" is not in kdp/catalog.json`] };
  if (!entry.languages.includes(lang)) return { errors: [`"${book}" is not set to publish in "${lang}" in kdp/catalog.json`] };
  // The catalog checks that need no other book's folders: the price and finish, and this book's shape. Any error stops the run.
  const catalogErrors = [...checkDefaults(catalog), ...checkEntryShape(entry, { bookIds: new Set([entry.id]) }), ...primaryAuthorErrors(entry)];
  if (catalogErrors.length) return { errors: catalogErrors };
  const listing = readListing(root, book, lang);
  const folder = join(root, kdpOutFolder(entry, lang));
  const files = { interior: join(folder, 'interior.pdf'), cover: join(folder, 'cover.pdf') };
  const outRoot = resolve(root, 'tmp', 'kdp-out') + sep;
  const outside = Object.values(files).filter((f) => !resolve(f).startsWith(outRoot));
  if (outside.length) return { errors: outside.map((f) => `${f} is outside tmp/kdp-out; the upload is refused`) };
  const missing = Object.values(files).filter((f) => !existsSync(f));
  for (const f of missing) errors.push(`missing ${f}: run the kdp-build skill first`);
  const priced = { ...entry, price: catalog.price, finish: catalog.finish }; // one price and one finish for every book
  return { entry, listing, files, fields: buildFields(listing, priced), folder, errors };
}

/** KDP's "Primary Author" boxes take the first contributor, so it must be an Author; checked here so a dry run and the plan see it, not mid-run. */
export function primaryAuthorErrors(entry) {
  const role = entry.contributors?.[0]?.role;
  if (typeof role === 'string' && role.toLowerCase() === 'author') return [];
  return [`the first contributor of "${entry.id}" must be an Author (KDP's primary author), not ${role ?? 'missing'}`];
}

const uploadsOf = (files) => [{ kind: 'interior.pdf (manuscript)', path: files.interior }, { kind: 'cover.pdf', path: files.cover }];

const indexOf = (field) => Number(field.key.split('-')[1]) - 1;
const isKind = (prefix) => (f) => f.key.startsWith(prefix);

/** The first contributor must be an Author: KDP's "Primary Author" boxes. The others fill the rows below it, row 0 first. */
function contributorTarget(field) {
  const i = indexOf(field);
  if (i === 0) {
    if (field.role.toLowerCase() !== 'author') throw new Error(`the first contributor must be an Author (KDP's primary author), not ${field.role}`);
    return { specs: PRIMARY_AUTHOR, row: null };
  }
  return { specs: contributorSpecs(i - 1), row: i - 1 };
}

async function readContributor(page, field) {
  const { specs, row } = contributorTarget(field);
  try {
    const name = `${await readField(page, 'first', specs.first)} ${await readField(page, 'last', specs.last)}`;
    if (row === null) return name;
    const role = await readField(page, 'role', specs.role);
    return role.toLowerCase() === field.role.toLowerCase() ? name : `${name} [role: ${role}]`;
  } catch (e) {
    if (e instanceof FieldNotFound) return undefined;
    throw e;
  }
}

/** Every details field the robot sets is read from the live page; nothing is taken from kdp/state. */
async function readDetailsLive(page, fields) {
  const live = await readFields(page, fields.map((f) => f.key), DETAILS_SPECS);
  const keywords = fields.filter(isKind('keyword-'));
  Object.assign(live, await readFields(page, keywords.map((f) => f.key), Object.fromEntries(keywords.map((f) => [f.key, keywordSpec(indexOf(f))]))));
  for (const f of fields.filter(isKind('contributor-'))) {
    const v = await readContributor(page, f);
    if (v !== undefined) live[f.key] = v;
  }
  Object.assign(live, await readCategoriesLive(page, fields));
  return live;
}

async function readCategoriesLive(page, fields) {
  const selected = await page.locator(CATEGORY.selected).allInnerTexts();
  return liveCategories(selected.map((t) => t.trim()).filter(Boolean), fields.filter(isKind('category-')));
}

/** After the save, a category counts as set only when the page shows it; the rest is marked so state never records it. */
async function verifyCategories(page, todo, ctx) {
  const wanted = todo.filter(isKind('category-'));
  if (!wanted.length) return;
  const live = await readCategoriesLive(page, wanted);
  for (const f of wanted.filter((x) => live[x.key] !== x.value)) {
    f.verified = false;
    collectMessages(ctx, [`${f.label} "${f.value}" is not shown as set after the save; not recorded in kdp/state`]);
  }
}

/** KDP shows one contributor row at first; a missing row is added with the guarded "Add another" button. */
async function ensureContributorRow(page, row) {
  for (let attempt = 0; attempt <= row; attempt++) {
    if ((await page.locator(contributorRowSelector(row)).count()) > 0) return;
    await clickButton(page, CONTRIBUTOR.addButton);
  }
  throw new FieldNotFound(`contributor row #${row + 1}`, await page.locator(contributorRowSelector(row)).count());
}

async function fillContributor(page, field) {
  const { specs, row } = contributorTarget(field);
  if (row !== null) await ensureContributorRow(page, row);
  const { first, last } = splitName(field.value);
  await writeField(page, `${field.key} first name`, specs.first, first);
  await writeField(page, `${field.key} last name`, specs.last, last);
  if (row !== null) await writeField(page, `${field.key} role`, specs.role, field.role);
}

/** One category: each branch of the path by exact text, then exactly one checkbox with the exact leaf name, ticked. */
async function pickCategory(dialog, path) {
  const name = path.join(' > ');
  for (const part of path.slice(0, -1)) await safeClick(await single(dialog.getByText(part, { exact: true }), `category ${name}: "${part}"`), part, [part]);
  const leaf = await single(dialog.getByRole('checkbox', { name: path.at(-1), exact: true }), `category ${name}`);
  await safeCheck(leaf, path.at(-1), [path.at(-1)]);
  if (!(await leaf.isChecked())) throw new FieldNotFound(`category ${name} (not ticked after the click)`, 0);
}

const TICKED_BOXES = '[role="checkbox"][aria-checked="true"], input[type="checkbox"]:checked';

async function boxName(box) {
  const label = await box.getAttribute('aria-label').catch(() => null);
  return (label ?? (await box.evaluate((el) => el.closest('label')?.innerText ?? el.parentElement?.innerText ?? '').catch(() => ''))).trim();
}

/**
 * A ticked category the catalog does not list is unticked first (through the guard), so the old pick never stays beside the new one.
 * The labels kept come from the catalog only. A page-named box with Publish or Submit text stops the run; the page's own name is
 * never an exemption (guard `refusal`).
 */
export async function untickUnlisted(dialog, categories) {
  const listed = new Set(categories.map((p) => p.at(-1)));
  for (const box of await dialog.locator(TICKED_BOXES).all()) {
    const name = await boxName(box);
    if (listed.has(name)) continue;
    if (looksLikePublish(name)) throw new PublishRefused(name.slice(0, 80), 'a ticked box on the page looks like a Publish or Submit control');
    await safeUncheck(box, 'category', [name]);
  }
}

export async function chooseCategories(page, categories) {
  await clickButton(page, CATEGORY.openButton);
  const dialog = await single(page.getByRole(CATEGORY.dialog), 'category dialog');
  await untickUnlisted(dialog, categories);
  for (const path of categories) await pickCategory(dialog, path);
  await clickButton(page, CATEGORY.saveButton);
}

async function applyDetail(page, field) {
  if (field.key.startsWith('contributor-')) return fillContributor(page, field);
  if (field.key.startsWith('keyword-')) return writeField(page, field.key, keywordSpec(indexOf(field)), field.value);
  return writeField(page, field.key, DETAILS_SPECS[field.key], field.value);
}

async function readPage(page) {
  const alerts = (await page.locator(CONTENT.messageBoxes).allInnerTexts()).map((t) => t.trim()).filter(Boolean);
  return { alerts, bodyText: await page.locator('body').innerText().catch(() => '') };
}

/** KDP's own alert texts go into the summary once each. */
function collectMessages(ctx, alerts) {
  for (const a of alerts) if (!ctx.messages.includes(a)) ctx.messages.push(a);
}

/** Save as Draft counts only when KDP shows a saved marker; an error alert or no marker stops the run with KDP's words. */
export async function savePage(page, pageName, ctx, diff = null) {
  const baseline = baselineOf(judgeSave, await readPage(page)); // a marker already on the page never counts as this save
  await clickButton(page, 'Save as Draft');
  await waitForVerdict({
    what: `the ${pageName} page save`, judge: judgeSave, read: () => readPage(page), timeoutMs: ctx.saveTimeoutMs ?? SAVE_TIMEOUT_MS,
    pollMs: ctx.pollMs, baseline, onMessages: (a) => collectMessages(ctx, a),
  });
  await ensureKdpPage(page);
  await noteKdpId(page, ctx, ctx.idTimeoutMs ?? ID_TIMEOUT_MS);
  ctx.screens.push(await shot(page, join(ctx.dir, 'shots'), pageName));
  if (!ctx.kdpId) await recordUnnumberedSave(page, ctx, diff);
  requireKdpId(ctx.kdpId);
}

/**
 * The save worked, so the title now exists on KDP: with no id in the URL, kdp/state marks it (and the fields just saved) so the
 * next run never creates a second one; then the id is looked up on the bookshelf row of this title (`ctx.findShelfId`).
 */
async function recordUnnumberedSave(page, ctx, diff) {
  ctx.record?.({ savedWithoutId: true, ...(diff ? { fields: recordableFields(diff) } : {}) });
  const id = ctx.findShelfId ? await ctx.findShelfId(page) : null;
  if (!id) return;
  ctx.kdpId = id;
  ctx.record?.({ kdpId: id });
  log(`KDP title id ${id} (from the bookshelf row)`);
}

/** The id has one owner and is set once: read from the URL (KDP redirects after a save, so it may take a moment) while none is known; a different id later is an IdConflict. */
export async function noteKdpId(page, ctx, waitMs) {
  const started = Date.now();
  let id = kdpIdFromUrl(page.url());
  while (!id && !ctx.kdpId && Date.now() - started < waitMs) {
    await sleep(ctx.pollMs ?? 500);
    id = kdpIdFromUrl(page.url());
  }
  const keep = keepKdpId(ctx.kdpId, id);
  if (keep === ctx.kdpId) return;
  ctx.kdpId = keep;
  ctx.record?.({ kdpId: keep });
  log(`KDP title id ${keep}`);
}

/**
 * Capture 2026-11-20: the new-details page (.../paperback/new/details) has no id in its URL; the Create page was captured the same
 * day (a "Create paperback" button) but the page after that click was not, so the first live run captures it. KDP may give the id
 * only after the first Save as Draft (`savePage` records it and stops the run when none shows). An id that does show now is
 * recorded at once. A title saved by an earlier run without an id is never followed by a second Create.
 */
export async function createTitle(page, ctx) {
  if (ctx.savedWithoutId) throw new Error('an earlier run saved a new title on KDP without reading its id, and the bookshelf shows no single row matching it; no second title is created. Look at the bookshelf, then put its id in kdp/state as kdpId (or remove savedWithoutId if there is no such title)');
  log('create: no KDP id yet, creating a new paperback');
  await page.goto(BOOKSHELF_URL);
  await ensureKdpPage(page);
  await clickAction(page, BOOKSHELF.createButton);
  await clickAction(page, BOOKSHELF.paperbackChoice);
  await ensureKdpPage(page);
  await noteKdpId(page, ctx, ctx.createIdWaitMs ?? CREATE_ID_WAIT_MS);
  if (!ctx.kdpId) log('create: the new title has no id yet; it comes with the first Save as Draft');
}

/** Reads the bookshelf and finds the one row matching this title (title + language + paperback); two matching rows is Ambiguous. */
async function shelfRowOf(page, plan, { book, lang }) {
  const raw = await readBookshelfRows(page);
  const rows = parseBookshelfRows(raw);
  const title = plan.fields.find((f) => f.key === 'title')?.value;
  const { matched } = matchRows(rows, { books: [{ id: book, languages: [lang] }] }, () => title);
  return { raw, rows, row: singleRow(matched, `${book} ${lang}`) };
}

/** The one bookshelf row of this title (title + language + paperback) fixes the id; its state must be draft or unknown. */
export async function settleTitle(page, plan, ctx, { book, lang }) {
  const { raw, rows, row } = await shelfRowOf(page, plan, { book, lang });
  if (!row && !ctx.kdpId && !CONTROL_CONFIRMED.titleLinks && raw.length > 0) {
    throw new Error(`the bookshelf shows ${raw.length} title link(s) but none is this title, and kdp/state has no id for ${book} ${lang}. The bookshelf title-link selector is not confirmed against a non-empty shelf yet, so the robot cannot tell a missing title from a selector miss and creates nothing. Find this title on the bookshelf and put its id in kdp/state as kdpId, or empty the bookshelf`);
  }
  const before = ctx.kdpId;
  ctx.kdpId = keepKdpId(ctx.kdpId, row?.kdpId ?? null);
  if (ctx.kdpId) assertEditable(stateForTitle(rows, ctx.kdpId));
  if (ctx.kdpId !== before) ctx.record?.({ kdpId: ctx.kdpId });
}

async function runDetails(page, plan, state, ctx) {
  const fields = plan.fields.filter((f) => f.page === 'details');
  const diff = diffFields(fields, await readDetailsLive(page, fields));
  const todo = changedOnly(diff);
  log(`details: ${todo.length} of ${diff.length} fields to set`);
  for (const f of todo.filter((x) => !x.key.startsWith('category-'))) {
    await applyDetail(page, f);
    await pace();
  }
  const setCategories = categoriesAllowed(todo, ctx);
  if (setCategories) await chooseCategories(page, plan.listing.categories);
  await savePage(page, 'details', ctx, diff);
  if (setCategories) await verifyCategories(page, todo, ctx);
  return diff;
}

/**
 * Categories are set only when their dialog map is confirmed and the title already has an id (the "Choose categories" button is
 * disabled on a new, unsaved title: capture 2026-11-20). Otherwise they are skipped, marked unset so kdp/state never records them,
 * and the summary says so.
 */
export function categoriesAllowed(todo, ctx) {
  const wanted = todo.filter(isKind('category-'));
  if (!wanted.length) return false;
  if (CATEGORY.confirmed && ctx.kdpId) return true;
  for (const f of wanted) f.verified = false;
  collectMessages(ctx, [CATEGORY.confirmed ? 'not set: categories wait for the first save of the title (the button is disabled before it)' : 'not set: categories dialog not captured']);
  return false;
}

async function uploadFile(page, input, path, ctx, kind) {
  log(`upload ${kind}: ${path}`);
  const loc = page.locator(input);
  if ((await loc.count()) !== 1) throw new FieldNotFound(`${kind} file input`, await loc.count());
  const before = await readPage(page);
  const baseline = { ...judgeFileCheck(kind, before), key: before.alerts.join('|') };
  await guardField(loc, `${kind} file input`, [CONTENT.uploadLabels[kind]]);
  await loc.setInputFiles(path); // Playwright docs: /microsoft/playwright, Locator.setInputFiles
  await waitForVerdict({
    what: `the ${kind} file check`, judge: (p) => judgeFileCheck(kind, p), read: () => readPage(page), timeoutMs: ctx.fileCheckTimeoutMs ?? CONTENT.fileCheckTimeoutMs,
    pollMs: ctx.pollMs ?? 5000, baseline, onMessages: (a) => collectMessages(ctx, a), onWait: () => log('waiting for KDP file check...'),
  });
  log(`${kind} file check passed`);
}

/** Picks "upload a cover you already have" when KDP shows it. A choice KDP does not show is already set; any other error surfaces. */
async function chooseCoverOption(page) {
  const choice = page.getByText(CONTENT.coverChoice);
  if ((await choice.count()) === 0) return log('cover choice not shown (already set)');
  await safeClick(await single(choice, 'cover choice'), 'cover choice', [CONTENT.coverChoice]);
}

/** The confirm dialog of the assignment, when KDP shows one within a few seconds; its button goes through the guard like every click. */
async function confirmIsbn(page, ctx) {
  const confirm = page.getByRole('button', { name: CONTENT.isbnConfirmButton, exact: true });
  const until = Date.now() + (ctx.isbnConfirmWaitMs ?? CONTENT.isbnConfirmWaitMs);
  while ((await confirm.count()) === 0) {
    if (Date.now() >= until) return;
    await sleep(ctx.pollMs ?? 250);
  }
  await clickButton(page, CONTENT.isbnConfirmButton);
}

async function waitForIsbn(page, ctx) {
  const started = Date.now();
  while (!hasIsbn((await readPage(page)).bodyText)) {
    if (Date.now() - started > (ctx.isbnTimeoutMs ?? CONTENT.isbnTimeoutMs)) throw new Error('no 978/979 ISBN shows after "Assign me a KDP ISBN" and its confirm step; nothing was recorded. Look at the title on KDP before running draft again');
    await pace();
  }
}

/** The free-ISBN assignment is irreversible. It is recorded in kdp/state only after a 978/979 number was read back from the page. */
export async function assignIsbn(page, ctx) {
  await clickButton(page, CONTENT.isbnButton);
  await confirmIsbn(page, ctx);
  await waitForIsbn(page, ctx);
  ctx.record?.({ fields: { isbn: hashValue('Free KDP ISBN') } });
}

async function chooseTrimSize(page) {
  await safeClick(page.getByRole('button', { name: CONTENT.trimSizeButton }).first(), 'trim size', [CONTENT.trimSizeButton]);
  await safeClick(page.getByText(CONTENT.trimOption).first(), 'trim size option', [CONTENT.trimOption]);
}

/** 'assign' | 'skip'. The ISBN plan is asked only when the isbn row is not 'same', so an ISBN text the regex misses never blocks a price-only run. */
export function decideIsbn(diff, body, state) {
  if (diff.find((f) => f.key === 'isbn')?.status === 'same') return 'skip';
  return isbnPlan({ liveHas: hasIsbn(body), stateHas: Boolean(state?.fields?.isbn) });
}

/** What kdp/state may record from a run's diff: never the isbn (only assignIsbn records it) and never a field not shown as set. */
export const recordableFields = (diff) => fieldHashes(diff.filter((f) => f.key !== 'isbn' && f.verified !== false));

async function applyContentField(page, f, isbn, ctx) {
  if (f.key === 'isbn') {
    if (isbn === 'assign') await assignIsbn(page, ctx);
  } else if (f.key === 'trim') await chooseTrimSize(page);
  else await writeField(page, f.key, CONTENT_SPECS[f.key], f.value);
  await pace();
}

async function runContent(page, plan, state, ctx) {
  const fields = plan.fields.filter((f) => f.page === 'content');
  const specFields = fields.filter((f) => CONTENT_SPECS[f.key] && !CONTENT_SPECS[f.key].readOnly);
  const body = (await readPage(page)).bodyText;
  const live = { ...(await readFields(page, specFields.map((f) => f.key), CONTENT_SPECS)), isbn: hasIsbn(body) ? 'Free KDP ISBN' : undefined, trim: liveTrim(body) };
  const diff = diffFields(fields, live);
  const isbn = decideIsbn(diff, body, state);
  for (const f of changedOnly(diff)) await applyContentField(page, f, isbn, ctx);
  const uploaded = await uploadFiles(page, plan, state, ctx);
  await savePage(page, 'content', ctx);
  ctx.record?.({ files: uploaded });
  return diff;
}

/** Each PDF is uploaded only when its hash differs from the one kdp/state recorded after the last good upload. */
export async function uploadFiles(page, plan, state, ctx) {
  const uploaded = {};
  for (const [kind, input, path] of [['manuscript', CONTENT.manuscriptInput, plan.files.interior], ['cover', CONTENT.coverInput, plan.files.cover]]) {
    const hash = fileHash(path);
    if (!needsUpload(state, kind, hash)) {
      log(`${kind}: same file as the last upload, not uploaded again`);
      continue;
    }
    if (kind === 'cover') await chooseCoverOption(page);
    await uploadFile(page, input, path, ctx, kind);
    uploaded[kind] = hash;
  }
  return uploaded;
}

async function runPricing(page, plan, state, ctx) {
  const fields = plan.fields.filter((f) => f.page === 'pricing');
  const live = await readFields(page, fields.map((f) => f.key), PRICING_SPECS);
  const diff = diffFields(fields, live);
  for (const f of changedOnly(diff)) {
    await writeField(page, f.key, PRICING_SPECS[f.key], f.key === 'price-us' ? formatPrice(f.value) : f.value);
    await pace();
  }
  await savePage(page, 'pricing', ctx);
  return diff;
}

/** The title's state comes from its bookshelf row, never from words on the pricing page. */
async function liveState(page, ctx) {
  ctx.state = stateForTitle(parseBookshelfRows(await readBookshelfRows(page)), ctx.kdpId);
}

export async function runDraft(root, catalog, book, lang, { dryRun = false, pages = START_PAGES } = {}) {
  // Only the bookshelf map is needed to start; each later page is checked when the run reaches it (`runSteps`).
  if (!dryRun) requireConfirmedPages(pages);
  const plan = planDraft(root, catalog, book, lang);
  const run = { book, lang, when: new Date().toISOString(), dryRun, kdpId: null, state: null, diff: [], uploads: [], messages: [], screenshots: [] };
  const blocking = (plan.errors ?? []).filter((e) => !dryRun || !e.startsWith('missing '));
  if (blocking.length) throw new Error(blocking.join('\n'));
  const state = readState(root, book, lang);
  run.kdpId = state?.kdpId ?? null;
  run.uploads = uploadsOf(plan.files);
  if (dryRun) {
    run.diff = diffFields(plan.fields, {});
    run.messages = plan.errors.map((e) => `dry run: ${e}`);
    return { summary: writeSummary(root, run), run };
  }
  return browserRun(root, plan, state, run);
}

async function browserRun(root, plan, state, run) {
  const s = await openSession();
  const ctx = makeContext(root, run);
  let cleanupError = null;
  try {
    await runPages(s.page, plan, state, ctx, run);
  } catch (e) {
    await recordStopped(s.page, ctx, run, e);
    throw e;
  } finally {
    cleanupError = await cleanup(root, s, ctx, run);
  }
  if (cleanupError) throw cleanupError;
  return { summary: run.summaryFile, run };
}

function makeContext(root, run) {
  const ctx = { root, kdpId: run.kdpId, screens: run.screenshots, messages: run.messages, dir: join(root, 'tmp', 'kdp-robot', `${run.book}-${run.lang}`), state: 'unknown' };
  // Written the moment a fact is known (title id, ISBN assigned, file uploaded), so a later stop never loses it.
  ctx.record = (patch) => writeState(root, run.book, run.lang, nextState(readState(root, run.book, run.lang), { kdpId: ctx.kdpId, ...patch }));
  return ctx;
}

async function runPages(page, plan, state, ctx, run) {
  ctx.savedWithoutId = Boolean(state?.savedWithoutId);
  ctx.findShelfId = async (p) => (await shelfRowOf(p, plan, run)).row?.kdpId ?? null;
  await settleTitle(page, plan, ctx, run);
  if (ctx.kdpId) {
    await page.goto(titleUrl(ctx.kdpId, 'details'));
    await ensureKdpPage(page);
  } else await createTitle(page, ctx);
  await runSteps(page, plan, state, ctx, run);
  await liveState(page, ctx);
}

export const DRAFT_STEPS = [
  { page: DETAILS_PAGE, run: runDetails },
  { page: CONTENT_PAGE, run: runContent },
  { page: PRICING_PAGE, run: runPricing },
];

/**
 * The pages in order. Each page whose map is confirmed is filled and saved; at the first unconfirmed one nothing more is saved:
 * the page is captured (read-only) and the run stops, telling the owner to confirm that map. The page after "details" only exists
 * once the draft is saved, so its capture can only be made here.
 */
export async function runSteps(page, plan, state, ctx, run, { steps = DRAFT_STEPS, capture = capturePage } = {}) {
  for (const [i, step] of steps.entries()) {
    if (i > 0) {
      await page.goto(titleUrl(ctx.kdpId, step.page.name));
      await ensureKdpPage(page);
    }
    if (!step.page.confirmed) {
      const { html } = await capture(page, captureDir(ctx.root), `draft-${step.page.name}`);
      throw new StoppedAtUnconfirmedPage(step.page.name, html);
    }
    run.diff.push(...(await step.run(page, plan, state, ctx)));
  }
}

async function recordStopped(page, ctx, run, e) {
  log(`stopped: ${e.message}`);
  run.messages.push(`STOPPED: ${e.message}`);
  run.screenshots.push(await shot(page, join(ctx.dir, 'shots'), 'stopped').catch(() => '(screenshot failed)'));
}

/** Only fields whose page was saved are recorded; a run that stopped midway never claims the rest. */
function writeFinalState(root, ctx, run) {
  const patch = { kdpId: ctx.kdpId, fields: recordableFields(run.diff), ...(ctx.state === 'unknown' ? {} : { state: ctx.state }) };
  if (ctx.kdpId) writeState(root, run.book, run.lang, nextState(readState(root, run.book, run.lang), patch));
}

function writeRunSummary(root, run) {
  try {
    run.summaryFile = writeSummary(root, run);
    log(`summary: ${run.summaryFile}`);
  } catch (e) {
    log(`summary not written: ${e.message}`);
  }
}

/** Each cleanup step has its own try, so a throw never hides the run's own error or leaves the browser open. Returns the state error, if any. */
async function cleanup(root, s, ctx, run) {
  let stateError = null;
  run.kdpId = ctx.kdpId;
  run.state = ctx.state;
  try {
    writeFinalState(root, ctx, run);
  } catch (e) {
    stateError = e;
    log(`state not written: ${e.message}`);
    run.messages.push(`STATE NOT WRITTEN: ${e.message}`);
  }
  writeRunSummary(root, run);
  await s.close().catch((e) => log(`browser close failed: ${e.message}`));
  return stateError;
}
