// "Paperback Details" page. One entry per field key from fields.mjs; kind: text | textarea | select | radio.
// Labels follow KDP's help pages for the manual flow and are unconfirmed on the live site (see bookshelf.mjs).
const YES_NO = ['Yes', 'No'];

// `confirmed` becomes true only after this map was checked against a `capture` of the live page; `draft` refuses until then.
export const PAGE = { name: 'details', confirmed: false };

export const DETAILS_SPECS = {
  language: { kind: 'select', label: 'Language' },
  title: { kind: 'text', label: 'Book Title' },
  subtitle: { kind: 'text', label: 'Subtitle' },
  description: { kind: 'textarea', label: 'Book Description' },
  rights: { kind: 'radio', group: /publishing rights/i, options: ['I own the copyright and I hold necessary publishing rights.', 'This is a public domain work.'] },
  'age-min': { kind: 'select', label: /minimum age/i },
  'age-max': { kind: 'select', label: /maximum age/i },
  'ai-text': { kind: 'radio', group: /AI-generated text/i, options: YES_NO },
  'ai-images': { kind: 'radio', group: /AI-generated images/i, options: YES_NO },
  'ai-translations': { kind: 'radio', group: /AI-generated translations/i, options: YES_NO },
  explicit: { kind: 'radio', group: /sexually explicit/i, options: YES_NO },
};

/** Contributor rows: KDP has First/Last name boxes and a role select per row; row i is the i-th match of each label. */
export const contributorSpecs = (i) => ({
  first: { kind: 'text', label: /first name/i, nth: i },
  last: { kind: 'text', label: /last name/i, nth: i },
  role: { kind: 'select', label: /^\s*(contributor\s+)?role\s*$/i, nth: i },
});

export const keywordSpec = (i) => ({ kind: 'text', label: /keyword/i, nth: i });

export const CATEGORY = {
  openButton: 'Choose categories',
  saveButton: 'Save categories',
  dialog: 'dialog',
  selected: '[data-selected-category], .selected-category, [id*="selected-categories" i] li', // unconfirmed; none found reads as "not set"
};
export const CONTRIBUTOR = { addButton: 'Add another contributor', firstName: /first name/i };

export function splitName(full) {
  const parts = full.trim().split(/\s+/);
  return { first: parts.slice(0, -1).join(' '), last: parts.at(-1) };
}
