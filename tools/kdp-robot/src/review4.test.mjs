import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, readdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { REPO_ROOT, loadCatalog } from './repo.mjs';
import { planDraft } from './draft.mjs';
import { formatPrice } from './pages/pricing.mjs';
import { isPlainFileName } from './catalog.mjs';
import { writeState } from './state.mjs';

const catalog = loadCatalog();
const withBook = (patch) => ({ ...catalog, books: catalog.books.map((b) => (b.id === 'freddy-and-the-slipper' ? { ...b, ...patch } : b)) });

test('planDraft stops on a bad price, finish, contributors or dino before anything else', () => {
  for (const bad of [{ ...catalog, price: '9,99' }, { ...catalog, price: 0 }, { ...catalog, price: Number.NaN }]) {
    assert.match(planDraft(REPO_ROOT, bad, 'freddy-and-the-slipper', 'en').errors[0], /price/);
  }
  assert.match(planDraft(REPO_ROOT, { ...catalog, finish: 'shiny' }, 'freddy-and-the-slipper', 'en').errors[0], /finish/);
  assert.match(planDraft(REPO_ROOT, withBook({ contributors: [] }), 'freddy-and-the-slipper', 'en').errors[0], /contributors/);
  for (const dino of ['../x.dino', 'a/b.dino', 'a\\b.dino', '..']) {
    const plan = planDraft(REPO_ROOT, withBook({ dino }), 'freddy-and-the-slipper', 'en');
    assert.match(plan.errors[0], /plain file name/, dino);
    assert.equal(plan.files, undefined);
  }
});

test('plain file names', () => {
  assert.ok(isPlainFileName('freddy.dino'));
  for (const n of ['', '..', 'a/b', 'a\\b', null]) assert.equal(isPlainFileName(n), false, String(n));
});

test('formatPrice takes only a finite number above 0', () => {
  assert.equal(formatPrice(9.99), '9.99');
  for (const n of ['9,99', 0, -1, Number.NaN, undefined]) assert.throws(() => formatPrice(n), /not a number above 0/, String(n));
});

test('writeState replaces the file whole and leaves no temp file after a good write', () => {
  const root = mkdtempSync(join(tmpdir(), 'kdp-state-'));
  const file = writeState(root, 'b', 'en', { kdpId: 'AAAAAAAAAA', state: 'draft' });
  writeState(root, 'b', 'en', { kdpId: 'AAAAAAAAAA', state: 'in_review' });
  assert.deepEqual(readdirSync(dirname(file)), ['en.json']);
});
