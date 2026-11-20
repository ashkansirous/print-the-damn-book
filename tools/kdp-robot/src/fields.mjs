// The fields the robot sets, built from the listing and the catalog; and the diff of repo values against what KDP shows.
import { createHash } from 'node:crypto';

export const PAGES = ['details', 'content', 'pricing'];

export function normalize(value) {
  return String(value ?? '').replace(/\r/g, '').replace(/[ \t]+/g, ' ').replace(/\n{3,}/g, '\n\n').trim();
}

export function hashValue(value) {
  return createHash('sha256').update(normalize(value)).digest('hex').slice(0, 16);
}

const f = (page, key, label, value) => ({ page, key, label, value: normalize(value) });

function contributorFields(contributors) {
  return contributors.map((c, i) => ({ ...f('details', `contributor-${i + 1}`, `Contributor ${i + 1} (${c.role})`, c.name), role: c.role }));
}

function categoryFields(categories) {
  return categories.map((p, i) => f('details', `category-${i + 1}`, `Category ${i + 1}`, p.join(' > ')));
}

function keywordFields(keywords) {
  return keywords.map((k, i) => f('details', `keyword-${i + 1}`, `Keyword ${i + 1}`, k));
}

/** Ordered list of every field one run sets. Series is Slice 40 and is left alone on purpose. */
export function buildFields(listing, entry) {
  return [
    f('details', 'language', 'Language', listing.language),
    f('details', 'title', 'Book title', listing.title),
    f('details', 'subtitle', 'Subtitle', listing.subtitle),
    ...contributorFields(entry.contributors),
    f('details', 'description', 'Description', listing.description),
    f('details', 'rights', 'Publishing rights', listing.rights),
    ...keywordFields(listing.keywords),
    ...categoryFields(listing.categories),
    f('details', 'age-min', 'Minimum reading age', listing.ageMin),
    f('details', 'age-max', 'Maximum reading age', listing.ageMax),
    f('details', 'explicit', 'Sexually explicit images or title', listing.explicit ? 'Yes' : 'No'),
    f('content', 'ai-text', 'AI-generated text', listing.ai.text ? 'Yes' : 'No'),
    f('content', 'ai-images', 'AI-generated images', listing.ai.images ? 'Yes' : 'No'),
    f('content', 'ai-translations', 'AI-generated translations', listing.ai.translations ? 'Yes' : 'No'),
    f('content', 'isbn', 'ISBN', 'Free KDP ISBN'),
    f('content', 'ink', 'Interior and paper type', 'Premium color'),
    f('content', 'trim', 'Trim size', '8.5 x 8.5 in'),
    f('content', 'bleed', 'Bleed settings', 'Bleed (PDF only)'),
    f('content', 'finish', 'Paperback cover finish', entry.finish === 'glossy' ? 'Glossy' : 'Matte'),
    f('pricing', 'territories', 'Territories', 'All territories (worldwide rights)'),
    f('pricing', 'price-us', 'List price Amazon.com (USD)', entry.price),
  ];
}

/** Whitespace-insensitive form: the rich editor wraps and re-spaces paragraphs, so only the words are compared. */
const squash = (value) => normalize(value).replace(/\s+/g, ' ');

/** Compare desired fields with what KDP shows now (live: key -> text). status: same | changed | new. */
export function diffFields(desired, live) {
  return desired.map((d) => {
    const old = live[d.key];
    const status = old == null || normalize(old) === '' ? 'new' : squash(old) === squash(d.value) ? 'same' : 'changed';
    return { ...d, old: old == null ? '' : normalize(old), status };
  });
}

export function changedOnly(diff) {
  return diff.filter((d) => d.status !== 'same');
}

export function fieldHashes(fields) {
  return Object.fromEntries(fields.map((x) => [x.key, hashValue(x.value)]));
}
