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
    // The exact text of each option's live <label> (capture 2026-11-20), help link included: the Publish guard exempts only this static string.
    guardLabels: {
      'I own the copyright and I hold necessary publishing rights.': 'I own the copyright and I hold necessary publishing rights. What are publishing rights?',
      'This is a public domain work.': 'This is a public domain work What is a public domain work?',
    },
  },
  // Hidden inputs that hold the saved series (capture 2026-11-20: empty on a title with no series); read-only, the series is set through SERIES.
  series: { ...byId('text', 'data-print-book-series-title', 'Series'), readOnly: true },
  'series-number': { ...byId('text', 'data-print-book-series-number', 'Number in series'), readOnly: true },
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
  prefix: byId('text', 'data-print-book-primary-author-prefix', 'Prefix'), // maxlength 4, placeholder "Prefix" (capture 2026-11-20)
  first: byId('text', 'data-print-book-primary-author-first-name', 'First name'),
  last: byId('text', 'data-print-book-primary-author-last-name', 'Last name'),
};

/** Contributor rows below the primary author: row 0 exists on the page, "Add Another" adds the next. */
export const contributorSpecs = (row) => ({
  prefix: byId('text', `data-print-book-contributors-${row}-prefix`, 'Prefix'),
  first: byId('text', `data-print-book-contributors-${row}-first-name`, 'First name'),
  last: byId('text', `data-print-book-contributors-${row}-last-name`, 'Last name'),
  role: byId('select', `data-print-book-contributors-${row}-role-native`, 'Role', ROLE_LABELS),
});

export const contributorRowSelector = (row) => `[id="data-print-book-contributors-${row}-first-name"]`;

/** Keywords are seven boxes #data-print-book-keywords-0 .. -6. */
export const keywordSpec = (i) => byId('text', `data-print-book-keywords-${i}`, 'Keyword');

/**
 * Categories dialog, confirmed from the capture of 2026-11-20 (title-categories.html, title-categories-two). The button
 * #categories-modal-button is disabled on an empty new title (needs the first save). The dialog is div[role=dialog] "Categories": a
 * cascade of drop-downs (select react-aui-N: "Children's Books" > "Animals" > "Birds"), a leaf adds no further select; buttons
 * "Reset", "Add another category" (adds a second picker, up to three), "Cancel", "Save categories". The tree has no "Action &
 * Adventure" under Children's Books: the real names are in kdp/listings. NOT shown by any capture: how the saved picks look on the
 * Details page, so `selected` is the whole categories card and a pick counts as set only when a line there reads "Books › A › B › Leaf"
 * (`pathLine`); until a save shows it, `verifyCategories` marks them unverified and kdp/state never records them.
 */
export const CATEGORY = {
  confirmed: true,
  openButton: 'Choose categories',
  addButton: 'Add another category',
  saveButton: 'Save categories',
  dialog: 'dialog',
  select: 'select',
  selectName: /^react-aui-\d+$/, // the drop-downs have no label of their own; this name is their identity for the guard
  emptyOption: 'Select one',
  selectWaitMs: 5000, // how long a drop-down may take to appear after the pick before it
  // After a leaf the dialog shows "Placement" with checkboxes ("0 out of 3 category placements selected"); a pick is kept only with one
  // ticked (screenshot 2026-11-20). The boxes differ per leaf, so the listing names it after "|" on each category line.
  selected: '#section-categories',
  pathLine: /[>›]/,
};

/**
 * Series. Capture 2026-11-20 (title-series, title-series-select, title-series-create, title-series-main): button
 * [data-test-id="add-series-details-button"] opens "Add title to a series" with "Create series" and "Select series" (a search box, "No
 * results" while the account has no series); "Create series" asks "How is this title related to the series?" ("Main content" or
 * "Related content"); "Main content" shows "Go to series setup", which saves the title draft and opens a page that was NOT captured
 * (a read-only capture must not press it). The recording of 2026-11-26 holds the series pages (src/pages/recorded/series.json: "Series
 * title", the Un-ordered radio, "Save as draft", "Go to title setup"), but KDP's series-setup button is "Submit updates", which matches the
 * Publish guard's submit pattern (blocked in the recording). The guard is not loosened, so the series is NOT written: `confirmed` stays
 * false and `draft` marks the series "not set" (open point in Slice 43 item C).
 */
export const SERIES = {
  confirmed: false,
  openButton: '[data-test-id="add-series-details-button"]',
  createButton: 'Create series',
  selectButton: 'Select series',
  mainContentButton: 'Main content',
  setupButton: 'Go to series setup', // not captured past this point; never pressed by `capture`
};
export const CONTRIBUTOR = { addButton: 'Add Another' }; // button#print-book-contributors-list-add-button-announce in the capture

export function splitName(full) {
  const parts = full.trim().split(/\s+/);
  return { first: parts.slice(0, -1).join(' '), last: parts.at(-1) };
}
