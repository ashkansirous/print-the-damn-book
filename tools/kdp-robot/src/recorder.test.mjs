// Slice 43 G, H, I: the recorder on a fixture page (headless Chromium, no KDP), the map generator, the bounded-run helpers.
process.env.KDP_ROBOT_NO_PACE = '1';
import test from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdtempSync, readdirSync, readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { ALLOWED_BUTTONS } from './guard.mjs';
import { MAPS_DIR, checkMap, controlKey, mapsFromRecording, writeMaps } from './map-gen.mjs';
import { appendEvent, installRecorder, newRecording, pageKindOfPath, readRecording, readRecordings, recordingPath, recorderScript, writeRecording } from './recorder.mjs';
import { assertRunsLeft } from './draft.mjs';
import { MAX_STOPPED_RUNS, renderSummary, stopLineOf } from './summary.mjs';

const REPO_ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const FIXTURE = `<!doctype html><html><body>
<label for="t">Book Title</label><input id="t" name="title" type="text">
<label><input type="radio" name="ai" value="yes"> Yes</label>
<label for="trim">Paper</label><select id="trim"><option>6 x 9</option><option>8.5 x 8.5</option></select>
<input id="f" type="file" aria-label="Manuscript file">
<div role="dialog"><button id="cats">Save categories</button></div>
<button id="draft">Save as Draft</button>
<button id="pub" onclick="window.published = true">Publish Your Paperback</button>
</body></html>`;

test('pageKindOfPath reads the KDP page from the URL path', () => {
  assert.equal(pageKindOfPath('/en_US/title-setup/paperback/4TK6278TV2X/content'), 'content');
  assert.equal(pageKindOfPath('/en_US/title-setup/paperback/new/details'), 'details');
  assert.equal(pageKindOfPath('/en_US/series/setup'), 'series');
  assert.equal(pageKindOfPath('/en_US/bookshelf'), 'other');
});

test('appendEvent numbers events, keeps a file name only and marks a Publish click blocked', () => {
  const rec = newRecording('b', 'en', 'ID1234567890');
  appendEvent(rec, { kind: 'upload', role: 'file', name: 'x', value: 'C:\\books\\interior.pdf' }, 'https://kdp.amazon.com/en_US/title-setup/paperback/ID1234567890/content');
  const e = appendEvent(rec, { kind: 'click', role: 'button', name: 'Publish Your Paperback' }, 'https://kdp.amazon.com/en_US/title-setup/paperback/ID1234567890/pricing');
  assert.deepEqual(rec.events.map((x) => x.order), [0, 1]);
  assert.equal(rec.events[0].value, 'interior.pdf');
  assert.equal(rec.events[0].page, 'content');
  assert.equal(e.blocked, true);
});

test('the injected recorder records changes and clicks in order and blocks Publish', async (t) => {
  let chromium;
  try {
    ({ chromium } = await import('playwright'));
  } catch (e) {
    return t.skip(`playwright not installed: ${e.message}`);
  }
  const browser = await chromium.launch({ headless: true }).catch((e) => { t.skip(`no chromium: ${e.message}`); });
  if (!browser) return;
  try {
    const context = await browser.newContext();
    const rec = newRecording('b', 'en', 'ID1234567890');
    await installRecorder(context, (raw, url) => appendEvent(rec, raw, url));
    const page = await context.newPage();
    await page.route('https://kdp.amazon.com/**', (r) => r.fulfill({ contentType: 'text/html', body: FIXTURE }));
    await page.goto('https://kdp.amazon.com/en_US/title-setup/paperback/ID1234567890/details');
    await page.getByLabel('Book Title').fill('X Marks');
    await page.getByLabel('Book Title').blur();
    await page.getByLabel('Yes').check();
    await page.getByLabel('Paper').selectOption({ label: '8.5 x 8.5' });
    await page.locator('#f').setInputFiles({ name: 'cover.pdf', mimeType: 'application/pdf', buffer: Buffer.from('x') });
    await page.getByRole('button', { name: 'Save categories' }).click();
    await page.getByRole('button', { name: 'Save as Draft' }).click();
    await page.getByRole('button', { name: 'Publish Your Paperback' }).click();
    await page.waitForTimeout(300);
    const kinds = rec.events.map((e) => `${e.kind}:${e.name || e.label}`);
    assert.deepEqual(kinds.slice(0, 4), ['input:Book Title', 'check:Yes', 'select:Paper', 'upload:Manuscript file']);
    assert.equal(rec.events[3].value, 'cover.pdf');
    assert.ok(rec.events.find((e) => e.name === 'Save categories').inDialog);
    assert.ok(rec.events.find((e) => e.name === 'Save as Draft'));
    const pub = rec.events.at(-1);
    assert.equal(pub.blocked, true, 'Publish click is recorded as blocked');
    assert.equal(await page.evaluate(() => window.published === true), false, 'the Publish handler never ran');
    assert.ok(rec.events.every((e) => e.page === 'details'));
  } finally {
    await browser.close();
  }
});

test('recorderScript parses as JavaScript', () => {
  assert.doesNotThrow(() => new Function(recorderScript()));
});

function sample() {
  const rec = newRecording('b', 'en', 'ID1234567890');
  const base = 'https://kdp.amazon.com/en_US/title-setup/paperback/ID1234567890/';
  appendEvent(rec, { kind: 'input', role: 'textbox', name: 'Title', label: 'Book Title', id: 't', value: 'X' }, `${base}details`);
  appendEvent(rec, { kind: 'check', role: 'radio', name: 'Yes', field: 'has-ai-content', value: 'yes' }, `${base}content`);
  appendEvent(rec, { kind: 'check', role: 'radio', name: 'No', field: 'has-ai-content', value: 'no' }, `${base}content`);
  appendEvent(rec, { kind: 'click', role: 'button', name: 'Publish Your Paperback' }, `${base}pricing`);
  appendEvent(rec, { kind: 'click', role: 'button', name: 'Save as Draft' }, `${base}pricing`);
  return rec;
}

test('maps: one per page, each control once with its values, blocked clicks left out', () => {
  const maps = mapsFromRecording(sample());
  assert.deepEqual(Object.keys(maps).sort(), ['content', 'details', 'pricing']);
  assert.equal(maps.content.controls.length, 1, 'the two radios share one control');
  assert.deepEqual(maps.content.controls[0].values, ['yes', 'no']);
  assert.deepEqual(maps.pricing.controls.map((c) => c.name), ['Save as Draft']);
});

test('a map naming a control the recording lacks fails the check', () => {
  const rec = sample();
  const maps = mapsFromRecording(rec);
  assert.deepEqual(checkMap(maps.details, rec), []);
  maps.details.controls.push({ key: 'textbox|guessed-field' });
  assert.deepEqual(checkMap(maps.details, rec), ['details map names "textbox|guessed-field", which the recording does not hold']);
  assert.equal(controlKey({ role: 'textbox', id: 't' }), 'textbox|t');
});

test('generated map files in src/pages/recorded are backed by a recording', () => {
  if (!existsSync(MAPS_DIR)) return;
  for (const file of readdirSync(MAPS_DIR).filter((f) => f.endsWith('.json'))) {
    const map = JSON.parse(readFileSync(join(MAPS_DIR, file), 'utf8'));
    const [book, lang] = [map.source.replace(/-[a-z]{2}(-[A-Za-z]+)?$/, ''), map.source.split('-').pop()];
    const recordings = readRecordings(REPO_ROOT, book, lang); // every recording of the book: <book>-<lang>.json and the dated older ones
    assert.ok(recordings.length, `${file} has no recording kdp/recordings/${map.source}.json`);
    assert.deepEqual(checkMap(map, recordings), [], file);
  }
});

test('writeMaps and writeRecording round-trip through files', () => {
  const dir = mkdtempSync(join(tmpdir(), 'kdp-maps-'));
  const rec = sample();
  const file = join(dir, 'r.json');
  writeRecording(file, rec);
  assert.deepEqual(readRecording(file), rec);
  const written = writeMaps(rec, join(dir, 'maps'));
  assert.equal(written.length, 3);
  assert.deepEqual(checkMap(JSON.parse(readFileSync(written[0], 'utf8')), rec), []);
});

test('guard allows the three buttons Slice 43 names, and Publish stays refused', () => {
  for (const b of ['Save as Draft', 'Save categories', 'Go to series setup']) assert.ok(ALLOWED_BUTTONS.includes(b), b);
});

test('bounded runs: the stop line, the summary head and the third-run refusal', () => {
  const err = Object.assign(new Error('Field "Series" not found on the page\nmore'), { field: 'Series' });
  assert.equal(stopLineOf(err), 'could not find field "Series": Field "Series" not found on the page');
  assert.match(renderSummary({ book: 'b', lang: 'en', when: 'now', diff: [], uploads: [], messages: [], screenshots: [], stop: 'x' }), /\nSTOP: x\n/);
  assert.doesNotThrow(() => assertRunsLeft({ stoppedRuns: MAX_STOPPED_RUNS - 1 }));
  assert.throws(() => assertRunsLeft({ stoppedRuns: MAX_STOPPED_RUNS, lastStop: 'y' }), /hand this line to the owner/);
  assert.doesNotThrow(() => assertRunsLeft({ stoppedRuns: MAX_STOPPED_RUNS }, true));
});
