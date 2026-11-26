// Review kdpx-high fixes 3, 4, 5, 7, 8 and 10 (Slice 43): each test fails on the code before its fix.
process.env.KDP_ROBOT_NO_PACE = '1';
import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { assertRunsLeft, fillContributor, listingProblems, planDraft, readContributor, runDraft, writeFinalState } from './draft.mjs';
import { buildFields } from './fields.mjs';
import { matchRows, parseBookshelfRows } from './bookshelf.mjs';
import { REDACTED, appendEvent, newRecording, recorderScript } from './recorder.mjs';
import { MAX_STOPPED_RUNS } from './summary.mjs';
import { REPO_ROOT, loadCatalog } from './repo.mjs';
import { readState, writeState } from './state.mjs';
import { NEEDED, missingControls, recordedConfirmed } from './pages/recorded.mjs';
import { checkListing } from '../../../.claude/skills/kdp-listing/listing-limits.mjs';

const catalog = loadCatalog();
const XMARKS = join(REPO_ROOT, 'kdp', 'listings', 'x-marks-the-treasure', 'en.md');
const xmarksText = () => readFileSync(XMARKS, 'utf8');

/** A root with the X Marks listing and the two PDFs the plan wants, so a non-dry run gets past the plan. */
function rootWithBook(state) {
  const root = mkdtempSync(join(tmpdir(), 'kdp-rev-'));
  mkdirSync(join(root, 'kdp', 'listings', 'x-marks-the-treasure'), { recursive: true });
  writeFileSync(join(root, 'kdp', 'listings', 'x-marks-the-treasure', 'en.md'), xmarksText());
  const out = join(root, 'tmp', 'kdp-out', 'X Marks the Treasure! v16.hd-en');
  mkdirSync(out, { recursive: true });
  writeFileSync(join(out, 'interior.pdf'), 'a');
  writeFileSync(join(out, 'cover.pdf'), 'b');
  if (state) writeState(root, 'x-marks-the-treasure', 'en', state);
  return root;
}

test('3: --reset-stops starts the stop count again; a plain run keeps it', async () => {
  const stopped = { kdpId: 'ABCDEFGHIJ', state: 'draft', fields: {}, files: {}, stoppedRuns: MAX_STOPPED_RUNS, lastStop: 'x' };
  const seen = [];
  const browser = async (root, plan, state, run) => seen.push(run.previousStops);
  await runDraft(rootWithBook(stopped), catalog, 'x-marks-the-treasure', 'en', { pages: [], resetStops: true, browser });
  assert.deepEqual(seen, [0]);
  assert.throws(() => assertRunsLeft(stopped), /hand this line to the owner/);
  await assert.rejects(runDraft(rootWithBook(stopped), catalog, 'x-marks-the-treasure', 'en', { pages: [], browser }), /stopped \d+ times in a row/);
  const second = { ...stopped, stoppedRuns: 1 };
  await runDraft(rootWithBook(second), catalog, 'x-marks-the-treasure', 'en', { pages: [], browser });
  assert.deepEqual(seen, [0, 1]);
});

test('3: a stop before the title has an id still counts', () => {
  const root = mkdtempSync(join(tmpdir(), 'kdp-stop-'));
  const run = { book: 'b', lang: 'en', stop: 'no field', previousStops: 0, diff: [] };
  writeFinalState(root, { kdpId: null, state: 'unknown' }, run);
  assert.equal(readState(root, 'b', 'en').stoppedRuns, 1);
  writeFinalState(root, { kdpId: null, state: 'unknown' }, { ...run, previousStops: 1 });
  assert.equal(readState(root, 'b', 'en').stoppedRuns, 2);
  const none = mkdtempSync(join(tmpdir(), 'kdp-stop-'));
  writeFinalState(none, { kdpId: null, state: 'unknown' }, { ...run, stop: undefined });
  assert.equal(readState(none, 'b', 'en'), null, 'a clean run with no id writes nothing');
});

test('5: a missing or old-format AI section stops the plan; it never becomes "No"', () => {
  const entry = catalog.books.find((b) => b.id === 'x-marks-the-treasure');
  const base = { language: 'English', title: 't', subtitle: 's', description: 'd', rights: 'r', keywords: [], categories: [], ageMin: 1, ageMax: 5, explicit: false };
  assert.throws(() => buildFields({ ...base, ai: { text: '', images: '', translations: '' } }, { ...entry, lang: 'en', price: 9.99, finish: 'glossy' }), /no usable answer for Text, Images, Translations/);
  assert.ok(buildFields({ ...base, ai: { text: 'None', images: 'Entire work, with minimal or no editing', translations: 'None' } }, { ...entry, lang: 'en', price: 9.99, finish: 'glossy' }).length > 5);
  const old = xmarksText().replace(/^- Text:.*$/m, '- Text: Yes');
  const root = rootWithBook();
  writeFileSync(join(root, 'kdp', 'listings', 'x-marks-the-treasure', 'en.md'), old);
  assert.match(planDraft(root, catalog, 'x-marks-the-treasure', 'en').errors[0], /AI-generated content: Text must say/);
  assert.deepEqual(listingProblems(rootWithBook(), entry, 'en'), []);
});

test('7: a row with no shown language, or one that only starts with the title, is never taken as this book', () => {
  const rows = parseBookshelfRows([
    { href: 'https://kdp.amazon.com/en_US/title-setup/paperback/AAAAAAAAAA/details', title: 'Fox', text: 'Draft' },
    { href: 'https://kdp.amazon.com/en_US/title-setup/paperback/BBBBBBBBBB/details', title: 'Fox: Other Edition', text: 'English Draft' },
    { href: 'https://kdp.amazon.com/en_US/title-setup/paperback/CCCCCCCCCC/details', title: 'Fox', text: 'German Draft' },
    { href: 'https://kdp.amazon.com/en_US/title-setup/paperback/DDDDDDDDDD/details', title: 'Fox', text: 'English Draft' },
  ]);
  const cat = { books: [{ id: 'fox', languages: ['en'] }] };
  const loose = matchRows(rows.slice(0, 2), cat, () => 'Fox');
  assert.equal(loose.matched.length, 2, 'the loose rules (the status command) still match both');
  const strict = matchRows(rows.slice(0, 3), cat, () => 'Fox', { strict: true });
  assert.deepEqual(strict.matched.map((m) => m.row.kdpId), ['AAAAAAAAAA'], 'kdpx-medium 1: the one row with no language and the exact title is adopted');
  assert.deepEqual(strict.doubtful.map((r) => r.kdpId), ['BBBBBBBBBB'], 'a title that only starts with this one is never adopted');
  assert.deepEqual(strict.unmatched.map((r) => r.kdpId), ['CCCCCCCCCC'], 'a known other language is simply not this book');
  const exact = matchRows([rows[3]], cat, () => 'Fox', { strict: true });
  assert.deepEqual(exact.matched.map((m) => m.row.kdpId), ['DDDDDDDDDD']);
});

/** A fake page with one text box per css selector, each holding a value; `fill` is logged. */
const labelOf = (css) => (/first-name/.test(css) ? 'First name' : /last-name/.test(css) ? 'Last name' : /role/.test(css) ? 'Role' : 'Prefix');
function boxesPage(values) {
  const filled = [];
  const box = (css) => ({
    first: () => box(css),
    count: async () => 1,
    inputValue: async () => values[css] ?? '',
    fill: async (v) => filled.push([css, v]),
    isEnabled: async () => true,
    selectOption: async (o) => filled.push([css, o.label]),
    getAttribute: async (a) => (a === 'aria-label' ? labelOf(css) : null),
    evaluate: async () => labelOf(css),
  });
  return { filled, locator: (css) => box(css) };
}
const PRIMARY = { first: '[id="data-print-book-primary-author-first-name"]', last: '[id="data-print-book-primary-author-last-name"]', prefix: '[id="data-print-book-primary-author-prefix"]' };

test('8: a contributor with no catalog prefix never blanks, or compares, the prefix the owner typed', async () => {
  const page = boxesPage({ [PRIMARY.prefix]: 'Dr.', [PRIMARY.first]: 'Elham Shafiei', [PRIMARY.last]: 'Gol' });
  const noPrefix = { key: 'contributor-1', role: 'Author', name: 'Elham Shafiei Gol', prefix: '', value: 'Elham Shafiei Gol' };
  assert.equal(await readContributor(page, noPrefix), 'Elham Shafiei Gol', 'the owner\'s "Dr." is not part of the compared name');
  await fillContributor(page, noPrefix);
  assert.ok(!page.filled.some(([css]) => css === PRIMARY.prefix), 'the prefix box is not written');
  assert.deepEqual(page.filled.map(([, v]) => v), ['Elham Shafiei', 'Gol']);
  const withPrefix = { key: 'contributor-2', role: 'Author', name: 'Ashkan Sirous', prefix: 'Mr.', value: 'Mr. Ashkan Sirous' };
  const page2 = boxesPage({});
  await fillContributor(page2, withPrefix);
  assert.deepEqual(page2.filled.map(([, v]) => v), ['Mr.', 'Ashkan', 'Sirous', 'Author']);
});

test('4: a recording never keeps a password, a one-time code or anything typed on a sign-in page', () => {
  const rec = newRecording('b', 'en', 'ABCDEFGHIJ');
  const kdp = 'https://kdp.amazon.com/en_US/title-setup/paperback/ABCDEFGHIJ/details';
  const password = appendEvent(rec, { kind: 'input', role: 'textbox', name: 'hunter2-secret', label: '', id: 'ap_password', inputType: 'password', value: 'hunter2-secret' }, 'https://www.amazon.com/ap/signin');
  const code = appendEvent(rec, { kind: 'input', role: 'textbox', name: '', label: 'Enter code', id: '', field: 'otpCode', inputType: 'text', value: '493817' }, kdp);
  const onSignIn = appendEvent(rec, { kind: 'input', role: 'textbox', name: 'Email', label: '', id: '', inputType: 'text', value: 'someone@example.com' }, 'https://www.amazon.com/ap/signin');
  const typedPassword = appendEvent(rec, { kind: 'input', role: 'textbox', name: 'pw-123', label: '', id: '', inputType: 'password', value: 'pw-123' }, kdp);
  for (const e of [password, code, onSignIn, typedPassword]) assert.equal(e.value, REDACTED);
  assert.equal(password.name, '', 'a box named by its own value loses the name too');
  assert.equal(rec.events.length, 4, 'the events stay');
  assert.ok(!JSON.stringify(rec).match(/hunter2|493817|someone@|pw-123/));
  const title = appendEvent(rec, { kind: 'input', role: 'textbox', name: 'Book Title', label: 'Book Title', id: 'data-print-book-title', inputType: 'text', value: 'X Marks the Treasure!' }, kdp);
  assert.equal(title.value, 'X Marks the Treasure!', 'an ordinary KDP field keeps its value');
  assert.match(recorderScript(), /secretField/);
  assert.match(recorderScript(), /\[redacted\]/);
});

test('10: every page map holds the controls the code needs, and a placement-less category line stops at plan time', () => {
  for (const page of Object.keys(NEEDED)) {
    assert.deepEqual(missingControls(page), [], `${page} map lacks a needed control`);
    assert.equal(recordedConfirmed(page), true, `${page} counts as confirmed`);
  }
  assert.ok(missingControls('pricing', { controls: [] }).length > 0, 'an empty map lacks them all');
  const without = xmarksText().replace('Birds | Fiction', 'Birds');
  assert.deepEqual(checkListing(xmarksText(), { ownerWritten: true }), []);
  const root = rootWithBook();
  writeFileSync(join(root, 'kdp', 'listings', 'x-marks-the-treasure', 'en.md'), without);
  assert.match(planDraft(root, catalog, 'x-marks-the-treasure', 'en').errors[0], /names no placement/);
});
