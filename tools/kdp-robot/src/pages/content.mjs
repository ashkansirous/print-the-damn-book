// "Paperback Content" page (ISBN, print options, manuscript and cover upload). Labels unconfirmed on the live site (see bookshelf.mjs).
// `confirmed` becomes true only after this map was checked against a `capture` of the live page; `draft` refuses until then.
export const PAGE = { name: 'content', confirmed: false };

export const CONTENT_SPECS = {
  ink: { kind: 'radio', group: /interior|ink|paper/i, options: ['Premium color', 'Standard color', 'Black & white'] },
  trim: { kind: 'text', label: /trim size/i, readOnly: true },
  bleed: { kind: 'radio', group: /bleed/i, options: ['Bleed (PDF only)', 'No Bleed'] },
  finish: { kind: 'radio', group: /cover finish/i, options: ['Matte', 'Glossy'] },
};

export const CONTENT = {
  isbnButton: 'Assign me a KDP ISBN',
  isbnConfirmButton: 'Assign ISBN', // the confirm step of the assignment (unconfirmed label); looked for after the first click
  isbnConfirmWaitMs: 3000,
  trimSizeButton: /trim size/i,
  trimOption: /8\.5\s*x\s*8\.5/i,
  manuscriptInput: 'input[type="file"][id*="manuscript" i], input[type="file"][name*="manuscript" i]',
  coverInput: 'input[type="file"][id*="cover" i], input[type="file"][name*="cover" i]',
  uploadLabels: { manuscript: /manuscript|interior/i, cover: /cover/i }, // what a file input's own label may say (unconfirmed)
  coverChoice: /upload a cover you already have/i,
  messageBoxes: '[role="alert"], .a-alert-content, .a-alert-container',
  fileCheckTimeoutMs: 15 * 60 * 1000,
  isbnTimeoutMs: 30 * 1000,
};
