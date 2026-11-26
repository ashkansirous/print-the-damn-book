// Slice 43: KDP covers the Content and Pricing pages with "We found an issue on an earlier page of your title setup ..." and a "Go Back"
// button while the Details page has a problem (2026-11-20, title 4TK6278TV2X: no categories). `capture` records the text, presses Go Back
// (navigation only) and goes on; it never saves.
process.env.KDP_ROBOT_NO_PACE = '1';
import test from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdtempSync, readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { handleRedirectModal, PROBES } from './capture.mjs';
import { PublishRefused } from './guard.mjs';

const MODAL = 'We found an issue on an earlier page of your title setup. Please go back, review your information and save in order to continue.';

function fakePage({ modal = true, flagged = ['invalid field data-print-book-title'] } = {}) {
  const clicks = [];
  const goBack = { innerText: async () => 'Go Back', getAttribute: async () => null, evaluate: async () => '', click: async () => clicks.push('Go Back') };
  return {
    clicks,
    locator: (sel) => (sel === '#redirect-modal-message'
      ? { waitFor: async () => { if (!modal) throw new Error('timeout'); }, innerText: async () => MODAL }
      : { allInnerTexts: async () => [], evaluateAll: async () => flagged }),
    getByRole: (role, o) => (o.name === 'Go Back' ? goBack : assert.fail(`unexpected button ${o.name}`)),
    content: async () => '<html></html>',
    evaluate: async () => ({ w: 1000, h: 800 }),
    screenshot: async () => {},
  };
}

test('the redirect modal is read, Go Back is pressed, what the details page flags is written down, and the run goes on', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'kdp-modal-'));
  const page = fakePage();
  const errors = [];
  assert.equal(await handleRedirectModal(page, dir, { name: 'title-content' }, errors), true);
  assert.deepEqual(page.clicks, ['Go Back']);
  assert.match(readFileSync(join(dir, 'title-content-issue.txt'), 'utf8'), /earlier page of your title setup[\s\S]*invalid field data-print-book-title/);
  assert.ok(existsSync(join(dir, 'title-content-flagged-details.html')));
  assert.match(errors[0], /title-content: KDP showed "We found an issue on an earlier page/);
});

test('no modal: nothing is pressed and the page is captured as it is', async () => {
  const page = fakePage({ modal: false });
  assert.equal(await handleRedirectModal(page, '/unused', { name: 'title-content' }, []), false);
  assert.deepEqual(page.clicks, []);
});

test('the probes never include a Save, Assign ISBN or Publish control', () => {
  const labels = Object.values(PROBES).flat().map((p) => p.label);
  assert.ok(labels.length >= 4);
  for (const l of labels) assert.doesNotMatch(l, /save|assign|publish|submit/i);
  assert.ok(PublishRefused);
});
