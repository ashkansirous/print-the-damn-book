import test from 'node:test';
import assert from 'node:assert/strict';
import { checkCatalog, kdpOutFolder } from './catalog.mjs';

const ctx = (over = {}) => ({
  bookIds: new Set(['a', 'b']),
  titleLangs: () => ['en', 'de'],
  hasListing: () => true,
  listingPrice: () => 9.99,
  ...over,
});
const book = (id, over = {}) => ({
  id, dino: `${id}.dino`, languages: ['en'], series: null,
  contributors: [{ role: 'Author', name: 'X' }], ...over,
});

const DEFAULTS = { price: 9.99, finish: 'glossy' };
const cat = (books, over = {}) => ({ ...DEFAULTS, books, ...over });

test('a clean catalog has no errors', () => {
  assert.deepEqual(checkCatalog(cat([book('a'), book('b')]), ctx()), []);
});

test('the one price and finish are required and checked at the top', () => {
  const errors = checkCatalog(cat([book('a'), book('b')], { price: 0, finish: 'shiny' }), ctx());
  assert.equal(errors.length, 2);
  assert.ok(checkCatalog({ books: [book('a'), book('b')] }, ctx()).length >= 2);
});

test('a book may not carry its own price or finish', () => {
  const errors = checkCatalog(cat([book('a', { price: 12, finish: 'matte' }), book('b')]), ctx());
  assert.equal(errors.filter((e) => e.includes('may not override')).length, 2);
});

test('unknown book folder and folder missing from catalog', () => {
  const errors = checkCatalog(cat([book('a'), book('zzz', { languages: [] })]), ctx());
  assert.ok(errors.some((e) => e.includes('"zzz": unknown book folder')));
  assert.ok(errors.some((e) => e.includes('folder "b" is not in the catalog')));
});

test('language with no titles.json entry or no listing', () => {
  const errors = checkCatalog(cat([book('a', { languages: ['en', 'fr'] }), book('b')]), ctx({ hasListing: (id, l) => l !== 'fr' }));
  assert.ok(errors.some((e) => e.includes('language "fr": no title in titles.json')));
  assert.ok(errors.some((e) => e.includes('language "fr": no listing')));
});

test('duplicate series numbers and duplicate ids', () => {
  const s = { name: 'Dino Stories', number: 1 };
  const errors = checkCatalog(cat([book('a', { series: s }), book('b', { series: s }), book('a')]), ctx());
  assert.ok(errors.some((e) => e.includes('number 1 is used by "a" and "b"')));
  assert.ok(errors.some((e) => e.includes('"a" appears twice')));
});

test('publishable entry needs dino and contributors; others do not', () => {
  const bad = { id: 'a', languages: ['en'], contributors: [] };
  const errors = checkCatalog(cat([bad, book('b', { languages: [], dino: null })]), ctx());
  assert.equal(errors.filter((e) => e.startsWith('book "a"')).length, 2);
  assert.equal(errors.filter((e) => e.startsWith('book "b"')).length, 0);
});

test('the catalog price must match the listing', () => {
  const errors = checkCatalog(cat([book('a'), book('b')], { price: 12 }), ctx());
  assert.ok(errors.some((e) => e.includes('differs from the listing')));
});

test('output folder name', () => {
  assert.equal(kdpOutFolder({ dino: 'Freddy and the Slipper v10.hd.dino' }, 'en'), 'tmp/kdp-out/Freddy and the Slipper v10.hd-en');
});
