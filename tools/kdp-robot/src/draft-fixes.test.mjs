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
import { assignIsbn, chooseCategories, savePage, uploadFiles, untickUnlisted } from './draft.mjs';
import { KdpNoSignal, KdpRejected, IsbnConflict, hasIsbn, isbnPlan, judgeFileCheck, judgeSave, needsUpload, requireKdpId, waitForVerdict, fileHash, stateForTitle, liveCategories } from './checks.mjs';
import { classifyPage } from './bookshelf.mjs';
import { CONTENT_SPECS } from './pages/content.mjs';
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

test('1: the category flow clicks only through the guard', async () => {
  const pub = fakeLocator({ text: 'Publish' });
  const dialog = fakeLocator({ children: { text: () => pub } });
  const page = { getByRole: (role) => (role === 'dialog' ? dialog : fakeLocator({ text: 'Choose categories' })) };
  await assert.rejects(chooseCategories(page, [['Children', 'Animals', 'Foxes']]), PublishRefused);
  assert.deepEqual(pub.calls, []);
});

test('untickUnlisted stops on a ticked "Publish" checkbox and never unticks it', async () => {
  const box = fakeLocator({ text: 'Publish', aria: 'Publish' });
  const dialog = fakeLocator();
  dialog.locator = () => ({ all: async () => [box] });
  await assert.rejects(untickUnlisted(dialog, [['Children', 'Animals', 'Foxes']]), PublishRefused);
  assert.deepEqual(box.calls, []);
  const old = fakeLocator({ text: 'Bears', aria: 'Bears' });
  dialog.locator = () => ({ all: async () => [old] });
  await untickUnlisted(dialog, [['Children', 'Animals', 'Foxes']]);
  assert.deepEqual(old.calls, ['uncheck']);
});

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
    locator: (sel) => (sel === 'body' ? { innerText: async () => (clicked ? body : 'Paperback Details') } : { allInnerTexts: async () => (clicked ? alerts : []) }),
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
  assert.deepEqual(recorded, [{ kdpId: 'ABC1234567' }]);
});

function categoryPage({ textCount = 1, leafCount = 1, checked = true }) {
  const leaf = fakeLocator({ count: leafCount, checked, text: 'Foxes' });
  const part = fakeLocator({ count: textCount, text: 'Children' });
  const dialog = fakeLocator({ children: { text: (t) => (t === 'Children' ? part : fakeLocator({ count: textCount, text: t })), checkbox: () => leaf } });
  const page = { getByRole: (role) => (role === 'dialog' ? dialog : fakeLocator({ text: 'Choose categories' })) };
  return { page, leaf, part };
}

test('5: a category is picked only on an exact single match; none or several stops the run', async () => {
  const path = [['Children', 'Animals', 'Foxes']];
  const ok = categoryPage({});
  await chooseCategories(ok.page, path);
  assert.deepEqual(ok.leaf.calls, ['check']);
  await assert.rejects(chooseCategories(categoryPage({ leafCount: 0 }).page, path), FieldNotFound);
  const two = categoryPage({ leafCount: 2 });
  await assert.rejects(chooseCategories(two.page, path), FieldNotFound);
  assert.deepEqual(two.leaf.calls, []);
  await assert.rejects(chooseCategories(categoryPage({ textCount: 2 }).page, path), FieldNotFound);
  await assert.rejects(chooseCategories(categoryPage({ checked: false }).page, path), /not ticked/);
});

test('6: live categories are matched by exact text first, else by position; none read means not set', () => {
  const fields = [{ key: 'category-1', value: 'A > B' }, { key: 'category-2', value: 'C > D' }];
  assert.deepEqual(liveCategories(['C › D', 'A > B'], fields), { 'category-1': 'A > B', 'category-2': 'C > D' });
  assert.deepEqual(liveCategories([], fields), {});
});

test('7: each AI group name matches exactly one of the content groups', () => {
  const groups = ['AI-generated text', 'AI-generated images', 'AI-generated translations', 'Publishing rights'];
  for (const key of ['ai-text', 'ai-images', 'ai-translations']) {
    assert.equal(groups.filter((g) => CONTENT_SPECS[key].group.test(g)).length, 1, key);
  }
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
function isbnPage({ confirm, shows }) {
  const assign = fakeLocator({ text: 'Assign me a KDP ISBN' });
  const ok = fakeLocator({ text: 'Assign ISBN', count: confirm ? 1 : 0 });
  const clicks = [];
  assign.click = async () => clicks.push('assign');
  ok.click = async () => clicks.push('confirm');
  return {
    clicks,
    getByRole: (role, o) => (o.name === 'Assign ISBN' ? ok : assign),
    locator: (s) => (s === 'body' ? { innerText: async () => (shows(clicks) ? 'ISBN: 978-1-2345-6789-0' : 'nothing') } : { allInnerTexts: async () => [] }),
  };
}

test('2: the ISBN is recorded only after a 978/979 number is read back, confirm dialog included', async () => {
  const plain = isbnPage({ confirm: false, shows: (c) => c.includes('assign') });
  const rec1 = [];
  await assignIsbn(plain, { record: (p) => rec1.push(p), isbnTimeoutMs: 20, isbnConfirmWaitMs: 5, pollMs: 1 });
  assert.deepEqual(plain.clicks, ['assign']);
  assert.equal(rec1.length, 1);
  assert.ok(rec1[0].fields.isbn);
  const withDialog = isbnPage({ confirm: true, shows: (c) => c.includes('confirm') });
  const rec2 = [];
  await assignIsbn(withDialog, { record: (p) => rec2.push(p), isbnTimeoutMs: 20, isbnConfirmWaitMs: 5, pollMs: 1 });
  assert.deepEqual(withDialog.clicks, ['assign', 'confirm']);
  assert.equal(rec2.length, 1);
});

test('2: an ISBN that never shows is not recorded', async () => {
  const never = isbnPage({ confirm: false, shows: () => false });
  const rec = [];
  await assert.rejects(assignIsbn(never, { record: (p) => rec.push(p), isbnTimeoutMs: 20, isbnConfirmWaitMs: 5, pollMs: 1 }), /nothing was recorded/);
  assert.deepEqual(rec, []);
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
