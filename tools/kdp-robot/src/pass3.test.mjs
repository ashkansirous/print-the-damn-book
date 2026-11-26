// Slice 38, "Design after three review passes": one owner for the KDP id, status from the status cell, unconfirmed page maps
// refuse `draft`, and the read-only `capture` command.
process.env.KDP_ROBOT_NO_PACE = '1';
import test from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdtempSync, readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { Ambiguous, IdConflict, NotEditable, PageMapNotConfirmed, assertEditable, keepKdpId, requireConfirmedPages, singleRow } from './checks.mjs';
import { DRAFT_STEPS, NEEDED_PAGES, START_PAGES, noteKdpId, runDraft, settleTitle } from './draft.mjs';
import { planStatusWrites } from './commands.mjs';
import { parseBookshelfRows, stateFromStatus } from './bookshelf.mjs';
import { CAPTURE_MAX_WIDTH, capturePage, capturePlan } from './capture.mjs';
import { assertSafeClick } from './guard.mjs';

const href = (id, p = 'details') => `https://kdp.amazon.com/en_US/title-setup/paperback/${id}/${p}`;
const shelf = (rows) => ({ $$eval: async () => rows });

test('1: the KDP id is set once; a different id stops the run, the same id is fine', () => {
  assert.equal(keepKdpId(null, 'AAAAAAAAAA'), 'AAAAAAAAAA');
  assert.equal(keepKdpId('AAAAAAAAAA', 'AAAAAAAAAA'), 'AAAAAAAAAA');
  assert.equal(keepKdpId('AAAAAAAAAA', null), 'AAAAAAAAAA');
  assert.throws(() => keepKdpId('AAAAAAAAAA', 'BBBBBBBBBB'), IdConflict);
});

test('3: a URL that carries another id never re-points the saved id', async () => {
  const recorded = [];
  const ctx = { kdpId: 'AAAAAAAAAA', record: (p) => recorded.push(p) };
  await assert.rejects(noteKdpId({ url: () => href('BBBBBBBBBB') }, ctx, 0), IdConflict);
  assert.equal(ctx.kdpId, 'AAAAAAAAAA');
  assert.deepEqual(recorded, []);
});

test('1: status skips a title with two matching rows and never overwrites a stored id', () => {
  const row = (kdpId) => ({ book: 'fox', lang: 'en', row: { kdpId, state: 'draft' } });
  const two = planStatusWrites([row('AAAAAAAAAA'), row('BBBBBBBBBB')], () => null);
  assert.equal(two.writes.length, 0);
  assert.match(two.skipped[0], /Ambiguous/);
  const clash = planStatusWrites([row('BBBBBBBBBB')], () => ({ kdpId: 'AAAAAAAAAA' }));
  assert.equal(clash.writes.length, 0);
  assert.match(clash.skipped[0], /IdConflict/);
  assert.equal(planStatusWrites([row('AAAAAAAAAA')], () => ({ kdpId: 'AAAAAAAAAA' })).writes.length, 1);
  assert.equal(planStatusWrites([row('AAAAAAAAAA')], () => null).writes.length, 1);
});

const plan = { fields: [{ key: 'title', value: 'Fox' }] };
const livePage = (rows) => ({ goto: async () => {}, url: () => 'https://kdp.amazon.com/en_US/bookshelf', title: async () => 'Bookshelf', locator: () => ({ innerText: async () => 'Bookshelf\n+ Create' }), ...shelf(rows) });
const raw = (id, status, extra = 'English') => ({ href: href(id), title: 'Fox', text: extra, status });

test('1: settleTitle adopts the one matching row, stops on two rows, and refuses a live or in-review title', async () => {
  const ctx = { kdpId: null, record: () => {} };
  await settleTitle(livePage([raw('AAAAAAAAAA', 'Draft')]), plan, ctx, { book: 'fox', lang: 'en' });
  assert.equal(ctx.kdpId, 'AAAAAAAAAA');
  const two = { kdpId: null };
  await assert.rejects(settleTitle(livePage([raw('AAAAAAAAAA', 'Live'), raw('BBBBBBBBBB', 'Draft')]), plan, two, { book: 'fox', lang: 'en' }), Ambiguous);
  const live = { kdpId: 'AAAAAAAAAA' };
  await assert.rejects(settleTitle(livePage([raw('AAAAAAAAAA', 'Live')]), plan, live, { book: 'fox', lang: 'en' }), NotEditable);
  const moved = { kdpId: 'AAAAAAAAAA' };
  await assert.rejects(settleTitle(livePage([raw('BBBBBBBBBB', 'Draft')]), plan, moved, { book: 'fox', lang: 'en' }), IdConflict);
  const offShelf = { kdpId: 'AAAAAAAAAA' };
  await assert.rejects(settleTitle(livePage([]), plan, offShelf, { book: 'fox', lang: 'en' }), NotEditable);
  const oddText = { kdpId: 'AAAAAAAAAA' };
  await assert.rejects(settleTitle(livePage([raw('AAAAAAAAAA', 'Live - View on Amazon')]), plan, oddText, { book: 'fox', lang: 'en' }), NotEditable);
  const fresh = { kdpId: null };
  await settleTitle(livePage([]), plan, fresh, { book: 'fox', lang: 'en' });
  assert.equal(fresh.kdpId, null);
});

test('2: the title links are confirmed (capture 2026-11-20): a shelf with only other titles and no saved id means a new title', async () => {
  const stranger = { href: href('CCCCCCCCCC'), title: 'Other book', text: 'English', status: 'Draft' };
  const none = { kdpId: null };
  await settleTitle(livePage([stranger]), plan, none, { book: 'fox', lang: 'en' });
  assert.equal(none.kdpId, null);
  const known = { kdpId: 'AAAAAAAAAA' };
  await assert.rejects(settleTitle(livePage([stranger]), plan, known, { book: 'fox', lang: 'en' }), NotEditable); // a saved id is checked as before
});

test('1: singleRow and assertEditable', () => {
  assert.equal(singleRow([], 'x'), null);
  assert.throws(() => singleRow([{ row: 1 }, { row: 2 }], 'x'), Ambiguous);
  assert.doesNotThrow(() => assertEditable('draft'));
  assert.throws(() => assertEditable('unknown'), NotEditable);
  assert.throws(() => assertEditable('live'), NotEditable);
  assert.throws(() => assertEditable('in_review'), NotEditable);
});

test('10: the state comes from the status cell with an exact label, not from words elsewhere in the row', () => {
  assert.equal(stateFromStatus(' Draft '), 'draft');
  assert.equal(stateFromStatus('Live'), 'live');
  assert.equal(stateFromStatus('In Review'), 'in_review');
  assert.equal(stateFromStatus('Live preview'), 'unknown');
  assert.equal(stateFromStatus(undefined), 'unknown');
  const [row] = parseBookshelfRows([{ href: href('AAAAAAAAAA'), title: 'Fox', text: 'English Draft  Go Live  Live help', status: 'Draft' }]);
  assert.equal(row.state, 'draft');
  assert.equal(parseBookshelfRows([{ href: href('AAAAAAAAAA'), title: 'Fox', text: 'English Live' }])[0].state, 'unknown');
});

test('page maps: bookshelf and details are confirmed from the 2026-11-20 capture, content and pricing from the recordings; draft refuses with the page named', async () => {
  assert.deepEqual(NEEDED_PAGES.map((p) => p.name), ['bookshelf', 'details', 'content', 'pricing']);
  assert.deepEqual(NEEDED_PAGES.map((p) => p.confirmed), [true, true, true, true]);
  for (const p of NEEDED_PAGES) {
    const only = NEEDED_PAGES.map((x) => ({ ...x, confirmed: x !== p }));
    assert.throws(() => requireConfirmedPages(only), (e) => e instanceof PageMapNotConfirmed && e.message === `page map not confirmed: ${p.name}; run capture first`);
  }
  assert.doesNotThrow(() => requireConfirmedPages(NEEDED_PAGES.map((p) => ({ ...p, confirmed: true }))));
  await assert.rejects(runDraft('/nowhere', { books: [] }, 'fox', 'en', { pages: [{ name: 'bookshelf', confirmed: false }] }), /page map not confirmed: bookshelf; run capture first/);
  await assert.rejects(runDraft('/nowhere', { books: [] }, 'fox', 'en', { pages: [{ name: 'content', confirmed: false }] }), /page map not confirmed: content; run capture first/);
});

test('the real lists: the up-front check passes with today\'s maps, and no draft step is left unconfirmed', () => {
  assert.deepEqual(START_PAGES.map((p) => p.name), ['bookshelf']);
  assert.doesNotThrow(() => requireConfirmedPages(START_PAGES));
  assert.deepEqual(DRAFT_STEPS.filter((s) => !s.page.confirmed).map((s) => s.page.name), []);
  assert.ok(START_PAGES.every((p) => NEEDED_PAGES.includes(p)));
});

test('capture: the plan is the bookshelf, the new form and one draft title\'s three pages; no draft row, no title pages', () => {
  const rows = [{ kdpId: 'LIVELIVELIVE', format: 'paperback', state: 'live' }, { kdpId: 'DRAFTDRAFT1', format: 'paperback', state: 'draft' }, { kdpId: 'KINDLEKINDLE', format: 'kindle', state: 'draft' }];
  assert.deepEqual(capturePlan(rows).map((p) => p.name), ['bookshelf', 'new-details', 'title-details', 'title-content', 'title-pricing']);
  assert.ok(capturePlan(rows).every((p) => !p.url.includes('LIVELIVELIVE') && !p.url.includes('KINDLEKINDLE')));
  assert.deepEqual(capturePlan([rows[0]]).map((p) => p.name), ['bookshelf', 'new-details']);
  assert.deepEqual(capturePlan([{ kdpId: 'UNKNOWN0001', format: 'paperback', state: 'unknown' }]).map((p) => p.name), ['bookshelf', 'new-details']);
});

test('state: written through a temp file renamed over the old one; a bad kdpId stops the run', async () => {
  const { mkdirSync, writeFileSync, readdirSync } = await import('node:fs');
  const { readState, writeState, statePath } = await import('./state.mjs');
  const root = mkdtempSync(join(tmpdir(), 'kdp-st-'));
  const file = writeState(root, 'fox', 'en', { kdpId: 'AAAAAAAAAA', state: 'draft' });
  assert.deepEqual(readdirSync(join(file, '..')), ['en.json']);
  assert.equal(readState(root, 'fox', 'en').kdpId, 'AAAAAAAAAA');
  writeFileSync(statePath(root, 'fox', 'en'), JSON.stringify({ kdpId: '../../x' }));
  assert.throws(() => readState(root, 'fox', 'en'), /not a KDP title id/);
  assert.throws(() => keepKdpId('../../x', null), /not a KDP title id/);
  mkdirSync(join(root, 'kdp', 'state', 'ok'), { recursive: true });
});

test('upload: the file input goes through the guard with the page map label', async () => {
  const { guardField } = await import('./browser.mjs');
  const { PublishRefused } = await import('./guard.mjs');
  const { CONTENT } = await import('./pages/content.mjs');
  const input = (aria) => ({ getAttribute: async () => aria, evaluate: async () => '' });
  await guardField(input('Upload manuscript'), 'manuscript file input', [CONTENT.uploadLabels.manuscript]);
  await assert.rejects(guardField(input('Publish'), 'manuscript file input', [CONTENT.uploadLabels.manuscript]), PublishRefused);
  await assert.rejects(guardField(input(null), 'cover file input', [CONTENT.uploadLabels.cover]), PublishRefused);
});

test('capture: writes <page>.html and a <page>.png no wider than 800 px, and clicks nothing', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'kdp-cap-'));
  let shot = null;
  const page = {
    content: async () => '<html>x</html>',
    evaluate: async () => ({ w: 1920, h: 1080 }),
    screenshot: async (o) => { shot = o; },
    click: () => { throw new Error('capture must not click'); },
  };
  const out = await capturePage(page, dir, 'bookshelf');
  assert.equal(readFileSync(out.html, 'utf8'), '<html>x</html>');
  assert.ok(out.png.endsWith('bookshelf.png') && existsSync(dir));
  assert.equal(shot.clip.width, CAPTURE_MAX_WIDTH);
  assert.equal(shot.path, out.png);
});

test('capture: the only buttons it uses (Choose categories, Cancel) pass the guard; Publish and Save still do not', () => {
  assertSafeClick('Choose categories');
  assertSafeClick('Cancel');
  assert.throws(() => assertSafeClick('Publish Your Paperback Book'));
  assert.throws(() => assertSafeClick('Save categories and publish'));
});

test('capture: a setup page is saved only after form controls appeared (an empty script-built shell is not a capture)', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'kdp-cap-'));
  const order = [];
  const page = {
    content: async () => { order.push('content'); return '<html>x</html>'; },
    evaluate: async () => ({ w: 800, h: 600 }),
    screenshot: async () => {},
    waitForFunction: async () => { order.push('settle'); },
  };
  await capturePage(page, dir, 'title-content');
  assert.deepEqual(order, ['settle', 'content']);
});
