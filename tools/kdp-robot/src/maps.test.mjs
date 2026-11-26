// Slice 38: page maps confirmed from the 2026-11-20 captures (bookshelf, details) and `draft` stopping at the first unconfirmed page.
process.env.KDP_ROBOT_NO_PACE = '1';
import test from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { StoppedAtUnconfirmedPage } from './checks.mjs';
import { DRAFT_STEPS, categoriesAllowed, primaryAuthorErrors, runSteps, seriesAllowed } from './draft.mjs';
import { FieldNotFound, readField, writeField } from './fieldio.mjs';
import { PublishRefused } from './guard.mjs';
import { clickAction } from './browser.mjs';
import { BOOKSHELF, CONTROL_CONFIRMED, PAGE as BOOKSHELF_PAGE } from './pages/bookshelf.mjs';
import { CATEGORY, CONTRIBUTOR, DETAILS_SPECS, SERIES, PAGE as DETAILS_PAGE, PRIMARY_AUTHOR, contributorSpecs, keywordSpec } from './pages/details.mjs';
import { PAGE as CONTENT_PAGE } from './pages/content.mjs';
import { PAGE as PRICING_PAGE } from './pages/pricing.mjs';

const REPO_ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const CAPTURE = join(REPO_ROOT, 'tmp', 'kdp-robot', 'capture', 'new-details.html');

/** A locator double that records the selector it was asked for and what was typed into it. */
function fakePage({ counts = {}, attrs = {}, enabled = true } = {}) {
  const calls = [];
  const loc = (sel) => ({
    count: async () => counts[sel] ?? 1,
    // The page's own id/name/frame title, as the real element would report them, read off the selector the spec used.
    getAttribute: async (a) => (a in attrs ? attrs[a] : { id: /^\[id="(.+)"\]$/.exec(sel)?.[1], name: /^input\[name="(.+?)"\]/.exec(sel)?.[1], value: /\[value="(.+?)"\]$/.exec(sel)?.[1] }[a] ?? null),
    evaluate: async () => /^iframe\[title="(.+?)"\]/.exec(sel)?.[1] ?? '',
    innerText: async () => 'text',
    inputValue: async () => 'value',
    isChecked: async () => true,
    isEnabled: async () => enabled,
    fill: async (v) => calls.push(['fill', sel, v]),
    check: async () => calls.push(['check', sel]),
    selectOption: async (o, opts) => calls.push(['select', sel, o.label, opts?.force]),
    locator: (inner) => loc(`${sel} >> ${inner}`),
    nth() { return this; },
  });
  return { calls, locator: (sel) => loc(sel), frameLocator: (f) => ({ locator: (s) => loc(`${f} >> ${s}`) }), getByLabel: () => { throw new Error('getByLabel must not be used for a css spec'); } };
}

test('maps: bookshelf and details are confirmed (capture 2026-11-20); content and pricing are confirmed from the recordings', () => {
  assert.equal(BOOKSHELF_PAGE.confirmed, true);
  assert.equal(DETAILS_PAGE.confirmed, true);
  assert.equal(CONTENT_PAGE.confirmed, true);
  assert.equal(PRICING_PAGE.confirmed, true);
});

test('bookshelf: the create control is the link "+ Create new title or series", not a button', () => {
  assert.equal(BOOKSHELF.signedInMarker.role, 'link');
  assert.match('+ Create new title or series', BOOKSHELF.signedInMarker.name);
  assert.equal(BOOKSHELF.createButton, '+ Create new title or series');
});

test('clickAction finds a button or a link by exact name through the allow-list, and refuses others', async () => {
  const seen = [];
  const target = { first: () => target, innerText: async () => '+ Create new title or series', getAttribute: async () => null, evaluate: async () => '', click: async () => seen.push('click'), or: () => target };
  const page = { getByRole: (role) => { seen.push(role); return target; } };
  await clickAction(page, '+ Create new title or series');
  assert.deepEqual(seen, ['button', 'link', 'click']);
  await assert.rejects(clickAction(page, 'Publish Your Paperback'), PublishRefused);
  await assert.rejects(clickAction(page, 'Delete title'), PublishRefused);
});

test('details specs find controls by the ids from the capture, never by label', async () => {
  const page = fakePage();
  await writeField(page, 'title', DETAILS_SPECS.title, 'Foxes');
  await writeField(page, 'language', DETAILS_SPECS.language, 'English');
  await writeField(page, 'age-min', DETAILS_SPECS['age-min'], '3');
  assert.deepEqual(page.calls, [
    ['fill', '[id="data-print-book-title"]', 'Foxes'],
    ['select', '[id="data-print-book-language-native"]', 'English', true],
    ['select', '[id="data-print-book-reading-interest-age-start-input-native"]', '3', true],
  ]);
  assert.equal(keywordSpec(6).css, '[id="data-print-book-keywords-6"]');
  assert.equal(contributorSpecs(1).last.css, '[id="data-print-book-contributors-1-last-name"]');
  assert.equal(PRIMARY_AUTHOR.first.css, '[id="data-print-book-primary-author-first-name"]');
  assert.equal(CONTRIBUTOR.addButton, 'Add Another');
});

test('a disabled select is refused, not "set" with force', async () => {
  const page = fakePage({ enabled: false });
  await assert.rejects(writeField(page, 'language', DETAILS_SPECS.language, 'English'), FieldNotFound);
  assert.deepEqual(page.calls, []);
});

test('bookshelf: confirmation is per control; the status cell and title links are not confirmed, the Create page button is', () => {
  assert.deepEqual(CONTROL_CONFIRMED, { signedInMarker: true, createButton: true, paperbackChoice: true, statusCell: true, titleLinks: true });
  assert.equal(BOOKSHELF.paperbackChoice, 'Create paperback');
});

test('categories: confirmed dialog; with no id yet they are skipped, marked unset and the summary says why; with an id they are set', () => {
  assert.equal(CATEGORY.confirmed, true); // title-categories.html: the select cascade, Add another category, Save categories
  const todo = [{ key: 'category-1' }, { key: 'title' }];
  const ctx = { messages: [], kdpId: null };
  assert.equal(categoriesAllowed(todo, ctx), false);
  assert.equal(todo[0].verified, false);
  assert.equal(todo[1].verified, undefined);
  assert.deepEqual(ctx.messages, ['not set: categories wait for the first save of the title (the button is disabled before it)']);
  assert.equal(categoriesAllowed([{ key: 'title' }], { messages: [] }), false);
  assert.equal(categoriesAllowed([{ key: 'category-1' }], { messages: [], kdpId: 'ABCDE12345' }), true);
});

test('series: the series-setup page is not captured, so the series fields are skipped, marked unset and said so (Slice 43)', () => {
  assert.equal(SERIES.confirmed, false);
  const todo = [{ key: 'series' }, { key: 'series-number' }, { key: 'title' }];
  const ctx = { messages: [] };
  assert.equal(seriesAllowed(todo, ctx), false);
  assert.deepEqual(todo.map((f) => f.verified), [false, false, undefined]);
  assert.match(ctx.messages[0], /not set: series/);
  assert.equal(seriesAllowed([{ key: 'title' }], { messages: [] }), false);
});

test('prefix boxes: the primary author and every contributor row have one, found by the ids from the capture', () => {
  assert.equal(PRIMARY_AUTHOR.prefix.css, '[id="data-print-book-primary-author-prefix"]');
  assert.equal(contributorSpecs(0).prefix.css, '[id="data-print-book-contributors-0-prefix"]');
});

test('the first contributor must be an Author, checked by the plan before any browser opens', () => {
  assert.deepEqual(primaryAuthorErrors({ id: 'fox', contributors: [{ name: 'A B', role: 'Author' }, { name: 'C D', role: 'Illustrator' }] }), []);
  assert.match(primaryAuthorErrors({ id: 'fox', contributors: [{ name: 'C D', role: 'Illustrator' }] })[0], /first contributor of "fox" must be an Author/);
  assert.equal(primaryAuthorErrors({ id: 'fox', contributors: [] }).length, 1);
});

test('details: the description is the CKEditor iframe body, radios are found by name and value', async () => {
  const page = fakePage();
  await writeField(page, 'description', DETAILS_SPECS.description, 'Once upon a time');
  await writeField(page, 'rights', DETAILS_SPECS.rights, 'I own the copyright and I hold necessary publishing rights.');
  await writeField(page, 'explicit', DETAILS_SPECS.explicit, 'No');
  assert.deepEqual(page.calls.map((c) => c.slice(0, 2)), [
    ['fill', 'iframe[title="Rich Text Editor, editor1"] >> body'],
    ['check', 'input[name="data-print-book-is-public-domain"][value="false"]'],
    ['check', 'input[name="data[print_book][is_adult_content]-radio"][value="false"]'],
  ]);
  assert.equal(await readField(page, 'explicit', DETAILS_SPECS.explicit), 'Yes');
  await assert.rejects(writeField(page, 'explicit', DETAILS_SPECS.explicit, 'Maybe'), FieldNotFound);
});

test('a css spec still goes through the Publish guard: an id or name that reads as Publish is refused and nothing is typed', async () => {
  const page = fakePage({ attrs: { id: 'publish-now' } });
  await assert.rejects(writeField(page, 'title', DETAILS_SPECS.title, 'x'), PublishRefused);
  assert.deepEqual(page.calls, []);
});

test('a css spec that matches twice or not at all stops the run', async () => {
  const dup = fakePage({ counts: { '[id="data-print-book-title"]': 2 } });
  await assert.rejects(writeField(dup, 'title', DETAILS_SPECS.title, 'x'), FieldNotFound);
  assert.deepEqual(dup.calls, []);
});

test('every id the details map uses exists in the real capture of 2026-11-20 (skipped when the capture is not on this machine)', { skip: !existsSync(CAPTURE) }, () => {
  const html = readFileSync(CAPTURE, 'utf8');
  const specs = [...Object.values(DETAILS_SPECS), PRIMARY_AUTHOR.first, PRIMARY_AUTHOR.last, ...Object.values(contributorSpecs(0)), keywordSpec(0), keywordSpec(6)];
  for (const s of specs.filter((x) => x.css)) assert.ok(html.includes(`id="${s.ref}"`) || html.includes(`name="${s.ref}"`), s.ref);
  assert.ok(html.includes('title="Rich Text Editor, editor1"'));
  assert.ok(html.includes('id="print-book-contributors-list-add-button-announce"'));
});

function stepPage() {
  const visited = [];
  return {
    visited,
    url: () => 'https://kdp.amazon.com/en_US/title-setup/paperback/ABCDE12345/details',
    title: async () => 'Paperback Content',
    goto: async (u) => visited.push(u),
    locator: () => ({ innerText: async () => 'Paperback Content' }),
  };
}

test('draft saves the confirmed pages in order and, at the first unconfirmed one, saves nothing more, captures it and stops', async () => {
  const ran = [];
  const captured = [];
  const mk = (name, confirmed) => ({ page: { name, confirmed }, run: async () => { ran.push(name); return [{ key: name }]; } });
  const steps = [mk('details', true), mk('content', false), mk('pricing', true)];
  const run = { diff: [] };
  const page = stepPage();
  const ctx = { root: '/repo', kdpId: 'ABCDE12345' };
  const capture = async (p, dir, name) => { captured.push([dir.replace(/\\/g, '/'), name]); return { html: `${dir}/${name}.html`, png: `${dir}/${name}.png` }; };
  await assert.rejects(runSteps(page, {}, null, ctx, run, { steps, capture }), (e) => e instanceof StoppedAtUnconfirmedPage && /page map not confirmed: content; nothing was saved on it/.test(e.message) && /draft-content\.html/.test(e.message));
  assert.deepEqual(ran, ['details']);
  assert.deepEqual(run.diff, [{ key: 'details' }]);
  assert.deepEqual(page.visited, ['https://kdp.amazon.com/en_US/title-setup/paperback/ABCDE12345/content']);
  assert.deepEqual(captured, [['/repo/tmp/kdp-robot/capture', 'draft-content']]);
});

test('draft with every map confirmed runs all three pages and captures nothing', async () => {
  const ran = [];
  const steps = ['details', 'content', 'pricing'].map((name) => ({ page: { name, confirmed: true }, run: async () => { ran.push(name); return []; } }));
  await runSteps(stepPage(), {}, null, { root: '/repo', kdpId: 'ABCDE12345' }, { diff: [] }, { steps, capture: async () => assert.fail('no capture expected') });
  assert.deepEqual(ran, ['details', 'content', 'pricing']);
});

test('the real step list is details, content, pricing; with the shipped maps every step is confirmed', () => {
  assert.deepEqual(DRAFT_STEPS.map((s) => s.page.name), ['details', 'content', 'pricing']);
  assert.equal(DRAFT_STEPS.find((s) => !s.page.confirmed), undefined);
});
