// Review kdpx-medium findings 1, 3 and 4 (Slice 43).
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { matchRows, parseBookshelfRows } from './bookshelf.mjs';
import { CATEGORY } from './pages/details.mjs';

const href = (id) => `https://kdp.amazon.com/en_US/title-setup/paperback/${id}/details`;
const cat = { books: [{ id: 'fox', languages: ['en'] }] };
const strictOf = (raw) => matchRows(parseBookshelfRows(raw), cat, () => 'Fox', { strict: true, subtitleOf: () => 'A Tale' });

test('1: "Title: Subtitle" with no language is adopted when it is the only such row', () => {
  const { matched, doubtful } = strictOf([{ href: href('AAAAAAAAAA'), title: 'Fox: A Tale', text: 'Draft' }]);
  assert.deepEqual(matched.map((m) => m.row.kdpId), ['AAAAAAAAAA']);
  assert.deepEqual(doubtful, []);
});

test('1: a shown language must match; two rows with none stop; prefix-only and other titles are not adopted', () => {
  const one = strictOf([
    { href: href('AAAAAAAAAA'), title: 'Fox: A Tale', text: 'German Draft' },
    { href: href('BBBBBBBBBB'), title: 'Fox: A Tale', text: 'English Draft' },
  ]);
  assert.deepEqual(one.matched.map((m) => m.row.kdpId), ['BBBBBBBBBB']);
  const two = strictOf([
    { href: href('AAAAAAAAAA'), title: 'Fox: A Tale', text: 'Draft' },
    { href: href('BBBBBBBBBB'), title: 'Fox', text: 'Draft' },
  ]);
  assert.deepEqual(two.matched, []);
  assert.deepEqual(two.doubtful.map((r) => r.kdpId).sort(), ['AAAAAAAAAA', 'BBBBBBBBBB']);
  const other = strictOf([
    { href: href('CCCCCCCCCC'), title: 'Fox: Another Subtitle', text: 'Draft' },
    { href: href('DDDDDDDDDD'), title: 'Wolf: A Tale', text: 'Draft' },
  ]);
  assert.deepEqual(other.matched, []);
  assert.deepEqual(other.doubtful.map((r) => r.kdpId), ['CCCCCCCCCC']);
});

test('3: only a full "Books > ... > ..." path counts as a picked category', () => {
  const ok = (t) => CATEGORY.pathLine.test(t);
  assert.ok(ok("Books › Children's Books › Animals › Birds"));
  assert.ok(ok('Books > Children > Animals'));
  assert.ok(!ok('Choose up to three categories > see help'));
  assert.ok(!ok('Books › only one separator'));
  assert.ok(!ok('Error: pick a leaf > then save'));
});

test('4: no import statement follows executable code in draft.mjs', () => {
  const lines = readFileSync(new URL('./draft.mjs', import.meta.url), 'utf8').split('\n');
  const lastImport = lines.findLastIndex((l) => /^(import\s|\} from )/.test(l));
  const firstCode = lines.findIndex((l) => /^(const|let|function|export)\s/.test(l));
  assert.ok(firstCode > lastImport, `code at line ${firstCode + 1} comes before the import at line ${lastImport + 1}`);
});
