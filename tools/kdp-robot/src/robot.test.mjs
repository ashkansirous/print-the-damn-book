import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { parseListing } from './listing.mjs';
import { buildFields, diffFields, changedOnly, hashValue, fieldHashes } from './fields.mjs';
import { classifyPage, matchRows, parseBookshelfRows } from './bookshelf.mjs';
import { renderSummary, writeSummary } from './summary.mjs';
import { nextState, readState, writeState } from './state.mjs';
import { FieldNotFound, readField, writeField } from './fieldio.mjs';
import { REPO_ROOT, loadCatalog, readListing } from './repo.mjs';
import { planDraft, runDraft } from './draft.mjs';
import { kdpIdFromUrl } from './pages/bookshelf.mjs';
import { splitName } from './pages/details.mjs';

const catalog = loadCatalog();
const freddy = catalog.books.find((b) => b.id === 'freddy-and-the-slipper');

test('Freddy listing parses into plain values', () => {
  const l = readListing(REPO_ROOT, 'freddy-and-the-slipper', 'en');
  assert.equal(l.title, 'Freddy and the Slipper');
  assert.equal(l.keywords.length, 7);
  assert.deepEqual(l.categories[0], ["Children's Books", 'Animals', 'Foxes & Wolves']);
  assert.equal(l.ageMin, 1); // owner, 2026-11-20: every book is ages 1 to 5. AI text below: the owner wrote Freddy's story (2026-11-26)
  assert.equal(l.ageMax, 5);
  assert.deepEqual(l.ai, { text: 'None', images: 'Entire work, with minimal or no editing', translations: 'None' });
  assert.equal(l.price, 9.99);
  assert.equal(l.pages, 34);
  assert.equal(l.contributors.length, 3);
});

test('fields cover every page and take price and finish from the catalog', () => {
  const fields = buildFields(readListing(REPO_ROOT, 'freddy-and-the-slipper', 'en'), { ...freddy, price: catalog.price, finish: catalog.finish });
  assert.deepEqual([...new Set(fields.map((f) => f.page))], ['details', 'content', 'pricing']);
  assert.equal(fields.find((f) => f.key === 'price-us').value, '9.99');
  assert.equal(fields.find((f) => f.key === 'finish').value, 'Glossy');
  assert.equal(catalog.price, 9.99);
  assert.equal(fields.filter((f) => f.key.startsWith('keyword-')).length, 7);
});

const xMarks = catalog.books.find((b) => b.id === 'x-marks-the-treasure');

test('Slice 43: X Marks the Treasure! keeps the owner keyword, its series, "Mr." and ages 1 to 5 in the fields', () => {
  const listing = readListing(REPO_ROOT, 'x-marks-the-treasure', 'en');
  assert.equal(listing.keywords[0], 'toddler bedtime picture book'); // the "(owner override)" mark is never typed into KDP
  assert.ok(!listing.keywords.some((k) => /read[- ]?aloud|audio|sound/i.test(k)));
  assert.equal(listing.subtitle, 'A Pirate Picture Book About a Seagull, a Rumbling Crew and a Pile of Pancakes');
  const fields = buildFields(listing, { ...xMarks, price: catalog.price, finish: catalog.finish, lang: 'en' });
  const value = (key) => fields.find((f) => f.key === key)?.value;
  assert.equal(value('series'), 'Little Seagull’s Big Adventures');
  assert.equal(value('series-number'), '2');
  assert.equal(value('contributor-1'), 'Mr. Ashkan Sirous');
  assert.equal(value('contributor-2'), 'Mr. Ashkan Sirous');
  assert.deepEqual([value('age-min'), value('age-max')], ['1', '5']);
  assert.equal(fields.filter((f) => f.key.startsWith('category-')).length, 3);
});

test('Slice 43: a book with no series has no series fields; the catalog gives every Ashkan Sirous row "Mr."', () => {
  const fields = buildFields(readListing(REPO_ROOT, 'freddy-and-the-slipper', 'en'), { ...freddy, price: 9.99, finish: 'glossy', lang: 'en' });
  assert.ok(!fields.some((f) => f.key.startsWith('series')));
  assert.deepEqual(catalog.books.flatMap((b) => b.contributors).filter((c) => c.name === 'Ashkan Sirous').map((c) => c.prefix), ['Mr.', 'Mr.', 'Mr.', 'Mr.']);
  assert.equal(catalog.books.find((b) => b.id === 'love-from-seagull-island').series.number, 1);
});

test('diff: same, changed and new', () => {
  const desired = [{ page: 'details', key: 'a', label: 'A', value: 'x' }, { page: 'details', key: 'b', label: 'B', value: 'y' }, { page: 'details', key: 'c', label: 'C', value: 'z' }];
  const diff = diffFields(desired, { a: ' x ', b: 'old' });
  assert.deepEqual(diff.map((d) => d.status), ['same', 'changed', 'new']);
  assert.deepEqual(changedOnly(diff).map((d) => d.key), ['b', 'c']);
  assert.equal(diff[1].old, 'old');
  assert.equal(hashValue(' x '), hashValue('x'));
  assert.deepEqual(Object.keys(fieldHashes(desired)), ['a', 'b', 'c']);
});

test('bookshelf rows: titles, formats, states, duplicates', () => {
  const rows = parseBookshelfRows([
    { href: 'https://kdp.amazon.com/en_US/title-setup/paperback/ABC1234567/details', title: ' Freddy  and the Slipper ', text: 'English Draft Live chapter', status: ' Draft ' },
    { href: 'https://kdp.amazon.com/en_US/title-setup/paperback/ABC1234567/content', title: 'dup', text: '' },
    { href: 'https://kdp.amazon.com/en_US/title-setup/kindle/XYZ9876543/details', title: 'Other', text: 'German In review', status: 'In review' },
    { href: 'https://kdp.amazon.com/other', title: 'x', text: '' },
  ]);
  assert.equal(rows.length, 2);
  assert.deepEqual(rows[0], { kdpId: 'ABC1234567', format: 'paperback', title: 'Freddy and the Slipper', language: 'English', state: 'draft' });
  assert.equal(rows[1].state, 'in_review');
  const { matched, unmatched } = matchRows(rows, catalog, (book) => (book === 'freddy-and-the-slipper' ? 'freddy and the slipper' : book));
  assert.equal(matched.length, 1);
  assert.equal(matched[0].book, 'freddy-and-the-slipper');
  assert.equal(unmatched.length, 1);
});

test('page classification: sign-in, captcha, unexpected, ok', () => {
  assert.equal(classifyPage({ url: 'https://www.amazon.com/ap/signin?x=1' }), 'signin');
  assert.equal(classifyPage({ url: 'https://kdp.amazon.com/en_US/bookshelf', bodyText: 'Enter the characters you see below' }), 'captcha');
  assert.equal(classifyPage({ url: 'https://example.com/' }), 'unexpected');
  assert.equal(classifyPage({ url: 'https://kdp.amazon.com/en_US/bookshelf', bodyText: 'Bookshelf' }), 'ok');
});

test('KDP id from url and name split', () => {
  assert.equal(kdpIdFromUrl('https://kdp.amazon.com/en_US/title-setup/paperback/ABC1234567/content'), 'ABC1234567');
  assert.equal(kdpIdFromUrl('https://kdp.amazon.com/en_US/bookshelf'), null);
  assert.equal(kdpIdFromUrl('https://kdp.amazon.com/en_US/title-setup/paperback/new/details'), null);
  assert.equal(kdpIdFromUrl('https://kdp.amazon.com/en_US/title-setup/paperback/abc1234567/details'), null);
  assert.deepEqual(splitName('Elham Shafiei Gol'), { first: 'Elham Shafiei', last: 'Gol' });
});

test('summary lists every field, uploads, messages and says nothing was published', () => {
  const diff = diffFields([{ page: 'details', key: 'title', label: 'Book title', value: 'T|x' }], { title: 'Old' });
  const md = renderSummary({ book: 'b', lang: 'en', when: 'now', kdpId: 'ID1', state: 'draft', diff, uploads: [{ kind: 'cover.pdf', path: 'p' }], messages: ['Cover OK'], screenshots: [] });
  assert.match(md, /\| details \| Book title \| Old \| T\\\|x \| changed \|/);
  assert.match(md, /nothing was published/);
  assert.match(md, /title-setup\/paperback\/ID1\/details/);
  assert.match(md, /Cover OK/);
  const root = mkdtempSync(join(tmpdir(), 'kdp-sum-'));
  assert.match(readFileSync(writeSummary(root, { book: 'b', lang: 'en', when: 'n', kdpId: null, diff: [], uploads: [], messages: [], screenshots: [] }), 'utf8'), /not created yet/);
});

test('state: write, merge, and refuse to overwrite an unreadable file', () => {
  const root = mkdtempSync(join(tmpdir(), 'kdp-state-'));
  assert.equal(readState(root, 'b', 'en'), null);
  writeState(root, 'b', 'en', nextState(null, { kdpId: 'AAAAAAAAAA', state: 'draft', fields: { t: 'h1' } }));
  const merged = nextState(readState(root, 'b', 'en'), { fields: { u: 'h2' } });
  assert.deepEqual(merged.fields, { t: 'h1', u: 'h2' });
  assert.equal(merged.kdpId, 'AAAAAAAAAA');
  mkdirSync(join(root, 'kdp', 'state', 'c'), { recursive: true });
  writeFileSync(join(root, 'kdp', 'state', 'c', 'en.json'), '{broken');
  assert.throws(() => writeState(root, 'c', 'en', {}), /will not overwrite/);
});

function fakePage(counts, values = {}) {
  const typed = [];
  const locator = (key) => ({
    count: async () => counts[key] ?? 0,
    inputValue: async () => values[key] ?? '',
    fill: async (v) => typed.push([key, v]),
    getAttribute: async (a) => (a === 'aria-label' ? key : null),
    evaluate: async () => '',
    nth() { return this; },
  });
  return { typed, getByLabel: (label) => locator(String(label)), getByRole: () => ({ getByRole: () => locator('radio') }) };
}

test('a missing or ambiguous field stops the run and types nothing', async () => {
  const spec = { kind: 'text', label: 'Book Title' };
  await assert.rejects(writeField(fakePage({}), 'title', spec, 'T'), FieldNotFound);
  const dup = fakePage({ 'Book Title': 2 });
  await assert.rejects(writeField(dup, 'title', spec, 'T'), FieldNotFound);
  assert.deepEqual(dup.typed, []);
  const ok = fakePage({ 'Book Title': 1 }, { 'Book Title': 'Old' });
  await writeField(ok, 'title', spec, 'T');
  assert.deepEqual(ok.typed, [['Book Title', 'T']]);
  assert.equal(await readField(ok, 'title', spec), 'Old');
});

test('draft plan: unknown book, language not in catalog, missing files', () => {
  assert.match(planDraft(REPO_ROOT, catalog, 'nope', 'en').errors[0], /not in kdp\/catalog/);
  assert.match(planDraft(REPO_ROOT, catalog, 'freddy-and-the-slipper', 'de').errors[0], /not set to publish/);
  // X Marks the Treasure! has a complete listing; Freddy's first category still has no placement, which the plan now refuses (review kdpx-high 10).
  const plan = planDraft(REPO_ROOT, catalog, 'x-marks-the-treasure', 'en');
  assert.ok(plan.fields.length > 20);
  assert.ok(plan.errors.every((e) => e.startsWith('missing ')));
  assert.match(planDraft(REPO_ROOT, catalog, 'freddy-and-the-slipper', 'en').errors[0], /names no placement/);
});

test('dry run writes a summary and opens no browser', async () => {
  const root = mkdtempSync(join(tmpdir(), 'kdp-dry-'));
  mkdirSync(join(root, 'kdp', 'listings', 'x-marks-the-treasure'), { recursive: true });
  writeFileSync(join(root, 'kdp', 'listings', 'x-marks-the-treasure', 'en.md'), readFileSync(join(REPO_ROOT, 'kdp', 'listings', 'x-marks-the-treasure', 'en.md')));
  const { summary, run } = await runDraft(root, catalog, 'x-marks-the-treasure', 'en', { dryRun: true });
  assert.ok(run.diff.every((d) => d.status === 'new'));
  assert.match(readFileSync(summary, 'utf8'), /dry run/);
  assert.equal(run.messages.length, 2);
});

test('the real catalog parses and holds all 16 books', () => {
  assert.equal(catalog.books.length, 16);
});
