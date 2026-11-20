// KDP bookshelf: URLs and selectors. Labels come from KDP's help pages on the manual + Create flow
// (https://kdp.amazon.com/en_US/help/topic/G202172740) and are NOT yet confirmed on the live site; the first live run fixes them here.
export const BOOKSHELF_URL = 'https://kdp.amazon.com/en_US/bookshelf';

export const titleUrl = (kdpId, page) => `https://kdp.amazon.com/en_US/title-setup/paperback/${kdpId}/${page}`;

// `confirmed` becomes true only after this map was checked against a `capture` of the live page; `draft` refuses until then.
export const PAGE = { name: 'bookshelf', confirmed: false };

export const BOOKSHELF = {
  // The row's own status cell (unconfirmed guess); the state is read from this cell's text only, never from the whole row.
  statusCell: '[data-column="status"], [class*="status" i]',
  statusLabels: { in_review: /^(in review|under review)$/i, live: /^live$/i, draft: /^draft$/i },
  signedInMarker: { role: 'button', name: /create/i },
  createButton: 'Create',
  paperbackChoice: 'Paperback',
  // Title links and the row that holds them; the page function in status.mjs reads these into {href, title, text}.
  titleLinks: 'a[href*="title-setup/"]',
};

// A real KDP title id is 10 or more upper-case letters and digits (case-sensitive); the placeholder "new" in
// ".../paperback/new/details" is never an id.
export const KDP_ID = /^[A-Z0-9]{10,}$/;
const ID_IN_URL = /title-setup\/(?:paperback|kindle|hardcover|ebook)\/([A-Z0-9]{10,})(?:[/?#]|$)/;

export function kdpIdFromUrl(url) {
  return ID_IN_URL.exec(url)?.[1] ?? null;
}
