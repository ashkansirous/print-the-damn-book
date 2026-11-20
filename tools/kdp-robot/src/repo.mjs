// The filesystem side of the catalog checks: the book folders, titles, listings.
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseListing } from './listing.mjs';

export const REPO_ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
export const DEFAULT_CONTENT = process.env.KDP_ROBOT_CONTENT ?? 'D:/ReadTheStupidText/content/dino';

const readJson = (file) => JSON.parse(readFileSync(file, 'utf8'));

export function loadCatalog(root = REPO_ROOT) {
  return readJson(join(root, 'kdp', 'catalog.json'));
}

export const listingPath = (root, book, lang) => join(root, 'kdp', 'listings', book, `${lang}.md`);

export function readListing(root, book, lang) {
  return parseListing(readFileSync(listingPath(root, book, lang), 'utf8'));
}

export function titleLangs(content, book) {
  const file = join(content, book, 'titles.json');
  return ['en', ...(existsSync(file) ? Object.keys(readJson(file)) : [])];
}

export function titleOf(content, book, lang) {
  if (lang === 'en') return readJson(join(content, book, 'book.json')).title;
  return readJson(join(content, book, 'titles.json'))[lang];
}

export function fsContext(root = REPO_ROOT, content = DEFAULT_CONTENT) {
  const bookIds = new Set(readdirSync(content, { withFileTypes: true }).filter((d) => d.isDirectory()).map((d) => d.name));
  return {
    bookIds,
    titleLangs: (id) => titleLangs(content, id),
    hasListing: (id, lang) => existsSync(listingPath(root, id, lang)),
    listingPrice: (id, lang) => readListing(root, id, lang).price,
  };
}
