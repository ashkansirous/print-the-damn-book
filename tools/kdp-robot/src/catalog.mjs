// Pure checks for kdp/catalog.json. The filesystem is reached only through `ctx`, so the tests need no folders.
//   ctx.bookIds: Set of book folder names; ctx.titleLangs(id): language codes with a title ('en' plus titles.json keys);
//   ctx.hasListing(id, lang): boolean; ctx.listingPrice(id, lang): number | null.

export const FINISHES = ['matte', 'glossy'];

export function isPublishable(entry) {
  return Array.isArray(entry.languages) && entry.languages.length > 0;
}

/** A bare file name: a string with no path separator and not "." or "..". */
export function isPlainFileName(name) {
  return typeof name === 'string' && !/[\\/]/.test(name) && !name.includes('..') && name !== '.' && name.trim() !== '';
}

export function checkEntryShape(entry, ctx) {
  const errors = [];
  const at = `book "${entry.id}"`;
  if (!ctx.bookIds.has(entry.id)) errors.push(`${at}: unknown book folder`);
  for (const key of ['price', 'finish']) if (key in entry) errors.push(`${at}: ${key} is set once at the top of catalog.json; a book may not override it`);
  if ('ownerWrittenText' in entry && typeof entry.ownerWrittenText !== 'boolean') errors.push(`${at}: ownerWrittenText must be true or false`);
  if (!Array.isArray(entry.languages)) return [...errors, `${at}: languages must be a list`];
  if (!isPublishable(entry)) return errors;
  if (!entry.dino) errors.push(`${at}: dino file name missing`);
  else if (!isPlainFileName(entry.dino)) errors.push(`${at}: dino must be a plain file name, with no folder or ".."`);
  if (!Array.isArray(entry.contributors) || entry.contributors.length === 0) errors.push(`${at}: contributors missing`);
  errors.push(...prefixErrors(entry), ...seriesShapeErrors(entry));
  return errors;
}

export function checkDefaults(catalog) {
  const errors = [];
  if (!(typeof catalog.price === 'number' && Number.isFinite(catalog.price) && catalog.price > 0)) errors.push('catalog.json: price (one for every book) must be a positive number');
  if (!FINISHES.includes(catalog.finish)) errors.push(`catalog.json: finish (one for every book) must be one of ${FINISHES.join(', ')}`);
  return errors;
}

function checkLanguages(entry, price, ctx) {
  const errors = [];
  const known = ctx.bookIds.has(entry.id) ? ctx.titleLangs(entry.id) : [];
  for (const lang of new Set(entry.languages ?? [])) {
    const at = `book "${entry.id}" language "${lang}"`;
    if (!known.includes(lang)) errors.push(`${at}: no title in titles.json`);
    if (!ctx.hasListing(entry.id, lang)) errors.push(`${at}: no listing at kdp/listings/${entry.id}/${lang}.md`);
    else {
      const listed = ctx.listingPrice(entry.id, lang);
      if (listed !== null && price && Math.abs(listed - price) > 0.001) {
        errors.push(`${at}: catalog price ${price} differs from the listing's ${listed}`);
      }
    }
  }
  return errors;
}

/** "series": null, or { "number": 2, "names": { "en": "Series name" } }: one name per language, one number per book (Slice 43, from Slice 40). */
export function seriesShapeErrors(entry) {
  const s = entry.series;
  if (s == null) return [];
  const at = `book "${entry.id}" series`;
  const errors = [];
  if (!Number.isInteger(s.number) || s.number < 1) errors.push(`${at}: number must be a whole number from 1`);
  if (!s.names || typeof s.names !== 'object') return [...errors, `${at}: names must map a language to the series name`];
  for (const lang of entry.languages ?? []) {
    if (typeof s.names[lang] !== 'string' || s.names[lang].trim() === '') errors.push(`${at}: no name for language "${lang}"`);
  }
  return errors;
}

function checkSeries(books) {
  const seen = new Map();
  const errors = [];
  for (const b of books) {
    if (!b.series || !Number.isInteger(b.series.number) || !b.series.names) continue;
    for (const [lang, name] of Object.entries(b.series.names)) {
      const key = `${lang}|${name}#${b.series.number}`;
      if (seen.has(key)) errors.push(`series "${name}" (${lang}) number ${b.series.number} is used by "${seen.get(key)}" and "${b.id}"`);
      else seen.set(key, b.id);
    }
  }
  return errors;
}

export const PREFIX_MAX_LENGTH = 4; // KDP's Prefix box has maxlength 4 (capture 2026-11-20)
/** Ashkan Sirous carries "Mr." as author and as illustrator on every book (owner, 2026-11-20). */
export const REQUIRED_PREFIXES = { 'Ashkan Sirous': 'Mr.' };

/** A contributor's optional "prefix" is a short plain string; a name with a required prefix must carry exactly it. */
export function prefixErrors(entry) {
  const errors = [];
  for (const c of entry.contributors ?? []) {
    const at = `book "${entry.id}" contributor "${c.name}" (${c.role})`;
    if ('prefix' in c && (typeof c.prefix !== 'string' || c.prefix.trim() === '' || c.prefix.length > PREFIX_MAX_LENGTH || /[<>]/.test(c.prefix))) {
      errors.push(`${at}: prefix must be 1 to ${PREFIX_MAX_LENGTH} plain characters`);
    }
    const required = REQUIRED_PREFIXES[c.name];
    if (required && c.prefix !== required) errors.push(`${at}: prefix must be "${required}"`);
  }
  return errors;
}

export function checkCatalog(catalog, ctx) {
  const books = catalog?.books;
  if (!Array.isArray(books)) return ['catalog.json needs a "books" list'];
  const errors = checkDefaults(catalog);
  const ids = new Set();
  for (const b of books) {
    if (ids.has(b.id)) errors.push(`book "${b.id}" appears twice`);
    ids.add(b.id);
    errors.push(...checkEntryShape(b, ctx), ...checkLanguages(b, catalog.price, ctx));
  }
  for (const id of ctx.bookIds) if (!ids.has(id)) errors.push(`book folder "${id}" is not in the catalog`);
  errors.push(...checkSeries(books));
  return errors;
}

export function findBook(catalog, id) {
  return catalog.books.find((b) => b.id === id) ?? null;
}

/** The folder the `kdp` command writes to: tmp/kdp-out/<dino file name without extension>-<lang>. */
export function kdpOutFolder(entry, lang) {
  return `tmp/kdp-out/${entry.dino.replace(/\.dino$/i, '')}-${lang}`;
}
