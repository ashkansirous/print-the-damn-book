// Slice 43: the categories dialog is a cascade of drop-downs (capture 2026-11-20: select react-aui-0 "Children's Books", then "Animals",
// then "Birds"; "Add another category" adds a second picker after the first one's selects). Fakes follow that capture.
process.env.KDP_ROBOT_NO_PACE = '1';
import test from 'node:test';
import assert from 'node:assert/strict';
import { chooseCategories } from './draft.mjs';
import { FieldNotFound } from './fieldio.mjs';
import { categoryPath, categoryPlacement } from './listing.mjs';
import { PublishRefused } from './guard.mjs';
import { liveCategories } from './checks.mjs';

// Earlier pickers' placement boxes stay in the dialog (review kdpx-high 6): `ticked` counts them per name, so the dialog shows one more
// box of that name than the picker's own row does. `rowScoped: false` models a dialog with no per-picker row.
function fakePage({ firstPick = 'Select one', selectCount = 12, selectId = (n) => `react-aui-${n}`, rowScoped = true } = {}) {
  let offered = null; // the placement names the newest picker offers; null = whichever is asked for
  const log = [];
  const ticked = {};
  const freshBox = (name) => {
    const box = {
      on: false,
      count: async () => 1,
      first: () => box,
      last: () => box,
      innerText: async () => name,
      getAttribute: async () => null,
      evaluate: async () => name,
      check: async (o) => { box.on = true; ticked[name] = (ticked[name] ?? 0) + 1; log.push(`check ${name}${o?.force ? ' (force)' : ''}`); },
      isChecked: async () => box.on,
    };
    return box;
  };
  const boxesOf = (name, own) => {
    const earlier = own ? 0 : (ticked[name] ?? 0);
    const offeredNow = (offered ?? [name]).includes(name) ? 1 : 0;
    const fresh = freshBox(name);
    const tickedBox = freshBox(name);
    tickedBox.on = true;
    return { count: async () => earlier + offeredNow, first: () => (earlier ? tickedBox : fresh), last: () => (offeredNow ? fresh : tickedBox) };
  };
  const row = { count: async () => (rowScoped ? 1 : 0), getByRole: (role, o) => boxesOf(o.name, true) };
  const select = (n) => ({
    first: () => select(n),
    count: async () => 1,
    innerText: async () => '',
    getAttribute: async (a) => (a === 'id' || a === 'name' ? selectId(n) : null),
    evaluate: async () => '',
    locator: (css) => (css.startsWith('xpath=') ? row : { innerText: async () => (n === 0 ? firstPick : 'Select one') }),
    selectOption: async (o) => log.push(`select ${n}: ${o.label}`),
  });
  const selects = { count: async () => selectCount, first: () => select(0), nth: (i) => select(i) };
  const dialog = {
    count: async () => 1,
    locator: (css) => (css === 'select' ? selects : assert.fail(`unexpected ${css}`)),
    getByRole: (role, o) => (role === 'checkbox' ? boxesOf(o.name, false) : assert.fail(`unexpected ${role}`)),
  };
  const button = (name) => ({
    first: () => button(name),
    innerText: async () => name,
    getAttribute: async () => null,
    evaluate: async () => '',
    click: async () => log.push(`click ${name}`),
  });
  return { log, offer: (names) => { offered = names; }, page: { getByRole: (role, o) => (role === 'dialog' ? dialog : button(o.name)) } };
}

const PATHS = [["Children's Books", 'Animals', 'Birds'], ["Children's Books", 'Humor'], ["Children's Books", 'Growing Up & Facts of Life', 'Friendship, Social Skills & School Life', 'Friendship']];

test('each path walks its own drop-downs in order; a second and third category press "Add another category" first, then Save', async () => {
  const { page, log } = fakePage();
  await chooseCategories(page, PATHS, ['Fiction', 'General', 'Nonfiction']);
  assert.deepEqual(log, [
    'click Choose categories',
    "select 0: Children's Books", 'select 1: Animals', 'select 2: Birds', 'check Fiction (force)',
    'click Add another category', "select 3: Children's Books", 'select 4: Humor', 'check General (force)',
    'click Add another category', "select 5: Children's Books", 'select 6: Growing Up & Facts of Life', 'select 7: Friendship, Social Skills & School Life', 'select 8: Friendship', 'check Nonfiction (force)',
    'click Save categories',
  ]);
});

test('review 6: two categories with the same placement name ("Fiction" twice) tick each picker\'s own box and do not stop the run', async () => {
  for (const rowScoped of [true, false]) {
    const { page, log } = fakePage({ rowScoped });
    await chooseCategories(page, [PATHS[0], PATHS[1]], ['Fiction', 'Fiction']);
    assert.deepEqual(log.filter((l) => l.startsWith('check')), ['check Fiction (force)', 'check Fiction (force)'], `rowScoped ${rowScoped}`);
    assert.ok(log.includes('click Save categories'));
  }
});

test('review 6: a leaf that offers no such box never ticks an earlier picker\'s box', async () => {
  const { page, log, offer } = fakePage({ rowScoped: false });
  await chooseCategories(page, [PATHS[0]], ['Fiction']);
  offer(['General']); // the second leaf offers only "General"; the earlier "Fiction" is still ticked in the dialog
  await assert.rejects(chooseCategories(page, [PATHS[1]], ['Fiction']), FieldNotFound);
  assert.equal(log.filter((l) => l.startsWith('check')).length, 1);
});

test('a category with no placement named in the listing stops the run before the dialog is saved', async () => {
  const { page, log } = fakePage();
  await assert.rejects(chooseCategories(page, [PATHS[0]], []), /names no placement/);
  assert.ok(!log.includes('click Save categories'));
});

test('the listing names the placement after "|" on a category line; the path is unchanged', () => {
  const line = "Children's Books > Geography & Cultures > Travel | Fiction (or the nearest)";
  assert.deepEqual(categoryPath(line), ["Children's Books", 'Geography & Cultures', 'Travel']);
  assert.equal(categoryPlacement(line), 'Fiction');
  assert.equal(categoryPlacement("Children's Books > Humor"), null);
});

test('a title that already has a first pick is left alone: nothing is picked or saved, the robot never deletes a pick', async () => {
  const { page, log } = fakePage({ firstPick: "Children's Books" });
  await assert.rejects(chooseCategories(page, PATHS), /never deletes a pick/);
  assert.deepEqual(log, ['click Choose categories']);
});

test('a drop-down that does not appear stops the run and saves nothing', async () => {
  const { page, log } = fakePage({ selectCount: 2 });
  await assert.rejects(chooseCategories(page, [PATHS[0]], ['Fiction']), FieldNotFound);
  assert.ok(!log.includes('click Save categories'));
});

test('a drop-down whose identity looks like Publish is refused and never touched', async () => {
  const { page, log } = fakePage({ selectId: () => 'publish-now' });
  await assert.rejects(chooseCategories(page, [PATHS[0]], ['Fiction']), PublishRefused);
  assert.deepEqual(log, ['click Choose categories']);
});

test('saved picks show as "Books > ... > Leaf": the root Books is dropped, the first match is by exact text', () => {
  const fields = [{ key: 'category-1', value: "Children's Books > Animals > Birds" }, { key: 'category-2', value: "Children's Books > Humor" }];
  assert.deepEqual(liveCategories(["Books › Children's Books › Humor", "Books › Children's Books › Animals › Birds"], fields), { 'category-1': fields[0].value, 'category-2': fields[1].value });
});
