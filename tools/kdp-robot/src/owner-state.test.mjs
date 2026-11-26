// Slice 43 H/K: what `draft` leaves to the owner (categories, prices, the series), the page-ready wait, and the page maps built from every recording.
process.env.KDP_ROBOT_NO_PACE = '1';
import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, readFileSync, readdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { PAGE_READY, categoriesOwnerSet, ownerCategoriesKept, recordableFields, runPricing, seriesAllowed, waitForPageReady } from './draft.mjs';
import { MAPS_DIR, checkMap, mapsFromRecordings, readMap, writeMaps } from './map-gen.mjs';
import { appendEvent, newRecording, readRecordings, recordingPath, writeRecording } from './recorder.mjs';
import { SERIES } from './pages/details.mjs';

const REPO_ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');

/** A page double for the categories button: its text, aria-label, how many such buttons exist and how many "Choose categories" buttons. */
function categoriesPage({ text = '', aria = '', present = 1, choose = 0 }) {
  const button = { first: () => button, textContent: async () => text, getAttribute: async () => aria, count: async () => present };
  return { locator: () => button, getByRole: () => ({ count: async () => choose }) };
}
const wanted = () => [{ key: 'category-1' }, { key: 'category-2' }, { key: 'title' }];

test('categoriesOwnerSet: "Edit categories", or the modal button with no "Choose categories" button, means the owner picked', () => {
  assert.equal(categoriesOwnerSet({ label: 'Edit categories ', present: 1, choose: 0 }), true);
  assert.equal(categoriesOwnerSet({ label: ' Change   edit categories now', present: 1, choose: 1 }), true);
  assert.equal(categoriesOwnerSet({ label: 'Choose categories', present: 1, choose: 1 }), false);
  assert.equal(categoriesOwnerSet({ label: '', present: 1, choose: 0 }), true, 'no "Choose categories" button but the modal button exists');
  assert.equal(categoriesOwnerSet({ label: '', present: 0, choose: 0 }), false, 'no button at all: nothing is known, the run goes on and fails clearly');
  assert.equal(categoriesOwnerSet({ label: '', present: 1, choose: 1 }), false);
});

test('ownerCategoriesKept keeps the owner\'s picks: unverified, said in the summary, true', async () => {
  const todo = wanted();
  const ctx = { messages: [] };
  assert.equal(await ownerCategoriesKept(categoriesPage({ text: 'Edit categories' }), todo, ctx), true);
  assert.deepEqual(todo.map((f) => f.verified), [false, false, undefined]);
  assert.match(ctx.messages.join('\n'), /not set: categories/);
  const odd = { messages: [] };
  assert.equal(await ownerCategoriesKept(categoriesPage({ text: '\n  ', aria: '' }), wanted(), odd), true, 'a label the regex misses but no Choose button');
});

test('ownerCategoriesKept leaves a title with no picks to be set, and does nothing when no category is wanted', async () => {
  const ctx = { messages: [] };
  assert.equal(await ownerCategoriesKept(categoriesPage({ text: 'Choose categories', choose: 1 }), wanted(), ctx), false);
  assert.deepEqual(ctx.messages, []);
  const untouched = new Proxy({}, { get: () => { throw new Error('page touched'); } });
  assert.equal(await ownerCategoriesKept(untouched, [{ key: 'title' }], ctx), false);
});

test('waitForPageReady waits for a visible control on Content and Pricing, and stops with a clear line when it never shows', async () => {
  const waits = [];
  const page = (fail) => ({ locator: (css) => ({ first: () => ({ waitFor: async (o) => { waits.push([css, o.state]); if (fail) throw new Error('timeout'); } }) }) });
  await waitForPageReady(page(false), { page: { name: 'content' } });
  await waitForPageReady(page(false), { page: { name: 'pricing' } });
  assert.deepEqual(waits, [[PAGE_READY.content, 'visible'], [PAGE_READY.pricing, 'visible']]);
  await assert.rejects(waitForPageReady(page(true), { page: { name: 'pricing' } }), /pricing page did not finish loading within 60 s.*nothing was typed on it/);
  waits.length = 0;
  await waitForPageReady(page(false), { page: { name: 'details' } });
  assert.deepEqual(waits, [], 'a page with no ready control is not waited for');
  await waitForPageReady({}, { page: { name: 'content' } }); // a fake page with no locator: no wait, no throw
});

test('prices are never written: a price that differs from the catalog is left, nothing is typed and the page is not saved', async () => {
  const calls = [];
  const loc = { first: () => loc, count: async () => 1, inputValue: async () => '7.99', fill: async (v) => calls.push(['fill', v]), click: async () => calls.push(['click']), getAttribute: async () => 'price-input-usd', evaluate: async () => 'Price for Amazon.com in USD' };
  const page = { locator: () => loc, getByRole: () => { calls.push(['getByRole']); return loc; } };
  const plan = { fields: [{ page: 'pricing', key: 'price-us', label: 'List price Amazon.com (USD)', value: 9.99 }] };
  const diff = await runPricing(page, plan, null, { messages: [] });
  assert.equal(diff[0].status, 'changed', 'the page price is read and reported');
  assert.equal(diff[0].old, '7.99');
  assert.deepEqual(calls, [], 'no fill, no click, no save');
});

test('review 2: the territories radio is the owner\'s too, and a skipped pricing row is not recorded as set in kdp/state', async () => {
  const calls = [];
  const radio = (checked) => ({ first: () => radio(checked), count: async () => 1, isChecked: async () => checked, check: async () => calls.push(['check']), click: async () => calls.push(['click']), getAttribute: async () => null, evaluate: async () => '' });
  const price = { first: () => price, count: async () => 1, inputValue: async () => '7.99', fill: async (v) => calls.push(['fill', v]), click: async () => calls.push(['click']), getAttribute: async () => 'price-input-usd', evaluate: async () => 'Price for Amazon.com in USD' };
  const page = { locator: (css) => (css.includes('territory-selection-type') ? radio(css.includes('"individual"')) : price), getByRole: () => { calls.push(['getByRole']); return radio(false); } };
  const plan = { fields: [
    { page: 'pricing', key: 'territories', label: 'Territories', value: 'All territories (worldwide rights)' },
    { page: 'pricing', key: 'price-us', label: 'List price Amazon.com (USD)', value: 9.99 },
  ] };
  const ctx = { messages: [] };
  const diff = await runPricing(page, plan, null, ctx);
  assert.equal(diff[0].old, 'Individual territories');
  assert.deepEqual(calls, [], 'the owner\'s radio is not touched and the page is not saved');
  assert.deepEqual(diff.map((f) => f.verified), [false, false]);
  assert.deepEqual(recordableFields(diff), {}, 'neither row is recorded as set');
  assert.equal(ctx.messages.length, 2);
});

test('the series is not written: its map exists but SERIES stays unconfirmed and the fields are marked not set', () => {
  assert.equal(SERIES.confirmed, false);
  const todo = [{ key: 'series' }, { key: 'series-number' }];
  const ctx = { messages: [] };
  assert.equal(seriesAllowed(todo, ctx), false);
  assert.match(ctx.messages[0], /not set: series.*Submit updates/);
  const series = readMap('series');
  assert.ok(series?.controls.some((c) => c.key === 'textbox|data-series-title'), 'the 2026-11-26 recording gave a series map');
  assert.ok(!series.controls.some((c) => /submit updates/i.test(c.name)), 'the blocked Submit updates click is never in a map');
});

function twoRecordings() {
  const base = 'https://kdp.amazon.com/en_US/title-setup/paperback/ID1234567890/';
  const older = newRecording('b', 'en', 'ID1234567890');
  appendEvent(older, { kind: 'check', role: 'radio', name: 'Yes', field: 'has-ai-content', value: 'yes' }, `${base}content`);
  appendEvent(older, { kind: 'click', role: 'option', name: 'Entire work', id: '' }, `${base}content`);
  const newer = newRecording('b', 'en', 'ID1234567890');
  appendEvent(newer, { kind: 'click', role: 'option', name: 'None', id: '' }, `${base}content`);
  appendEvent(newer, { kind: 'input', role: 'textbox', name: 'Series title', id: 'data-series-title', value: 'S' }, 'https://kdp.amazon.com/en_US/series-setup/series/new');
  appendEvent(newer, { kind: 'click', role: 'button', name: 'Submit updates' }, 'https://kdp.amazon.com/en_US/series-setup/series/new');
  return [older, newer];
}

test('gen-maps builds every page map from all recordings of a book, the series map included', () => {
  const [older, newer] = twoRecordings();
  const maps = mapsFromRecordings([older, newer]);
  assert.deepEqual(Object.keys(maps).sort(), ['content', 'series']);
  assert.deepEqual(maps.content.controls.map((c) => c.name), ['Yes', 'Entire work', 'None'], 'the controls of both recordings, older first');
  assert.deepEqual(maps.series.controls.map((c) => c.name), ['Series title'], 'a blocked Submit updates click is left out');
  assert.deepEqual(checkMap(maps.content, [older, newer]), []);
  assert.equal(checkMap(maps.content, newer).length, 2, 'checked against one recording only, the older one\'s controls are missing');
});

test('readRecordings finds <book>-<lang>.json and the dated older ones, dated first, and writeMaps takes the list', () => {
  const root = mkdtempSync(join(tmpdir(), 'kdp-recs-'));
  const [older, newer] = twoRecordings();
  writeRecording(recordingPath(root, 'b', 'en').replace('b-en.json', 'b-en-2026-11-21.json'), older);
  writeRecording(recordingPath(root, 'b', 'en'), newer);
  writeRecording(recordingPath(root, 'b', 'de'), newer);
  writeRecording(recordingPath(root, 'bb', 'en'), newer);
  const found = readRecordings(root, 'b', 'en');
  assert.deepEqual(found, [older, newer]);
  assert.deepEqual(readRecordings(root, 'nobody', 'en'), []);
  const dir = join(root, 'maps');
  const written = writeMaps(found, dir);
  assert.equal(written.length, 2);
  assert.deepEqual(readMap('content', dir).controls.map((c) => c.name), ['Yes', 'Entire work', 'None']);
});

test('the shipped maps equal what gen-maps makes from every recording in kdp/recordings (both X Marks recordings are kept)', () => {
  const found = readRecordings(REPO_ROOT, 'x-marks-the-treasure', 'en');
  assert.equal(found.length, 2, 'the 2026-11-21 recording and the 2026-11-26 one');
  const fresh = mapsFromRecordings(found);
  const files = readdirSync(MAPS_DIR).filter((f) => f.endsWith('.json')).map((f) => f.replace('.json', '')).sort();
  assert.deepEqual(files, Object.keys(fresh).sort());
  for (const [page, map] of Object.entries(fresh)) assert.deepEqual(JSON.parse(readFileSync(join(MAPS_DIR, `${page}.json`), 'utf8')), map, `${page}.json is stale: run gen-maps`);
});
