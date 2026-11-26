// Pure parsing of the KDP bookshelf and of the page the browser landed on.
import { BOOKSHELF } from './pages/bookshelf.mjs';

const TITLE_LINK = /title-setup\/(paperback|kindle|hardcover|ebook)\/([A-Z0-9]{10,})(?:[/?#]|$)/;
// Catalog language codes -> the language names KDP's bookshelf rows show.
const LANGUAGE_NAMES = {
  en: 'English', de: 'German', fr: 'French', es: 'Spanish', it: 'Italian', pt: 'Portuguese', nl: 'Dutch', sv: 'Swedish', da: 'Danish',
  fi: 'Finnish', no: 'Norwegian', nb: 'Norwegian', ru: 'Russian', ja: 'Japanese', zh: 'Chinese', ar: 'Arabic', fa: 'Persian',
};
const LANGUAGES = ['English', 'German', 'French', 'Spanish', 'Italian', 'Portuguese', 'Dutch', 'Swedish', 'Danish', 'Finnish', 'Norwegian',
  'Russian', 'Japanese', 'Chinese', 'Arabic', 'Persian', 'Farsi'];

/** The state from the row's own status cell: the whole cell text must be one exact label; anything else is 'unknown'. */
export function stateFromStatus(status) {
  const text = String(status ?? '').replace(/\s+/g, ' ').trim();
  return Object.entries(BOOKSHELF.statusLabels).find(([, re]) => re.test(text))?.[0] ?? 'unknown';
}

/** rawRows: [{href, title, text, status}] from the bookshelf's title links, their rows and each row's status cell. Rows with no title link are skipped. */
export function parseBookshelfRows(rawRows) {
  const seen = new Set();
  const rows = [];
  for (const r of rawRows) {
    const m = TITLE_LINK.exec(r.href ?? '');
    if (!m || seen.has(m[2])) continue;
    seen.add(m[2]);
    rows.push({
      kdpId: m[2],
      format: m[1].toLowerCase() === 'ebook' ? 'kindle' : m[1].toLowerCase(),
      title: String(r.title ?? '').replace(/\s+/g, ' ').trim(),
      language: LANGUAGES.find((l) => new RegExp(`\\b${l}\\b`, 'i').test(r.text ?? '')) ?? 'unknown',
      state: stateFromStatus(r.status),
    });
  }
  return rows;
}

const norm = (t) => String(t).toLowerCase().replace(/\s+/g, ' ').trim();

// The bookshelf row shows no language (capture 2026-11-20), so an unknown one fits any language: a book with the same title in two
// languages then lands in `ambiguous`, never on the first.
const sameLanguage = (row, lang) => {
  if (row.language === 'unknown') return true;
  const name = LANGUAGE_NAMES[lang];
  return Boolean(name) && (row.language === name || (name === 'Persian' && row.language === 'Farsi'));
};

// KDP lists a title as "Title: Subtitle", so a row also fits when it starts with the title and a colon.
const sameTitle = (rowTitle, title) => norm(rowTitle) === norm(title) || norm(rowTitle).startsWith(`${norm(title)}:`);

// Strict (the editing path, review kdpx-high 7): the exact title and a language the shelf actually shows. A row with no language, or
// one that only starts with the title, is never taken as this book.
// Slice 43 (review kdpx-medium 1): KDP writes "Title: Subtitle" and often shows no language, so strict accepts the exact title or exactly
// "<title>: <subtitle>"; a language the row shows must match, and a row showing none counts only if it is the only such row (below).
const exactTitle = (rowTitle, title, subtitle) => norm(rowTitle) === norm(title) || (Boolean(subtitle) && norm(rowTitle) === `${norm(title)}: ${norm(subtitle)}`);

/** The catalog pairs a row stands for: same title, same language, paperback. */
function candidatesFor(row, pairs, titleOf, strict, subtitleOf) {
  if (row.format !== 'paperback') return [];
  const [title, language] = strict ? [exactTitle, sameLanguage] : [sameTitle, sameLanguage];
  return pairs.filter((p) => title(row.title, titleOf(p.book, p.lang), subtitleOf?.(p.book, p.lang)) && language(row, p.lang));
}

/** Strict only: rows that matched with no language shown are adopted only when one such row fits that pair; two or more go to `doubtful`. */
function demoteSharedUnknown(out) {
  const unknown = out.matched.filter((m) => m.row.language === 'unknown');
  const sameKey = (a, b) => a.book === b.book && a.lang === b.lang;
  const shared = unknown.filter((m) => unknown.filter((o) => sameKey(o, m)).length > 1);
  out.matched = out.matched.filter((m) => !shared.includes(m));
  out.doubtful.push(...shared.map((m) => m.row));
}

/**
 * Pair paperback rows with catalog (book, lang) pairs by title AND language AND format. titleOf(book, lang) -> string.
 * Returns {matched, unmatched, ambiguous, doubtful}: a row that fits two or more pairs goes to `ambiguous`, never to the first.
 * With `strict`, a row that fits only the loose rules (no language shown, or a "Title: ..." prefix) goes to `doubtful` and is not matched.
 */
export function matchRows(rows, catalog, titleOf, { strict = false, subtitleOf } = {}) {
  const pairs = catalog.books.flatMap((b) => b.languages.map((lang) => ({ book: b.id, lang })));
  const out = { matched: [], unmatched: [], ambiguous: [], doubtful: [] };
  for (const row of rows) {
    const hits = candidatesFor(row, pairs, titleOf, strict, subtitleOf);
    if (hits.length === 1) out.matched.push({ ...hits[0], row });
    else if (hits.length > 1) out.ambiguous.push(row);
    else if (strict && candidatesFor(row, pairs, titleOf, false).length > 0) out.doubtful.push(row);
    else out.unmatched.push(row);
  }
  if (strict) demoteSharedUnknown(out);
  return out;
}

// A KDP page counts as known only when it shows one of these (the bookshelf's Create button or a title step heading).
const KDP_PAGE_MARKER = /\bBookshelf\b|\bPaperback (Details|Content|Rights)\b|\bRights\s*(&|and)\s*Pricing\b|\+\s*Create\b|\bCreate a (new )?title\b/i;
const SIGN_IN_PROMPT = /\bsign[\s-]?in\b/i;

/** What the browser landed on: 'ok' | 'signin' | 'captcha' | 'unexpected'. A KDP host page with no known marker is never 'ok'. */
export function classifyPage({ url, title = '', bodyText = '' }) {
  if (/\/ap\/(signin|mfa|cvf|challenge)|\/ax\/claim|ap\/widget/i.test(url)) return 'signin';
  if (/validatecaptcha|captcha/i.test(url) || /enter the characters you see|type the characters|solve this puzzle/i.test(bodyText)) return 'captcha';
  if (!/^https:\/\/kdp\.amazon\.com\//i.test(url)) return 'unexpected';
  if (KDP_PAGE_MARKER.test(`${title}\n${bodyText}`)) return 'ok';
  return SIGN_IN_PROMPT.test(`${title}\n${bodyText}`) ? 'signin' : 'unexpected';
}

export function ownerInstruction(kind) {
  const what = { signin: 'Sign in (and type any two-step code) in the open browser window.', captcha: 'Solve the check in the open browser window.',
    unexpected: 'The browser is on a page the robot does not know. Look at the window and get back to KDP.' }[kind];
  return `${what} The robot carries on by itself once KDP shows (waits up to 10 minutes).`;
}
