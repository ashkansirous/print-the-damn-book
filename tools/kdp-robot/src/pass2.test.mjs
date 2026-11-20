// Review fixes of Slice 38 pass 2: real KDP ids, language-aware bookshelf match, ISBN text, owner wait, guarded fill/select/radio,
// unticking unlisted categories, the ISBN plan and the id recorded as soon as the title exists.
process.env.KDP_ROBOT_NO_PACE = '1';
import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { OwnerNeeded, ensureKdpPage, waitForOwner } from './browser.mjs';
import { matchRows, parseBookshelfRows } from './bookshelf.mjs';
import { hasIsbn, IsbnConflict } from './checks.mjs';
import { chooseCategories, createTitle, decideIsbn, recordableFields, savePage } from './draft.mjs';
import { writeField } from './fieldio.mjs';
import { PublishRefused } from './guard.mjs';
import { kdpIdFromUrl } from './pages/bookshelf.mjs';
import { nextState } from './state.mjs';

function ctl({ aria = null, text = '', count = 1, checked = true } = {}) {
  const calls = [];
  const loc = {
    calls,
    first: () => loc,
    or: () => loc,
    nth: () => loc,
    count: async () => count,
    innerText: async () => text,
    getAttribute: async (a) => (a === 'aria-label' ? aria : null),
    evaluate: async () => '',
    isChecked: async () => checked,
  };
  for (const m of ['click', 'check', 'uncheck', 'fill', 'selectOption']) loc[m] = async () => calls.push(m);
  return loc;
}

const kdpPage = (url, body = 'Paperback Details\nYour changes have been saved.') => ({
  url: () => url,
  title: async () => 'Paperback Details',
  goto: async () => {},
  locator: (sel) => (sel === 'body' ? { innerText: async () => body } : { count: async () => 0, allInnerTexts: async () => [] }),
  getByRole: () => ctl({ text: 'Create' }),
  screenshot: async () => {},
});

/** A page whose "saved" marker shows only after Save as Draft was clicked. */
function savedAfterClick(url) {
  let clicked = false;
  const btn = ctl({ text: 'Save as Draft' });
  btn.click = async () => { clicked = true; };
  const body = () => (clicked ? 'Paperback Details\nYour changes have been saved.' : 'Paperback Details');
  return { ...kdpPage(url), getByRole: () => btn, locator: (sel) => (sel === 'body' ? { innerText: async () => body() } : { count: async () => 0, allInnerTexts: async () => [] }) };
}

test('1: a "new" placeholder is never a KDP id and is not recorded after a save', async () => {
  assert.equal(kdpIdFromUrl('https://kdp.amazon.com/en_US/title-setup/paperback/new/details'), null);
  const recorded = [];
  const ctx = { kdpId: null, screens: [], messages: [], dir: mkdtempSync(join(tmpdir(), 'kdp-p2-')), saveTimeoutMs: 20, pollMs: 1, idTimeoutMs: 20, record: (p) => recorded.push(p) };
  await assert.rejects(savePage(savedAfterClick('https://kdp.amazon.com/en_US/title-setup/paperback/new/details'), 'details', ctx), /no KDP id/);
  assert.deepEqual(recorded, [{ savedWithoutId: true }]); // the id is never recorded, but the saved-without-id marker is
});

test('1c: with no id in the URL the id comes from the bookshelf row, and the saved fields are recorded with the marker', async () => {
  const recorded = [];
  const ctx = {
    kdpId: null, screens: [], messages: [], dir: mkdtempSync(join(tmpdir(), 'kdp-p2-')), saveTimeoutMs: 20, pollMs: 1, idTimeoutMs: 20,
    record: (p) => recorded.push(p), findShelfId: async () => 'ABCDE12345',
  };
  const diff = [{ key: 'title', value: 'Fox', status: 'new' }];
  await savePage(savedAfterClick('https://kdp.amazon.com/en_US/title-setup/paperback/new/details'), 'details', ctx, diff);
  assert.equal(ctx.kdpId, 'ABCDE12345');
  assert.deepEqual(recorded[0], { savedWithoutId: true });
  assert.deepEqual(recorded[1], { savedWithoutId: true, fields: recordableFields(diff) });
  assert.deepEqual(recorded[2], { kdpId: 'ABCDE12345' });
});

test('1b: a title saved without an id blocks a second Create on the next run', async () => {
  const ctx = { kdpId: null, savedWithoutId: true };
  await assert.rejects(createTitle({ goto: async () => assert.fail('no navigation') }, ctx), /no second title is created/);
  assert.equal(nextState({ kdpId: null, savedWithoutId: true }, { kdpId: 'ABCDE12345' }).savedWithoutId, undefined);
});

test('2: a bookshelf row matches on title AND language AND format; same title in two languages is not mixed up', () => {
  const catalog = { books: [{ id: 'fox', languages: ['en', 'de'] }] };
  const rows = parseBookshelfRows([
    { href: 'https://kdp.amazon.com/en_US/title-setup/paperback/DEDEDEDEDE/details', title: 'Fox', text: 'German Draft' },
    { href: 'https://kdp.amazon.com/en_US/title-setup/paperback/ENENENENEN/details', title: 'Fox', text: 'English Draft' },
    { href: 'https://kdp.amazon.com/en_US/title-setup/kindle/KIKIKIKIKI/details', title: 'Fox', text: 'English Draft' },
  ]);
  const { matched, unmatched } = matchRows(rows, catalog, () => 'Fox');
  assert.deepEqual(matched.map((m) => [m.lang, m.row.kdpId]).sort(), [['de', 'DEDEDEDEDE'], ['en', 'ENENENENEN']]);
  assert.deepEqual(unmatched.map((r) => r.kdpId), ['KIKIKIKIKI']);
});

test('2: a row that fits two catalog entries is ambiguous, never matched to the first', () => {
  const catalog = { books: [{ id: 'a', languages: ['en'] }, { id: 'b', languages: ['en'] }] };
  const rows = parseBookshelfRows([{ href: 'https://kdp.amazon.com/en_US/title-setup/paperback/AAAAAAAAAA/details', title: 'Same', text: 'English Draft' }]);
  const out = matchRows(rows, catalog, () => 'Same');
  assert.equal(out.matched.length, 0);
  assert.equal(out.ambiguous.length, 1);
});

test('3: the label "Free KDP ISBN" alone is not an assigned ISBN; a real number is', () => {
  assert.equal(hasIsbn('Free KDP ISBN\nAssign me a KDP ISBN'), false);
  assert.equal(hasIsbn('Free KDP ISBN\nISBN: 979-8-1234-5678-9'), true);
});

test('4: the owner wait resumes when a signed-in marker appears, and gives up after 10 minutes', async () => {
  let clock = 0;
  const wait = async (ms) => { clock += ms; };
  const kinds = ['signin', 'unexpected', 'ok'];
  let polls = 0;
  await waitForOwner('x', { check: async () => kinds[polls++], now: () => clock, wait });
  assert.equal(polls, 3);
  assert.equal(clock, 15000);
  clock = 0;
  await assert.rejects(waitForOwner('x', { check: async () => 'signin', now: () => clock, wait }), OwnerNeeded);
  assert.equal(clock, 10 * 60 * 1000);
});

test('4: a page that stays on sign-in after the capped number of owner waits stops the run', async () => {
  const page = { url: () => 'https://www.amazon.com/ap/signin', title: async () => '', locator: () => ({ innerText: async () => '' }) };
  let waits = 0;
  await assert.rejects(ensureKdpPage(page, { waitOwner: async () => { waits++; }, maxWaits: 2 }), OwnerNeeded);
  assert.equal(waits, 2);
});

test('5: the ISBN plan is not asked when the isbn row is same; a stale state only conflicts when the row changed', () => {
  const same = [{ key: 'isbn', status: 'same' }];
  const fresh = [{ key: 'isbn', status: 'new' }];
  assert.equal(decideIsbn(same, 'no number here', { fields: { isbn: 'h' } }), 'skip');
  assert.throws(() => decideIsbn(fresh, 'no number here', { fields: { isbn: 'h' } }), IsbnConflict);
  assert.equal(decideIsbn(fresh, 'nothing', null), 'assign');
});

test('5/7: kdp/state never gets an isbn hash from the diff, nor a category the page did not show as set', () => {
  const diff = [
    { key: 'title', value: 'T', status: 'new' },
    { key: 'isbn', value: 'Free KDP ISBN', status: 'same' },
    { key: 'category-1', value: 'A > B', status: 'new', verified: false },
  ];
  assert.deepEqual(Object.keys(recordableFields(diff)), ['title']);
});

test('6: fill, select and radio on a control that resolves to Publish are refused and nothing is typed', async () => {
  for (const [spec, value] of [[{ kind: 'text', label: 'Book Title' }, 'x'], [{ kind: 'select', label: 'Language' }, 'English'], [{ kind: 'radio', options: ['Yes'] }, 'Yes']]) {
    const bad = ctl({ aria: 'Submit for review' });
    await assert.rejects(writeField({ getByLabel: () => bad, getByRole: () => bad }, 'field', spec, value), PublishRefused);
    assert.deepEqual(bad.calls, []);
  }
  const fine = ctl({ aria: 'Book Title' });
  await writeField({ getByLabel: () => fine }, 'title', { kind: 'text', label: 'Book Title' }, 'Foxes');
  assert.deepEqual(fine.calls, ['fill']);
});

test('7: categories the catalog does not list are unticked first; listed ones stay', async () => {
  const old = ctl({ aria: 'Dogs' });
  const keep = ctl({ aria: 'Foxes' });
  const leaf = ctl({ aria: 'Foxes' });
  const dialog = { count: async () => 1, locator: () => ({ all: async () => [old, keep] }), getByText: (t) => ctl({ text: t }), getByRole: () => leaf };
  const page = { getByRole: (role) => (role === 'dialog' ? dialog : ctl({ text: 'Choose categories' })) };
  await chooseCategories(page, [['Children', 'Foxes']]);
  assert.deepEqual(old.calls, ['uncheck']);
  assert.deepEqual(keep.calls, []);
  assert.deepEqual(leaf.calls, ['check']);
});

test('9: the KDP id is recorded as soon as the title exists, before any save', async () => {
  const recorded = [];
  const ctx = { kdpId: null, record: (p) => recorded.push(p), pollMs: 1, createIdWaitMs: 20 };
  await createTitle(kdpPage('https://kdp.amazon.com/en_US/title-setup/paperback/ABCDE12345/details', 'Bookshelf\n+ Create'), ctx);
  assert.equal(ctx.kdpId, 'ABCDE12345');
  assert.deepEqual(recorded, [{ savedWithoutId: true }, { kdpId: 'ABCDE12345' }]); // the marker is on disk before the Create click
});

test('9: a Create that lands on a page with no id in its URL (the new-details page has none; the Create flow itself was not captured): nothing is recorded, the id comes with the first save', async () => {
  const recorded = [];
  const ctx = { kdpId: null, record: (p) => recorded.push(p), pollMs: 1, createIdWaitMs: 10 };
  await createTitle(kdpPage('https://kdp.amazon.com/en_US/title-setup/paperback/new/details', 'Bookshelf\n+ Create'), ctx);
  assert.equal(ctx.kdpId, null);
  assert.deepEqual(recorded, [{ savedWithoutId: true }]); // no id recorded, but the marker blocks a second Create
});
