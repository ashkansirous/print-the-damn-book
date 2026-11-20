// "Paperback Details" page, confirmed from the real page captured 2026-11-20 (.../title-setup/paperback/new/details, owner signed in).
// One entry per field key from fields.mjs; kind: text | select | radio | rich. Most KDP boxes have no accessible name or linked
// label, so each spec finds its control by the stable id/name from the capture (`css`, with `ref` = that id/name, which the guard
// accepts as the control's identity). The AI-generated-content questions are not on this page (0 matches in the capture); they
// are in content.mjs.
const YES_NO = ['Yes', 'No'];

// `confirmed` is true only for a map checked against a `capture` of the live page; `draft` refuses until then.
export const PAGE = { name: 'details', confirmed: true };

const byId = (kind, ref, label, labels) => ({ kind, css: `[id="${ref}"]`, ref, label, ...(labels ? { labels } : {}) });

// Option texts from the capture of 2026-11-20: the age selects list "Select one", "Baby" (value 0), then 1..17; the role select's
// last option is "Contributions by" (value "contributor"). Keys are lower case catalog values.
const AGE_LABELS = { 0: 'Baby' };
const ROLE_LABELS = { contributor: 'Contributions by' };

export const DETAILS_SPECS = {
  language: byId('select', 'data-print-book-language-native', 'Language'),
  title: byId('text', 'data-print-book-title', 'Book Title'),
  subtitle: byId('text', 'data-print-book-subtitle', 'Subtitle'),
  // CKEditor iframe titled "Rich Text Editor, editor1" (4000 character limit); the guard reads the frame title as its identity.
  description: { kind: 'rich', frame: 'iframe[title="Rich Text Editor, editor1"]', ref: 'Rich Text Editor, editor1', label: 'Book Description' },
  rights: {
    kind: 'radio', css: 'input[name="data-print-book-is-public-domain"]', ref: 'data-print-book-is-public-domain',
    values: { 'I own the copyright and I hold necessary publishing rights.': 'false', 'This is a public domain work.': 'true' },
    options: ['I own the copyright and I hold necessary publishing rights.', 'This is a public domain work.'],
  },
  'age-min': byId('select', 'data-print-book-reading-interest-age-start-input-native', 'Minimum age', AGE_LABELS),
  'age-max': byId('select', 'data-print-book-reading-interest-age-end-input-native', 'Maximum age', AGE_LABELS),
  // The "adult content" radio pair: true = Yes, false = No.
  explicit: {
    kind: 'radio', css: 'input[name="data[print_book][is_adult_content]-radio"]', ref: 'data[print_book][is_adult_content]-radio',
    values: { Yes: 'true', No: 'false' }, options: YES_NO,
  },
};

/** The first Author in the catalog is KDP's "Primary Author" (first/last name boxes). */
export const PRIMARY_AUTHOR = {
  first: byId('text', 'data-print-book-primary-author-first-name', 'First name'),
  last: byId('text', 'data-print-book-primary-author-last-name', 'Last name'),
};

/** Contributor rows below the primary author: row 0 exists on the page, "Add Another" adds the next. */
export const contributorSpecs = (row) => ({
  first: byId('text', `data-print-book-contributors-${row}-first-name`, 'First name'),
  last: byId('text', `data-print-book-contributors-${row}-last-name`, 'Last name'),
  role: byId('select', `data-print-book-contributors-${row}-role-native`, 'Role', ROLE_LABELS),
});

export const contributorRowSelector = (row) => `[id="data-print-book-contributors-${row}-first-name"]`;

/** Keywords are seven boxes #data-print-book-keywords-0 .. -6. */
export const keywordSpec = (i) => byId('text', `data-print-book-keywords-${i}`, 'Keyword');

export const CATEGORY = {
  // false until the dialog is captured: `draft` then skips the categories (a message says so) and never opens the dialog.
  confirmed: false,
  // Capture 2026-11-20: button#categories-modal-button "Choose categories" is present but DISABLED on the empty new title; the dialog
  // and its buttons below were not captured (unconfirmed).
  openButton: 'Choose categories',
  saveButton: 'Save categories',
  dialog: 'dialog',
  selected: '[data-selected-category], .selected-category, [id*="selected-categories" i] li', // unconfirmed; none found reads as "not set"
};
export const CONTRIBUTOR = { addButton: 'Add Another' }; // button#print-book-contributors-list-add-button-announce in the capture

export function splitName(full) {
  const parts = full.trim().split(/\s+/);
  return { first: parts.slice(0, -1).join(' '), last: parts.at(-1) };
}
