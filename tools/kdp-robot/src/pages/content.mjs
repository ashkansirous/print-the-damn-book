// "Paperback Content" page (ISBN, print options, manuscript and cover upload), checked against the capture of 2026-11-20 (title-content.html).
// Slice 43 capture run (2026-11-20): KDP covered this page with "We found an issue on an earlier page of your title setup ... Go Back" because the
// title had no categories, so the trim list, the ISBN result and the AI selects could not be opened.
// Slice 43 H (2026-11-21): the trim list, the ISBN confirm dialog, the AI selects, the uploads and the previewer now come from the owner's
// recording (src/pages/recorded/content.json, preview.json, made by `gen-maps`). The page counts as confirmed only while that map holds every
// control `draft` uses (pages/recorded.mjs lists them); there is no hand-set flag.
import { AI_COMBO_NAME, AI_OPTION_ENTIRE, AI_OPTION_NONE, recordedConfirmed } from './recorded.mjs';

export const PAGE = { name: 'content', get confirmed() { return recordedConfirmed('content') && recordedConfirmed('preview'); } };

/** Radios are found by css + value (they have no name of their own: the label text sits beside the input), as on the Details page. */
const radio = (css, values) => ({ kind: 'radio', css, values, options: Object.keys(values) });

export const CONTENT_SPECS = {
  // One question on the real page (input[name=has-ai-content], value yes|no); "Yes" opens the Texts/Images/Translations drop-downs (recording), set by `setAiAnswers` in draft.mjs.
  'ai-content': radio('input[name="has-ai-content"]', { Yes: 'yes', No: 'no' }),
  ink: radio('input[id^="ink-paper-"]', { 'Premium color': 'COLOR_COLOR', 'Standard color': 'COLOR_WHITE', 'Black & white': 'BW_WHITE' }),
  // The trim size is a custom combobox (div role=combobox labelled "Trim Size", showing "6 x 9 in (...)" by default), read from the page text.
  trim: { kind: 'text', label: /trim size/i, readOnly: true },
  bleed: radio('input[id^="bleed-"]', { 'Bleed (PDF only)': 'bleed', 'No Bleed': 'no-bleed' }),
  finish: radio('input[id^="cover-finish-"]', { Matte: 'MATTE', Glossy: 'GLOSSY' }),
};

export const CONTENT = {
  isbnChoice: 'input[name="isbn-type"][value="FREE"]', // "Get a free KDP ISBN", checked by default
  isbnButton: 'Assign ISBN', // button under the free-ISBN radio (capture)
  isbnConfirmButton: 'Assign ISBN', // recording: the click opens a dialog (inDialog) whose button has the same label; pressed inside the dialog only
  dialogScope: '[role=dialog], [aria-modal=true], .a-modal', // the recorder's own test for "inside a dialog"
  previewerButton: 'Launch Previewer', // recording: pressed after both files are in; the previewer page (/print-preview/) has an "Approve" submit
  previewerApprove: 'Approve',
  previewerUrl: /\/print-preview\//,
  aiComboName: AI_COMBO_NAME,
  aiOptions: { None: AI_OPTION_NONE, entire: AI_OPTION_ENTIRE },
  isbnConfirmWaitMs: 3000,
  // Capture 2026-11-20 (title-content-trim): div#select--r9- role=combobox, named "Trim Size" through aria-labelledby, showing the chosen size as text
  // ("6 x 9 in ( 15.24 x 22.86 cm )"); opened, it lists div[role=option] with aria-label "8.5 x 8.5 in ( 21.59 x 21.59 cm )" among others.
  trimSizeCombobox: /trim size/i,
  trimShown: /^\d+(?:\.\d+)? x \d+(?:\.\d+)? in\b/i, // what the combobox and each option show; the guard needs one of the labels the control shows
  trimOption: /^8\.5 x 8\.5 in\b/i,
  // Both file inputs are anonymous (display:none, no id or name); told apart by accept (manuscript has .docx, cover is .pdf only).
  manuscriptInput: 'input[type="file"][accept*=".docx"]',
  coverInput: 'input[type="file"][accept=".pdf"]',
  uploadButtons: { manuscript: 'Upload manuscript', cover: 'Upload your cover file' }, // aria-labels of the visible buttons
  uploadLabels: { manuscript: /manuscript|interior/i, cover: /cover/i }, // what a file input's own label may say
  coverChoice: /upload a cover you already have/i, // radio input[name=cover-choice][value=UPLOAD] (default is COVER_CREATOR)
  saveButton: 'Save as Draft', // next to "Save and Continue" (data-testid=save-and-continue-button), which is never used
  // Visible boxes only: KDP keeps every message template in the DOM under class a-hidden (display:none), capture 2026-11-20.
  messageBoxes: '[role="alert"]:visible, .a-alert-content:visible, .a-alert-container:visible',
  // The "Saving..." and "working" popovers (ids in the 2026-11-20 new-details capture); while one shows, a save has no verdict yet
  // and no read of the page counts. Not confirmed mid-save: KDP may move the popover into another element.
  savingOverlay: '#saving-popover:visible, #working-popover:visible',
  fileCheckTimeoutMs: 15 * 60 * 1000,
  isbnTimeoutMs: 30 * 1000,
};
