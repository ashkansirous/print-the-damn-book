// Pure checks for one KDP listing file (kdp/listings/<book-id>/<lang>.md). Limits are KDP's own, read from its help pages
// (URLs and read date in SKILL.md); change them there first, then here.

export const LIMITS = {
  titleAndSubtitleBelow: 200, // https://kdp.amazon.com/en_US/help/topic/G201097560 : "fewer than 200 characters" together
  descriptionMax: 4000, // https://kdp.amazon.com/en_US/help/topic/G201189630 : counts HTML tags
  keywordCount: 7, // https://kdp.amazon.com/en_US/help/topic/G201298500
  categoryCount: 3, // https://kdp.amazon.com/en_US/help/topic/G200652170
  // Premium colour on white paper, read 2026-11-19: https://kdp.amazon.com/en_US/help/topic/G201834340 (min 24, max 828) and
  // https://kdp.amazon.com/en_US/help/topic/GVBQ3CMEQW3W2VL6 (min 24, max 828; some larger trims have lower maximums, and the page
  // does not list premium colour for 8.5 x 8.5 in: unconfirmed for this trim). Page parity is NOT a KDP-confirmed limit.
  minPages: 24,
  maxPages: 828,
  royaltyMinPrice: 9.99, // https://kdp.amazon.com/en_US/help/topic/G201834330 (read 2026-11-19): 60% from 9.99 USD, 50% at or below 9.98
  royaltyHigh: 0.6,
  royaltyLow: 0.5,
  maxPrice: 250, // https://kdp.amazon.com/en_US/help/topic/G201834330 and G201834340 (read 2026-11-19): "maximum of 250 USD (Amazon.com)"
};

// Premium colour, Amazon.com, large trim (wider than 6.12 in or taller than 9 in), read 2026-11-19 from
// https://kdp.amazon.com/en_US/help/topic/G201834340 : 24-40 pages 4.20 fixed; 42-828 pages 1.00 fixed + 0.08 per page. The page gives
// no cost for 41 pages (an odd count), so printingCost returns null there.
export const PRINT_COST = [
  { fromPages: 24, toPages: 40, fixed: 4.2, perPage: 0 },
  { fromPages: 42, toPages: 828, fixed: 1.0, perPage: 0.08 },
];

// The KDP form's order; every heading must be present and in this order.
export const REQUIRED_HEADINGS = [
  'Language',
  'Book title',
  'Subtitle',
  'Series',
  'Authors and contributors',
  'Description',
  'Keywords',
  'Categories',
  'Reading age',
  'AI-generated content',
  'Publishing rights',
  'Sexually explicit images or title',
  'Pages',
  'List price (USD)',
  'ISBN',
];

const STOPWORDS = new Set(['a', 'an', 'and', 'the', 'of', 'to', 'in', 'on', 'for', 'or', 'with', 'who', 'up', 's']);
const ROLE_LABEL = /^\s*(?:[-*]\s*)?(?:author|illustrator|editor|translator|narrator|contributor|photographer)\s*:\s*/i;

const HTML_TAG = /<[^>]*>/;

export function parseSections(markdown) {
  const sections = {};
  let current = null;
  for (const line of markdown.replace(/\r/g, '').split('\n')) {
    const heading = /^## (.+?)\s*$/.exec(line);
    if (heading) {
      current = heading[1];
      sections[current] = [];
    } else if (current) {
      sections[current].push(line);
    }
  }
  return Object.fromEntries(Object.entries(sections).map(([name, lines]) => [name, lines.join('\n').trim()]));
}

export function printingCost(pages) {
  const band = PRINT_COST.find((b) => pages >= b.fromPages && pages <= b.toPages);
  return band ? Math.round((band.fixed + pages * band.perPage) * 1000) / 1000 : null;
}

export function royalty(price, pages) {
  const cost = printingCost(pages);
  if (cost === null) return null;
  const rate = price >= LIMITS.royaltyMinPrice ? LIMITS.royaltyHigh : LIMITS.royaltyLow;
  return Math.round((rate * price - cost) * 1000) / 1000;
}

/** The lowest price at or above 9.99 whose royalty is positive (whole cents). */
export function proposePrice(pages) {
  for (let cents = Math.round(LIMITS.royaltyMinPrice * 100); cents <= LIMITS.maxPrice * 100; cents += 1) {
    const price = cents / 100;
    const r = royalty(price, pages);
    if (r !== null && r > 0) return price;
  }
  return null;
}

function keywordList(text) {
  return text.split('\n').map((l) => l.replace(/^\s*(?:\d+[.)]|[-*])\s*/, '').trim()).filter(Boolean);
}

function words(text) {
  return new Set(text.toLowerCase().match(/[\p{L}\p{N}]+/gu) ?? []);
}

function headingOrder(markdown) {
  return markdown.replace(/\r/g, '').split('\n').map((l) => /^## (.+?)\s*$/.exec(l)?.[1]).filter(Boolean);
}

function stem(word) {
  return word.replace(/'s$/, '').replace(/ies$/, 'y').replace(/(?:s|x|z|ch|sh)es$/, (m) => m.slice(0, -2)).replace(/s$/, '');
}

function contentWords(text) {
  return [...words(text.replace(/'s\b/gi, ''))].filter((w) => !STOPWORDS.has(w));
}

function categoryWords(categoriesText) {
  const lines = categoriesText.split('\n').filter((l) => /^\s*(?:\d+[.)]|[-*])\s+/.test(l));
  return lines.flatMap((l) => contentWords(l.replace(/^\s*(?:\d+[.)]|[-*])\s+/, '').replace(/\([^)]*\)/g, '')));
}

/** Words of a keyword that equal, or share a stem prefix of 4+ letters with, a word KDP forbids repeating. */
function repeatedWords(keyword, used) {
  const usedStems = [...used].map(stem);
  return contentWords(keyword).filter((w) => {
    const ws = stem(w);
    return usedStems.some((u) => u === ws || (Math.min(u.length, ws.length) >= 4 && (u.startsWith(ws) || ws.startsWith(u))));
  });
}

function aiDisclosureErrors(text) {
  const missing = ['Text', 'Images', 'Translations'].filter((k) => !new RegExp(`^\\s*(?:[-*]\\s+)?${k}:\\s*yes\\b`, 'im').test(text));
  return missing.length ? [`AI-generated content must say Yes for text, images and translations (missing: ${missing.join(', ')})`] : [];
}

function priceErrors(priceSection, pages) {
  const price = Number(/\d+(?:\.\d+)?/.exec(priceSection)?.[0]);
  const errors = [];
  if (!(price >= LIMITS.royaltyMinPrice)) errors.push(`price ${price} is under ${LIMITS.royaltyMinPrice} (the 60% royalty starts there)`);
  else if (price > LIMITS.maxPrice) errors.push(`price ${price} is over KDP's maximum ${LIMITS.maxPrice} USD, even with an owner override`);
  else if (printingCost(pages) === null) errors.push(`${pages} pages has no printing cost band (KDP's printing-cost page lists no cost for it; check the Pages section)`);
  else if (!((royalty(price, pages) ?? 0) > 0)) errors.push(`price ${price} gives no positive royalty at ${pages} pages`);
  else {
    const proposed = proposePrice(pages);
    if (price !== proposed && !/owner override/i.test(priceSection)) {
      errors.push(`price ${price} is not the lowest paying price ${proposed} (write "owner override" in the section to keep it)`);
    }
    const expected = royalty(price, pages);
    const shown = (priceSection.match(/(?<![\d.])\d+(?:\.\d+)?/g) ?? []).map(Number);
    if (!shown.some((n) => Math.abs(n - expected) < 0.001)) errors.push(`the sum text does not show the royalty ${royalty(price, pages)} for price ${price}`);
  }
  return errors;
}

export function checkListing(markdown) {
  const errors = [];
  const s = parseSections(markdown);
  for (const h of REQUIRED_HEADINGS) {
    if (!(h in s)) errors.push(`missing heading "## ${h}"`);
  }
  if (errors.length) return errors;
  const order = headingOrder(markdown).filter((h) => REQUIRED_HEADINGS.includes(h));
  if (order.join('|') !== REQUIRED_HEADINGS.join('|')) {
    return [`headings are not in the KDP form's order: ${order.join(' > ')}`];
  }

  const title = s['Book title'];
  const subtitle = s['Subtitle'];
  if (title.length + subtitle.length >= LIMITS.titleAndSubtitleBelow) {
    errors.push(`title + subtitle is ${title.length + subtitle.length} characters, must be fewer than ${LIMITS.titleAndSubtitleBelow}`);
  }
  for (const h of ['Book title', 'Subtitle', 'Authors and contributors', 'Keywords']) {
    if (HTML_TAG.test(s[h])) errors.push(`"${h}" holds an HTML tag, KDP refuses them there`);
  }
  if (s['Description'].length > LIMITS.descriptionMax) {
    errors.push(`description is ${s['Description'].length} characters, the limit is ${LIMITS.descriptionMax}`);
  }

  const keywords = keywordList(s['Keywords']);
  if (keywords.length !== LIMITS.keywordCount) errors.push(`${keywords.length} keywords, KDP takes ${LIMITS.keywordCount}`);
  const seen = new Set();
  const contributors = s['Authors and contributors'].split('\n').map((l) => l.replace(ROLE_LABEL, '')).join('\n');
  const used = new Set([...contentWords(title), ...contentWords(subtitle), ...contentWords(contributors), ...categoryWords(s['Categories'])]);
  for (const k of keywords) {
    if (k.includes('"')) errors.push(`keyword has a quotation mark: ${k}`);
    if (seen.has(k.toLowerCase())) errors.push(`keyword repeated: ${k}`);
    seen.add(k.toLowerCase());
    const dup = repeatedWords(k, used);
    if (dup.length) errors.push(`keyword "${k}" repeats words (or their plural/stem) already in title, subtitle, contributors or categories: ${dup.join(', ')}`);
  }

  const categories = s['Categories'].split('\n').filter((l) => /^\s*(?:\d+[.)]|[-*])\s+/.test(l));
  if (categories.length !== LIMITS.categoryCount) errors.push(`${categories.length} categories, KDP takes ${LIMITS.categoryCount}`);

  const pages = Number(/(\d+)\s*pages\b/i.exec(s['Pages'])?.[1]);
  if (!(pages >= LIMITS.minPages && pages <= LIMITS.maxPages)) {
    errors.push(`page count "${s['Pages']}" is not a number from ${LIMITS.minPages} to ${LIMITS.maxPages}`);
  } else {
    errors.push(...priceErrors(s['List price (USD)'], pages));
  }

  errors.push(...aiDisclosureErrors(s['AI-generated content']));
  return errors;
}
