// Slice 43: a Save as Draft with no marker reports what the whole page shows instead of a bare timeout.
process.env.KDP_ROBOT_NO_PACE = '1';
import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { describePage, savePage } from './draft.mjs';
import { KdpNoSignal } from './checks.mjs';

test('describePage lists inline errors, invalid fields and open dialogs once each, short and clean', () => {
  const lines = describePage({ texts: ['  Enter a  title \n', '', 'Enter a title'], invalid: ['data-print-book-title'], dialogs: ['Categories\nPlacement', ''] });
  assert.deepEqual(lines, ['Enter a title', 'invalid field data-print-book-title', 'open dialog: Categories Placement']);
});

test('a save with no marker throws KdpNoSignal carrying what the page shows, and the texts go to the summary', async () => {
  const shots = [];
  const body = { innerText: async () => 'Paperback Details' };
  const btn = { first: () => btn, innerText: async () => 'Save as Draft', getAttribute: async () => null, evaluate: async () => '', click: async () => {} };
  const page = {
    url: () => 'https://kdp.amazon.com/en_US/title-setup/paperback/ABC1234567/details',
    title: async () => 'Paperback Details',
    getByRole: () => btn,
    locator: (sel) => {
      if (sel === 'body') return body;
      if (sel === '[aria-invalid="true"]') return { evaluateAll: async () => ['data-print-book-primary-author-prefix'] };
      if (sel === '[role="dialog"]:visible') return { allInnerTexts: async () => [] };
      if (/alert/.test(sel)) return { allInnerTexts: async () => ['Select at least one category'], count: async () => 0 };
      return { count: async () => 0, allInnerTexts: async () => [] };
    },
    screenshot: async (o) => shots.push(o.path),
  };
  const ctx = { kdpId: 'ABC1234567', screens: [], messages: [], dir: mkdtempSync(join(tmpdir(), 'kdp-diag-')), saveTimeoutMs: 20, pollMs: 1, idTimeoutMs: 20 };
  await assert.rejects(savePage(page, 'details', ctx), (e) => e instanceof KdpNoSignal && /Select at least one category/.test(e.message) && /invalid field data-print-book-primary-author-prefix/.test(e.message));
  assert.ok(ctx.messages.includes('after the save click: Select at least one category'));
  assert.equal(shots.length, 1);
  assert.match(shots[0], /details-after-save-full\.png$/);
});
