import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { checkListing as check, listingBookId, ownerWrittenIds, printingCost, proposePrice, royalty } from './listing-limits.mjs';

const freddy = new URL('../../../kdp/listings/freddy-and-the-slipper/en.md', import.meta.url);
const owner = ownerWrittenIds(JSON.parse(readFileSync(new URL('../../../kdp/catalog.json', import.meta.url), 'utf8')));
/** Freddy is one of the owner's three books, so its listing is checked as one (Text "None"). */
const checkListing = (text, opts = { ownerWritten: true }) => check(text, opts);

test('the Freddy listing passes', () => {
  assert.deepEqual(checkListing(readFileSync(freddy, 'utf8')), []);
});

test('every listing under kdp/listings passes', () => {
  const root = new URL('../../../kdp/listings/', import.meta.url);
  for (const dir of readdirSync(root, { withFileTypes: true }).filter((d) => d.isDirectory())) {
    for (const file of readdirSync(new URL(`${dir.name}/`, root)).filter((f) => f.endsWith('.md'))) {
      assert.deepEqual(check(readFileSync(new URL(`${dir.name}/${file}`, root), 'utf8'), { ownerWritten: owner.has(dir.name) }), [], `${dir.name}/${file}`);
    }
  }
});

test('the owner wrote three books: Text is None for them and never Entire work (owner, 2026-11-26; Slice 43 K)', () => {
  assert.deepEqual([...owner].sort(), ['freddy-and-the-slipper', 'love-from-seagull-island', 'x-marks-the-treasure']);
  assert.equal(listingBookId('kdp/listings/x-marks-the-treasure/en.md'), 'x-marks-the-treasure');
  assert.equal(listingBookId('D:\\r\\kdp\\listings\\freddy-and-the-slipper\\en.md'), 'freddy-and-the-slipper');
  const text = readFileSync(freddy, 'utf8');
  assert.ok(check(text, { ownerWritten: false }).some((e) => e.includes('Text must say "Yes, Entire work')), 'a book the owner did not write still needs Entire work');
  const entire = text.replace(/- Text: None[^\n]*/, '- Text: Yes, Entire work, with minimal or no editing');
  assert.ok(check(entire, { ownerWritten: true }).some((e) => e.includes('Text must say "None"')));
  assert.deepEqual(check(entire, { ownerWritten: false }), []);
});

test('printing cost uses the large-trim premium colour bands', () => {
  assert.equal(printingCost(34), 4.2);
  assert.equal(printingCost(42), 4.36);
  assert.equal(printingCost(41), null);
});

test('price proposal is 9.99 when that already pays, and royalty is 60 percent minus cost', () => {
  assert.equal(proposePrice(34), 9.99);
  assert.equal(royalty(9.99, 34), 1.794);
});

test('a bad listing fails every limit it breaks', () => {
  const text = readFileSync(freddy, 'utf8')
    .replace(/## Subtitle\n+.+/, '## Subtitle\n\n' + 'x'.repeat(200))
    .replace(/(## List price \(USD\)\n+)[\d.]+/, (_m, head) => head + '9.5');
  const errors = checkListing(text);
  assert.ok(errors.some((e) => e.includes('title + subtitle')));
  assert.ok(errors.some((e) => e.includes('price 9.5')));
});

const KEYWORD_1 = '1. fussy eater kids tale';
const original = () => readFileSync(freddy, 'utf8');
const edit = (from, to) => checkListing(original().replace(from, () => to));

test('AI disclosure answers per field: Entire work for text and images, None for translations in English', () => {
  assert.ok(edit('- Translations: None', '- Translations: Yes, Entire work, with minimal or no editing').some((e) => e.includes('Translations')));
  assert.ok(edit('- Images: Yes, Entire work, with minimal or no editing', '- Images: Yes').some((e) => e.includes('Images')));
  assert.ok(edit(/- Text: None[^\n]*\n/, '').some((e) => e.includes('Text')));
  assert.ok(edit(/- Text: None[^\n]*/, '- Text: Yes, Many AI-generated images, with minimal or no editing').some((e) => e.includes('Text')));
});

test('AI disclosure for another language needs Entire work for translations', () => {
  const german = original().replace('## Language\n\nEnglish', '## Language\n\nGerman');
  assert.ok(checkListing(german).some((e) => e.includes('Translations')));
  const answered = german.replace(/- Translations: None[^\n]*/, '- Translations: Yes, Entire work, with minimal or no editing');
  assert.ok(!checkListing(answered).some((e) => e.includes('Translations')));
});

test('keywords may not repeat title, subtitle or category words, plurals included', () => {
  assert.ok(edit(KEYWORD_1, '1. funny foxes').some((e) => e.includes('foxes')));
  assert.ok(edit(KEYWORD_1, '1. toddler humor').some((e) => e.includes('humor')));
  assert.ok(edit(KEYWORD_1, '1. fox hunting stories').some((e) => e.includes('fox')));
});

test('an owner-chosen keyword may repeat words, but only with "(owner override)", and never a banned word', () => {
  assert.ok(edit(KEYWORD_1, '1. toddler bedtime picture book').some((e) => e.includes('picture')));
  assert.deepEqual(edit(KEYWORD_1, '1. toddler bedtime picture book (owner override)'), []);
  assert.ok(edit(KEYWORD_1, '1. read aloud tale (owner override)').some((e) => e.includes('paper book')));
});

test('role labels in contributors are not treated as used words', () => {
  assert.deepEqual(edit(KEYWORD_1, '1. author interview tips'), []);
});

test('a keyword for something a paper book is not is refused (Slice 43)', () => {
  for (const bad of ['read aloud bedtime tale', 'read-aloud stories', 'audio bedtime', 'sound book toddlers', 'talking books kids', 'listen along tale']) {
    assert.ok(edit(KEYWORD_1, `1. ${bad}`).some((e) => e.includes('paper book')), bad);
  }
});

test('keyword count and category count are enforced', () => {
  assert.ok(edit('7. fussy eating kids', '').some((e) => e.includes('6 keywords')));
  assert.ok(edit('3. Children', '3x Children').some((e) => e.includes('categories')));
});

test('headings must all be present and in the form order', () => {
  assert.ok(edit('## ISBN', '## Isbn').some((e) => e.includes('missing heading "## ISBN"')));
  assert.ok(edit('## Publishing rights', '## Rights').some((e) => e.includes('Publishing rights')));
  const swapped = original().replace('## Keywords', '## Tmp').replace('## Description', '## Keywords').replace('## Tmp', '## Description');
  assert.ok(checkListing(swapped).some((e) => e.includes('not in the KDP form')));
});

test('a price other than the proposed one needs an owner override and a matching sum', () => {
  assert.ok(edit('## List price (USD)\n\n9.99', '## List price (USD)\n\n19.99').some((e) => e.includes('lowest paying price 9.99')));
  assert.deepEqual(edit('## List price (USD)\n\n9.99', '## List price (USD)\n\nowner override 9.99'), []);
  assert.ok(edit('= 1.794', '= 2').some((e) => e.includes('sum text')));
});

test('AI disclosure matches a whole list line, not a word ending in Text', () => {
  assert.ok(edit('- Text: None', '- Context: None').some((e) => e.includes('Text')));
  assert.ok(!edit('- Text: None', '- Context: None').some((e) => e.includes('Images')));
});

test('the sum text must show the royalty as a whole number', () => {
  assert.ok(edit('= 1.794', '= 11.794').some((e) => e.includes('sum text')));
});

test('an odd page count says there is no printing cost band', () => {
  assert.ok(edit('34 pages (', '41 pages (').some((e) => e.includes('no cost for it')));
});

test('a price over KDP maximum fails even with an owner override', () => {
  assert.ok(edit('## List price (USD)\n\n9.99', '## List price (USD)\n\nowner override 300').some((e) => e.includes('maximum 250')));
});

test('-es plurals count as the same word as the title word', () => {
  const withBox = original().replace(/(## Keywords\n\n)([^\n]+)/, (_m, h) => `${h}1. foxes`).replace('## Book title\n\n', '## Book title\n\nFox ');
  assert.ok(checkListing(withBox).some((e) => e.includes('repeats words')));
});

test('page count is read from "N pages", not the first number such as the trim size', () => {
  assert.ok(edit('34 pages (', '8.5 x 8.5 in, 41 pages (').some((e) => e.includes('no cost for it')));
});

test('royalty text matches by value, so 4.20 style and 1.79 trailing zeros are fine', () => {
  assert.deepEqual(edit('= 1.794', '= 1.7940'), []);
});

test('a missing heading is reported', () => {
  assert.ok(checkListing('## Language\n\nEnglish').some((e) => e.includes('missing heading')));
});
