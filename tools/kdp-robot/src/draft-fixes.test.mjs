// Review fixes of Slice 38 pass 1: guarded clicks, save and file-check signals, KDP id, categories, page classes, ISBN, uploads.
process.env.KDP_ROBOT_NO_PACE = '1';
import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { safeCheck, safeClick, clickButton, guardField } from './browser.mjs';
import { PublishRefused } from './guard.mjs';
import { FieldNotFound } from './fieldio.mjs';
import { assignIsbn, savePage, uploadFiles } from './draft.mjs';
import { KdpNoSignal, KdpRejected, IsbnConflict, hasIsbn, isbnPlan, judgeFileCheck, judgeSave, needsUpload, requireKdpId, waitForVerdict, fileHash, stateForTitle, liveCategories } from './checks.mjs';
import { classifyPage } from './bookshelf.mjs';
import { CONTENT, CONTENT_SPECS } from './pages/content.mjs';
import { nextState } from './state.mjs';

function fakeLocator({ text = '', aria = null, count = 1, checked = true, children = {} } = {}) {
  const calls = [];
  const loc = {
    calls,
    first: () => loc,
    count: async () => count,
    innerText: async () => text,
    getAttribute: async (a) => (a === 'aria-label' ? aria : null),
    evaluate: async () => '',
    click: async () => calls.push('click'),
    check: async () => calls.push('check'),
    isChecked: async () => checked,
    uncheck: async () => calls.push('uncheck'),
    all: async () => [],
    locator: () => fakeLocator({ count: 0 }),
    getByText: (t) => children.text?.(t) ?? fakeLocator({ text: t }),
    getByRole: (role, o) => children[role]?.(o) ?? fakeLocator({ text: o?.name ?? '' }),
  };
  return loc;
}

test('1: a click or check on a control that resolves to Publish is refused and never made', async () => {
  const pub = fakeLocator({ text: 'Publish Your Paperback Book' });
  await assert.rejects(safeClick(pub, 'trim size', [/trim size/i]), PublishRefused);
  assert.deepEqual(pub.calls, []);
  const hidden = fakeLocator({ text: 'Next', aria: 'Submit for review' });
  await assert.rejects(safeCheck(hidden, 'Foxes', ['Foxes']), PublishRefused);
  assert.deepEqual(hidden.calls, []);
  const fine = fakeLocator({ text: 'Foxes' });
  await safeClick(fine, 'Foxes', ['Foxes']);
  assert.deepEqual(fine.calls, ['click']);
  await assert.rejects(clickButton({ getByRole: () => fakeLocator() }, 'Delete title'), PublishRefused);
});

test('2: a control with no Publish word but none of the allowed labels is refused and never touched', async () => {
  const goLive = fakeLocator({ text: 'Go live' });
  await assert.rejects(safeClick(goLive, 'trim size', [/trim size/i]), PublishRefused);
  await assert.rejects(safeCheck(goLive, 'Foxes', ['Foxes']), PublishRefused);
  await assert.rejects(guardField(goLive, 'title', [/book title/i]), PublishRefused);
  assert.deepEqual(goLive.calls, []);
  await assert.rejects(safeClick(fakeLocator({ text: 'Foxes' }), 'Foxes', []), PublishRefused);
  await assert.rejects(safeClick(fakeLocator({ text: 'Foxes' }), 'Foxes'), PublishRefused);
  await guardField(fakeLocator({ aria: 'Book Title' }), 'title', [/book title/i]);
});

// The category flow tests (guard, exact single match, no deleting of picks) moved to categories-cascade.test.mjs: KDP's dialog is a
// cascade of drop-downs (capture 2026-11-20), not the tick boxes these tests were written for.

test('2/3: judgeSave and judgeFileCheck read error alerts and success markers', () => {
  assert.equal(judgeSave({ alerts: ['Subtitle is too long'], bodyText: 'Your changes have been saved' }).state, 'error');
  assert.equal(judgeSave({ alerts: [], bodyText: 'Your changes have been saved.' }).state, 'saved');
  assert.equal(judgeSave({ alerts: [], bodyText: 'Paperback Details' }).state, 'pending');
  assert.equal(judgeFileCheck('manuscript', { alerts: ['Your manuscript was successfully uploaded.'] }).state, 'done');
  assert.equal(judgeFileCheck('cover', { alerts: ['Your manuscript was successfully uploaded.'] }).state, 'pending');
  assert.equal(judgeFileCheck('cover', { alerts: ['Your cover could not be processed: error 12'] }).state, 'error');
});

test('2/3: no marker before the timeout throws, an error alert throws with KDP\'s words, a stale marker does not count', async () => {
  const read = async () => ({ alerts: [], bodyText: 'nothing' });
  await assert.rejects(waitForVerdict({ what: 'x', judge: judgeSave, read, timeoutMs: 5, pollMs: 1 }), KdpNoSignal);
  const bad = async () => ({ alerts: ['Subtitle is too long'], bodyText: '' });
  await assert.rejects(waitForVerdict({ what: 'x', judge: judgeSave, read: bad, timeoutMs: 5, pollMs: 1 }), /Subtitle is too long/);
  const stale = async () => ({ alerts: ['Your manuscript was successfully uploaded.'], bodyText: '' });
  const baseline = { state: 'done', key: 'Your manuscript was successfully uploaded.' };
  await assert.rejects(waitForVerdict({ what: 'm', judge: (p) => judgeFileCheck('manuscript', p), read: stale, timeoutMs: 5, pollMs: 1, baseline }), KdpNoSignal);
  const fresh = async () => ({ alerts: ['Your manuscript was successfully uploaded. (2)'], bodyText: '' });
  assert.equal((await waitForVerdict({ what: 'm', judge: (p) => judgeFileCheck('manuscript', p), read: fresh, timeoutMs: 5, pollMs: 1, baseline })).state, 'done');
});

/** The alerts and the body text this page shows appear only after Save as Draft was clicked, unless `before` says they were there already. */
function fakeSavePage({ alerts = [], body = 'Paperback Details', url = 'https://kdp.amazon.com/en_US/title-setup/paperback/ABC1234567/details', before = false }) {
  let clicked = before;
  const btn = fakeLocator({ text: 'Save as Draft' });
  btn.click = async () => { clicked = true; };
  return {
    getByRole: () => btn,
    locator: (sel) => (sel === 'body' ? { innerText: async () => (clicked ? body : 'Paperback Details') } : { count: async () => 0, allInnerTexts: async () => (clicked ? alerts : []) }),
    url: () => url,
    title: async () => 'Paperback Details',
    screenshot: async () => {},
  };
}
const saveCtx = (extra = {}) => ({ kdpId: null, screens: [], messages: [], dir: mkdtempSync(join(tmpdir(), 'kdp-save-')), saveTimeoutMs: 20, pollMs: 1, idTimeoutMs: 20, ...extra });

test('2: savePage stops on a KDP error alert and copies its message; saves on a saved marker', async () => {
  const ctx = saveCtx();
  await assert.rejects(savePage(fakeSavePage({ alerts: ['Subtitle is too long'] }), 'details', ctx), KdpRejected);
  assert.ok(ctx.messages.includes('Subtitle is too long'));
  const ok = saveCtx();
  await savePage(fakeSavePage({ body: 'Paperback Details\nYour changes have been saved.' }), 'details', ok);
  assert.equal(ok.kdpId, 'ABC1234567');
});

test('8: a saved marker already on the page before the click does not count as the save', async () => {
  const stale = fakeSavePage({ body: 'Paperback Details\nYour changes have been saved.', before: true });
  await assert.rejects(savePage(stale, 'pricing', saveCtx()), KdpNoSignal);
});

test('2: savePage with no saved marker times out instead of passing', async () => {
  await assert.rejects(savePage(fakeSavePage({}), 'details', saveCtx()), KdpNoSignal);
});

test('4: no KDP id after the details save stops the run; a found id is recorded at once', async () => {
  assert.throws(() => requireKdpId(null), /no KDP id/);
  const noId = fakeSavePage({ body: 'Paperback Details\nYour changes have been saved.', url: 'https://kdp.amazon.com/en_US/bookshelf' });
  await assert.rejects(savePage(noId, 'details', saveCtx()), /no KDP id/);
  const recorded = [];
  const ctx = saveCtx({ record: (p) => recorded.push(p) });
  await savePage(fakeSavePage({ body: 'Paperback Details\nYour changes have been saved.' }), 'details', ctx);
  assert.deepEqual(recorded, [{ savedWithoutId: true }, { kdpId: 'ABC1234567' }]); // marker before the click, then the id (which clears it)
});


test('6: live categories are matched by exact text first, else by position; none read means not set', () => {
  const fields = [{ key: 'category-1', value: 'A > B' }, { key: 'category-2', value: 'C > D' }];
  assert.deepEqual(liveCategories(['C › D', 'A > B'], fields), { 'category-1': 'A > B', 'category-2': 'C > D' });
  assert.deepEqual(liveCategories([], fields), {});
});

test('7: the AI question is one Yes/No radio group on the real page (has-ai-content)', () => {
  assert.equal(CONTENT_SPECS['ai-content'].css, 'input[name="has-ai-content"]');
  assert.deepEqual(CONTENT_SPECS['ai-content'].values, { Yes: 'yes', No: 'no' });
});

test('9: a KDP host page with no known marker is not ok; a signed-out landing page is signin', () => {
  assert.equal(classifyPage({ url: 'https://kdp.amazon.com/en_US/bookshelf', bodyText: 'Bookshelf\n+ Create' }), 'ok');
  assert.equal(classifyPage({ url: 'https://kdp.amazon.com/en_US/title-setup/paperback/A/content', title: 'Paperback Content' }), 'ok');
  assert.equal(classifyPage({ url: 'https://kdp.amazon.com/en_US/bookshelf', bodyText: 'Something went wrong. Please try again.' }), 'unexpected');
  assert.equal(classifyPage({ url: 'https://kdp.amazon.com/', bodyText: 'Self-publish with us. Sign in  Sign up' }), 'signin');
  assert.equal(classifyPage({ url: 'https://kdp.amazon.com/en_US/bookshelf', bodyText: 'Your identity must be verified' }), 'unexpected');
});

test('12: the free ISBN is assigned only when the page and kdp/state both show none, and never twice', () => {
  assert.equal(isbnPlan({ liveHas: false, stateHas: false }), 'assign');
  assert.equal(isbnPlan({ liveHas: true, stateHas: false }), 'skip');
  assert.equal(isbnPlan({ liveHas: true, stateHas: true }), 'skip');
  assert.throws(() => isbnPlan({ liveHas: false, stateHas: true }), IsbnConflict);
  assert.equal(hasIsbn('ISBN: 978-1-2345-6789-0'), true);
  assert.equal(hasIsbn('Assign me a KDP ISBN'), false);
  assert.equal(hasIsbn('Free KDP ISBN'), false);
});

/** A content page: the assign button, an optional confirm button, and a body that shows the ISBN once `shows(clicks)` says so. */
function isbnPage({ confirm, shows, dialogLabel = 'Assign ISBN' }) {
  // Like real KDP: the page's own "Assign ISBN" stays on the page behind the modal, and the modal has its own button
  // (the same label by default). The confirm may only be pressed inside the dialog.
  const assign = fakeLocator({ text: 'Assign ISBN' });
  const clicks = [];
  assign.click = async () => clicks.push('assign');
  const inDialog = (name) => {
    const open = confirm && clicks.includes('assign');
    const ok = fakeLocator({ text: dialogLabel, count: open && name === dialogLabel ? 1 : 0 });
    ok.click = async () => clicks.push('confirm');
    return ok;
  };
  const dialog = { getByRole: (role, o) => inDialog(o.name) };
  return {
    clicks,
    getByRole: (role, o) => (o.name === 'Assign ISBN' ? assign : fakeLocator({ count: 0 })),
    locator: (s) => {
      if (s === 'body') return { innerText: async () => (shows(clicks) ? 'ISBN: 978-1-2345-6789-0' : 'nothing') };
      if (s === CONTENT.dialogScope) return dialog;
      return { count: async () => 0, allInnerTexts: async () => [] };
    },
  };
}

test('2: the ISBN is recorded only after a 978/979 number is read back, confirm dialog included', async () => {
  const plain = isbnPage({ confirm: false, shows: (c) => c.includes('assign') });
  const rec1 = [];
  await assignIsbn(plain, { isbnConfirmButton: 'Create', record: (p) => rec1.push(p), isbnTimeoutMs: 20, isbnConfirmWaitMs: 5, pollMs: 1 });
  assert.deepEqual(plain.clicks, ['assign']);
  assert.equal(rec1.length, 1);
  assert.ok(rec1[0].fields.isbn);
  // Owner's recording 2026-11-21: KDP's "Assign ISBN" opens a dialog whose button has the same label, so the default is two clicks.
  // Finding 1 (review kdpx-high): the second click must be the one in the dialog, never the page's own button behind it.
  const recorded = isbnPage({ confirm: true, shows: (c) => c.includes('confirm') });
  const rec0 = [];
  await assignIsbn(recorded, { record: (p) => rec0.push(p), isbnTimeoutMs: 20, isbnConfirmWaitMs: 5, pollMs: 1 });
  assert.deepEqual(recorded.clicks, ['assign', 'confirm']);
  assert.equal(rec0.length, 1);
  const withDialog = isbnPage({ confirm: true, shows: (c) => c.includes('confirm'), dialogLabel: 'Create' });
  const rec2 = [];
  await assignIsbn(withDialog, { isbnConfirmButton: 'Create', record: (p) => rec2.push(p), isbnTimeoutMs: 20, isbnConfirmWaitMs: 5, pollMs: 1 });
  assert.deepEqual(withDialog.clicks, ['assign', 'confirm']);
  assert.equal(rec2.length, 1);
});

test('2: an ISBN that never shows is not recorded', async () => {
  const never = isbnPage({ confirm: false, shows: () => false });
  const rec = [];
  await assert.rejects(assignIsbn(never, { record: (p) => rec.push(p), isbnTimeoutMs: 20, isbnConfirmWaitMs: 5, pollMs: 1 }), /nothing was recorded/);
  assert.deepEqual(rec, []);
});

/** An ISBN page whose body already shows a number, but under the Saving overlay for the first `overlayReads` reads. */
function isbnUnderOverlay(overlayReads) {
  const page = isbnPage({ confirm: false, shows: () => true });
  let reads = 0;
  const base = page.locator;
  page.locator = (s) => (s === 'body' || s === CONTENT.dialogScope ? base(s) : { count: async () => (reads++ < overlayReads ? 1 : 0), allInnerTexts: async () => [] });
  return page;
}

test('2: an ISBN read under the Saving overlay is never recorded; it waits for the overlay to clear', async () => {
  const rec = [];
  await assignIsbn(isbnUnderOverlay(3), { record: (p) => rec.push(p), isbnTimeoutMs: 20, isbnConfirmWaitMs: 5, pollMs: 1, overlayWaitMs: 500 });
  assert.equal(rec.length, 1);
  const stuck = [];
  await assert.rejects(assignIsbn(isbnUnderOverlay(1e9), { record: (p) => stuck.push(p), isbnTimeoutMs: 20, isbnConfirmWaitMs: 5, pollMs: 1, overlayWaitMs: 20 }), /still shows its Saving/);
  assert.deepEqual(stuck, []);
});

test('13: a PDF whose hash matches kdp/state is not uploaded again', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'kdp-up-'));
  const { writeFileSync } = await import('node:fs');
  const interior = join(dir, 'interior.pdf');
  const cover = join(dir, 'cover.pdf');
  writeFileSync(interior, 'a');
  writeFileSync(cover, 'b');
  const state = nextState(null, { files: { manuscript: fileHash(interior), cover: fileHash(cover) } });
  const untouched = new Proxy({}, { get: () => { throw new Error('page touched'); } });
  assert.deepEqual(await uploadFiles(untouched, { files: { interior, cover } }, state, { messages: [] }), {});
  assert.equal(needsUpload(state, 'cover', fileHash(cover)), false);
  assert.equal(needsUpload(state, 'cover', 'other'), true);
  assert.equal(needsUpload(null, 'cover', 'x'), true);
});

test('8: the title state comes from its bookshelf row, unknown when there is none', () => {
  const rows = [{ kdpId: 'A', state: 'in_review' }];
  assert.equal(stateForTitle(rows, 'A'), 'in_review');
  assert.equal(stateForTitle(rows, 'B'), 'unknown');
});
