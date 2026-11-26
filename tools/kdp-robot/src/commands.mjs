// login and status: the two commands that only read KDP.
import { openSession, ensureKdpPage, log, pace } from './browser.mjs';
import { BOOKSHELF_URL, BOOKSHELF } from './pages/bookshelf.mjs';
import { matchRows, parseBookshelfRows } from './bookshelf.mjs';
import { nextState, writeState, readState } from './state.mjs';
import { DEFAULT_CONTENT, loadCatalog, titleOf } from './repo.mjs';

export async function runLogin() {
  const s = await openSession();
  try {
    await s.page.goto(BOOKSHELF_URL);
    await ensureKdpPage(s.page);
    const marker = s.page.getByRole(BOOKSHELF.signedInMarker.role, { name: BOOKSHELF.signedInMarker.name }).first();
    await marker.waitFor({ timeout: 30000 });
    log('signed in: the bookshelf shows. The browser profile is kept for the next run.');
  } finally {
    await s.close();
  }
}

/** Runs in the page: one {href, title, text, status} per top-level shelf row (see BOOKSHELF in pages/bookshelf.mjs). */
export function readShelfRowsInPage(trs, sel) {
  return trs.map((tr) => ({
    href: tr.querySelector(sel.titleLinks)?.href ?? '',
    title: tr.querySelector(sel.titleText)?.innerText ?? '',
    text: tr.innerText ?? '',
    status: tr.querySelector(sel.statusCell)?.innerText ?? '',
  }));
}

export async function readBookshelfRows(page) {
  await page.goto(BOOKSHELF_URL);
  await ensureKdpPage(page);
  await pace();
  return page.$$eval(BOOKSHELF.rows, readShelfRowsInPage, BOOKSHELF);
}

/**
 * What `status` may write: a (book, lang) with two or more matching rows is skipped (Ambiguous), and a stored id is never
 * overwritten by a different one (IdConflict). Returns {writes, skipped: [message]}.
 */
export function planStatusWrites(matched, readStored) {
  const writes = [];
  const skipped = [];
  const count = (m) => matched.filter((x) => x.book === m.book && x.lang === m.lang).length;
  for (const m of matched) {
    const stored = readStored(m.book, m.lang)?.kdpId;
    if (count(m) > 1) skipped.push(`Ambiguous: ${count(m)} bookshelf rows fit ${m.book} ${m.lang}; nothing written for it`);
    else if (stored && stored !== m.row.kdpId) skipped.push(`IdConflict: ${m.book} ${m.lang} has id ${stored} in kdp/state, the bookshelf row says ${m.row.kdpId}; nothing written`);
    else writes.push(m);
  }
  return { writes, skipped: [...new Set(skipped)] };
}

export function formatRows(rows) {
  const line = (r) => [r.title, r.format, r.language, r.state, r.kdpId].join('\t');
  return ['title\tformat\tlanguage\tstate\tkdp id', ...rows.map(line)].join('\n');
}

export async function runStatus(root, content = DEFAULT_CONTENT) {
  const s = await openSession();
  try {
    const rows = parseBookshelfRows(await readBookshelfRows(s.page));
    console.log(formatRows(rows));
    const { matched, ambiguous } = matchRows(rows, loadCatalog(root), (b, l) => titleOf(content, b, l));
    for (const r of ambiguous) log(`skipped: "${r.title}" (${r.language}, ${r.kdpId}) fits more than one catalog entry`);
    const { writes, skipped } = planStatusWrites(matched, (b, l) => readState(root, b, l));
    for (const msg of skipped) log(`skipped: ${msg}`);
    for (const m of writes) {
      writeState(root, m.book, m.lang, nextState(readState(root, m.book, m.lang), { kdpId: m.row.kdpId, state: m.row.state, readAt: new Date().toISOString() }));
      log(`state written for ${m.book} ${m.lang}: ${m.row.state}`);
    }
  } finally {
    await s.close();
  }
}
