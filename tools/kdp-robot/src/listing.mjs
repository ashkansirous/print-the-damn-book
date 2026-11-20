// Reads a kdp/listings/<book>/<lang>.md file into plain values. Section names and parsing come from kdp-listing's own module.
import { parseSections } from '../../../.claude/skills/kdp-listing/listing-limits.mjs';

const LIST_MARK = /^\s*(?:\d+[.)]|[-*])\s+/;
const ROLE = /^\s*(?:[-*]\s*)?(author|illustrator|editor|translator|narrator|contributor|photographer)\s*:\s*(.+?)\s*$/i;

function listLines(text) {
  return text.split('\n').filter((l) => LIST_MARK.test(l)).map((l) => l.replace(LIST_MARK, '').trim());
}

function yes(text, key) {
  return new RegExp(`^\\s*(?:[-*]\\s+)?${key}:\\s*yes\\b`, 'im').test(text);
}

/** "Children's Books > Animals > Foxes (or the nearest ...)" -> ["Children's Books", 'Animals', 'Foxes']. */
export function categoryPath(line) {
  return line.replace(/\([^)]*\)/g, '').split('>').map((p) => p.trim()).filter(Boolean);
}

function ages(text) {
  const min = /minimum\s+(\d+)/i.exec(text)?.[1];
  const max = /maximum\s+(\d+)/i.exec(text)?.[1];
  return { ageMin: min ? Number(min) : null, ageMax: max ? Number(max) : null };
}

export function parseListing(markdown) {
  const s = parseSections(markdown);
  const contributors = s['Authors and contributors'].split('\n').map((l) => ROLE.exec(l)).filter(Boolean)
    .map((m) => ({ role: m[1][0].toUpperCase() + m[1].slice(1).toLowerCase(), name: m[2] }));
  const pages = Number(/(\d+)\s*pages\b/i.exec(s['Pages'])?.[1]);
  return {
    language: s['Language'],
    title: s['Book title'],
    subtitle: s['Subtitle'],
    series: s['Series'],
    contributors,
    description: s['Description'],
    keywords: listLines(s['Keywords']),
    categories: listLines(s['Categories']).map(categoryPath),
    ...ages(s['Reading age']),
    ai: { text: yes(s['AI-generated content'], 'Text'), images: yes(s['AI-generated content'], 'Images'), translations: yes(s['AI-generated content'], 'Translations') },
    rights: s['Publishing rights'],
    explicit: /^\s*yes/i.test(s['Sexually explicit images or title']),
    pages: Number.isFinite(pages) ? pages : null,
    price: Number(/\d+(?:\.\d+)?/.exec(s['List price (USD)'])?.[0]) || null,
  };
}
