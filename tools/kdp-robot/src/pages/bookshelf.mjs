// KDP bookshelf: URLs and selectors, confirmed from the real page captured 2026-11-20 with the owner signed in (empty bookshelf).
// What the capture did not show (the status cell and title links of a non-empty shelf) is marked "unconfirmed" below and in
// CONTROL_CONFIRMED, and is re-checked by the next capture that has a title on the shelf. The Create page was captured separately.
export const BOOKSHELF_URL = 'https://kdp.amazon.com/en_US/bookshelf';

export const titleUrl = (kdpId, page) => `https://kdp.amazon.com/en_US/title-setup/paperback/${kdpId}/${page}`;

// `confirmed` is true when the controls `draft` needs to start are checked against a `capture` (CONTROL_CONFIRMED lists the rest).
// While `titleLinks` is unconfirmed, `settleTitle` stops before Create when the shelf shows any title that is not this one and
// kdp/state holds no id (the guessed selectors cannot tell a missing title from a miss); an empty shelf still lets the first run proceed.
export const PAGE = { name: 'bookshelf', confirmed: true };

export const BOOKSHELF = {
  // The row's own status cell (unconfirmed guess); the state is read from this cell's text only, never from the whole row.
  statusCell: '[data-column="status"], [class*="status" i]',
  statusLabels: { in_review: /^(in review|under review)$/i, live: /^live$/i, draft: /^draft$/i },
  // Capture 2026-11-20: "+ Create new title or series" is an <a href="/action/dualbookshelf.createnew/create"> styled as a button
  // (role link, not button), so a role=button lookup finds nothing on an empty shelf.
  signedInMarker: { role: 'link', name: /create new title or series/i },
  createButton: '+ Create new title or series',
  // Capture 2026-11-20 of https://kdp.amazon.com/en_US/create (the target of the link above): the paperback card holds
  // <button type="button"><span>Create paperback</span></button> (data-test-id create-new-title-format-card-create-new-card-paperback).
  paperbackChoice: 'Create paperback',
  // Title links and the row that holds them; the page function in status.mjs reads these into {href, title, text}.
  titleLinks: 'a[href*="title-setup/"]',
};

/**
 * Confirmation per control, not per page: true only for a selector checked against a capture. The two false ones need a title on
 * the shelf to capture. Until then `draft` treats "no row for this title" as a new title, and a row it finds whose status it cannot
 * read as exactly "draft" stops the run (`settleTitle`, `assertEditable`).
 */
export const CONTROL_CONFIRMED = { signedInMarker: true, createButton: true, paperbackChoice: true, statusCell: false, titleLinks: false };

// A real KDP title id is 10 or more upper-case letters and digits (case-sensitive); the placeholder "new" in
// ".../paperback/new/details" is never an id.
export const KDP_ID = /^[A-Z0-9]{10,}$/;
const ID_IN_URL = /title-setup\/(?:paperback|kindle|hardcover|ebook)\/([A-Z0-9]{10,})(?:[/?#]|$)/;

export function kdpIdFromUrl(url) {
  return ID_IN_URL.exec(url)?.[1] ?? null;
}
