// Fixes of the 2026-11-20 live run: hidden message templates are not errors, the "Saving..." overlay holds the verdict back, and
// the savedWithoutId marker is on disk before any click that can create a title.
import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { createTitle, savePage } from './draft.mjs';
import { KdpRejected, judgeFileCheck, judgeSave } from './checks.mjs';
import { CONTENT } from './pages/content.mjs';

const NEW_URL = 'https://kdp.amazon.com/en_US/title-setup/paperback/new/details';
const ID_URL = 'https://kdp.amazon.com/en_US/title-setup/paperback/J7D02A7MVND/details';
const HIDDEN_TEMPLATES = ['Language cannot be edited after your book has been published.', 'Scheduled release titles cannot be previously published.', 'Series information updated'];

/** A page with message boxes ({ text, visible }); a selector without :visible sees every box, as KDP's a-hidden templates are in the DOM. */
function boxPage({ boxes, url = ID_URL, savingReads = 0, events = [], clickThrows = false, throwAtClick = 0, preSavingReads = 0 }) {
  let clicked = false;
  let reads = 0;
  let pre = 0;
  let clicks = 0;
  const buttonNamed = (name) => {
    const btn = {
      first: () => btn, or: () => btn, count: async () => 1, innerText: async () => name, getAttribute: async () => null, evaluate: async () => '',
      click: async () => { events.push('click'); clicks++; clicked = true; if (clickThrows || clicks === throwAtClick) throw new Error('click failed'); },
    };
    return btn;
  };
  return {
    url: () => url,
    title: async () => 'Paperback Details',
    screenshot: async () => {},
    getByRole: (role, o) => buttonNamed(o.name),
    locator: (sel) => {
      if (sel === 'body') return { innerText: async () => (clicked ? 'Paperback Details\nYour changes have been saved.' : 'Paperback Details') };
      if (sel === CONTENT.savingOverlay) return { count: async () => (!clicked && pre++ < preSavingReads ? 1 : clicked && reads++ < savingReads ? 1 : 0) };
      const onlyVisible = sel.includes(':visible');
      return { count: async () => 0, allInnerTexts: async () => boxes.filter((b) => !onlyVisible || b.visible).map((b) => b.text) };
    },
  };
}

const ctxOf = (extra = {}) => ({ kdpId: null, screens: [], messages: [], dir: mkdtempSync(join(tmpdir(), 'kdp-alerts-')), saveTimeoutMs: 200, pollMs: 1, idTimeoutMs: 20, ...extra });

test('hidden message templates are not read as errors; the save counts on its saved marker', async () => {
  const boxes = HIDDEN_TEMPLATES.map((text) => ({ text, visible: false }));
  const ctx = ctxOf();
  await savePage(boxPage({ boxes }), 'details', ctx);
  assert.equal(ctx.kdpId, 'J7D02A7MVND');
  assert.deepEqual(ctx.messages, []);
});

test('a visible error box is counted and stops the save with KDP words', async () => {
  const boxes = [...HIDDEN_TEMPLATES.map((text) => ({ text, visible: false })), { text: 'Subtitle is too long', visible: true }];
  const ctx = ctxOf();
  await assert.rejects(savePage(boxPage({ boxes }), 'details', ctx), (e) => e instanceof KdpRejected && /Subtitle is too long/.test(e.message) && !/Language cannot/.test(e.message));
  assert.deepEqual(ctx.messages, ['Subtitle is too long']);
});

test('the Saving overlay holds the verdict back until it is gone', async () => {
  const boxes = [{ text: 'Subtitle is too long', visible: true }]; // would be an error, but the page is still saving
  const page = boxPage({ boxes: [], savingReads: 3 });
  const ctx = ctxOf();
  await savePage(page, 'details', ctx);
  assert.equal(ctx.kdpId, 'J7D02A7MVND');
  const stillSaving = boxPage({ boxes, savingReads: 1000 });
  await assert.rejects(savePage(stillSaving, 'details', ctxOf({ saveTimeoutMs: 30 })), /no success or error marker/);
});

test('the savedWithoutId marker is on disk before the Save as Draft click, even when the click fails', async () => {
  const events = [];
  const ctx = ctxOf({ record: (p) => events.push(`record ${JSON.stringify(p)}`) });
  await assert.rejects(savePage(boxPage({ boxes: [], url: NEW_URL, events, clickThrows: true }), 'details', ctx), /click failed/);
  assert.deepEqual(events.slice(0, 2), ['record {"savedWithoutId":true}', 'click']);
});

test('no marker is written for a title that already has an id', async () => {
  const events = [];
  const ctx = ctxOf({ kdpId: 'J7D02A7MVND', record: (p) => events.push(p) });
  await savePage(boxPage({ boxes: [], events: [] }), 'details', ctx);
  assert.equal(events.some((p) => p.savedWithoutId), false);
});

test('the marker is written before the Create paperback click', async () => {
  const events = [];
  const page = boxPage({ boxes: [], url: NEW_URL, events });
  page.goto = async () => {};
  const ctx = { kdpId: null, record: (p) => events.push(`record ${JSON.stringify(p)}`), pollMs: 1, createIdWaitMs: 5 };
  await createTitle(page, ctx);
  assert.equal(events[0], 'record {"savedWithoutId":true}');
  assert.ok(events.indexOf('click') > 0);
});

const bookshelfPage = (opts) => {
  const page = boxPage({ boxes: [], url: 'https://kdp.amazon.com/en_US/bookshelf', ...opts });
  page.goto = async () => {};
  return page;
};

test('a Create button click that fails removes the pre-Create marker (nothing can exist yet)', async () => {
  const events = [];
  const ctx = { kdpId: null, record: (p) => events.push(p), pollMs: 1 };
  await assert.rejects(createTitle(bookshelfPage({ throwAtClick: 1 }), ctx), /click failed/);
  assert.equal(ctx.savedWithoutId, false);
  assert.deepEqual(events, [{ savedWithoutId: true }, { savedWithoutId: undefined }]);
});

test('a failed paperback choice click keeps the marker and says exactly what to do', async () => {
  const events = [];
  const ctx = { kdpId: null, record: (p) => events.push(p), pollMs: 1 };
  await assert.rejects(createTitle(bookshelfPage({ throwAtClick: 2 }), ctx), /kdp\/state keeps savedWithoutId.*put its id in kdp\/state as kdpId.*remove savedWithoutId/);
  assert.equal(ctx.savedWithoutId, true);
  assert.deepEqual(events, [{ savedWithoutId: true }]);
});

test('a baseline taken while the overlay shows waits for it to clear; a stale saved marker then never counts', async () => {
  const ctx = ctxOf({ saveTimeoutMs: 40 });
  const page = boxPage({ boxes: [], preSavingReads: 3 });
  await savePage(page, 'details', ctx);
  assert.equal(ctx.kdpId, 'J7D02A7MVND');
  await assert.rejects(savePage(boxPage({ boxes: [], preSavingReads: 1000 }), 'details', ctxOf({ overlayWaitMs: 20 })), /still shows its Saving/);
});

test('judges: the overlay holds file checks back; a non-success visible alert blocks a saved text', () => {
  assert.equal(judgeFileCheck('manuscript', { alerts: [], bodyText: 'Manuscript uploaded successfully', saving: true }).state, 'pending');
  assert.equal(judgeSave({ alerts: ['Please enter a title'], bodyText: 'Your changes have been saved' }).state, 'pending');
  assert.equal(judgeSave({ alerts: ['Your changes have been saved'], bodyText: '' }).state, 'saved');
  assert.equal(judgeSave({ alerts: ['Please enter a title'], bodyText: 'Your changes have been saved', ignore: ['Please enter a title'] }).state, 'saved');
});

test('an IdConflict after a failed save is not swallowed; a record failure goes to the messages', async () => {
  const boxes = [{ text: 'Subtitle is too long', visible: true }];
  await assert.rejects(savePage(boxPage({ boxes }), 'details', ctxOf({ kdpId: 'ZZZZZZZZZZ' })), /KDP title id is ZZZZZZZZZZ/);
  const ctx = ctxOf({ record: (p) => { if (p.kdpId) throw new Error('disk full'); } });
  await assert.rejects(savePage(boxPage({ boxes }), 'details', ctx), KdpRejected);
  assert.ok(ctx.messages.some((m) => /disk full/.test(m)));
});

test('a save that KDP rejected but that already put the id in the URL still records the id', async () => {
  const boxes = [{ text: 'Subtitle is too long', visible: true }];
  const recorded = [];
  const ctx = ctxOf({ record: (p) => recorded.push(p) });
  await assert.rejects(savePage(boxPage({ boxes }), 'details', ctx), KdpRejected);
  assert.equal(ctx.kdpId, 'J7D02A7MVND');
  assert.deepEqual(recorded.at(-1), { kdpId: 'J7D02A7MVND' });
});

test('a harmless alert on the page does not switch off the stale-marker guard', async () => {
  const { baselineOf, waitForVerdict, KdpNoSignal } = await import('./checks.mjs');
  const page = { alerts: ['Your changes have been saved', 'Tip: add a series'], bodyText: '' };
  const baseline = baselineOf(judgeSave, page);
  assert.equal(baseline.state, 'saved');
  await assert.rejects(
    waitForVerdict({ what: 'x', judge: judgeSave, read: async () => page, timeoutMs: 20, pollMs: 5, baseline }),
    KdpNoSignal,
  );
});

test('known progress texts do not block a success marker, unknown alerts still do', () => {
  const progress = ['In Progress...', 'Not Started...', 'Processing', 'Your changes have been saved'];
  assert.equal(judgeSave({ alerts: progress }).state, 'saved');
  assert.equal(judgeSave({ alerts: ['Your changes have been saved', 'Something odd'] }).state, 'pending');
});

test('KDP\'s real "Save Successful!" alert counts as the saved marker (first save, id arrives in the URL)', async () => {
  const alerts = ['In Progress...', 'Not Started...', 'Save Successful!'];
  assert.equal(judgeSave({ alerts, bodyText: 'Paperback Details' }).state, 'saved');
  const events = [];
  const page = boxPage({ boxes: [], url: ID_URL, events });
  const orig = page.locator;
  page.locator = (sel) => {
    if (sel === 'body') return { innerText: async () => 'Paperback Details' };
    if (sel === CONTENT.messageBoxes) return { allInnerTexts: async () => (events.includes('click') ? alerts : []) };
    return orig(sel);
  };
  const ctx = ctxOf({ savedWithoutId: true });
  await savePage(page, 'details', ctx);
  assert.equal(ctx.kdpId, 'J7D02A7MVND');
});
