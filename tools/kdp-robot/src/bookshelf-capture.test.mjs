// The bookshelf reader against markup captured from the real KDP bookshelf on 2026-11-20 (one draft paperback; name, id and
// title replaced, scripts, popover actions and styling dropped). Runs the real page function in headless Chromium; skipped
// when Playwright's Chromium is not installed.
import test from 'node:test';
import assert from 'node:assert/strict';
import { readShelfRowsInPage } from './commands.mjs';
import { parseBookshelfRows, matchRows } from './bookshelf.mjs';
import { BOOKSHELF } from './pages/bookshelf.mjs';

const ID = 'ABCDE12345';
const SHELF_HTML = `<table class="mt-table refreshedbookshelftable"><tbody>
<tr id="${ID}" data-test-id="refreshedbookshelftable-p1r1" class="mt-row"><td data-column="cover">No Cover Uploaded</td>
<td id="${ID}-formats" data-column="formats"><div class="pod_bookshelf_formats_cell">
<div class="bookshelf-nested-table-digital"><table><tbody>
<tr id="${ID}" data-test-id="titlesetheadertable-p0r1"><td data-column="itemset-metadata">
<span id="zme-indie-bookshelf-dual-itemset-itemset-metadata-series_title-${ID}"> </span>
<span id="zme-indie-bookshelf-dual-itemset-itemset-metadata-title-${ID}"> Fox Tale!: A Picture Book About a Fox </span>
<span id="zme-indie-bookshelf-dual-itemset-itemset-metadata-author-${ID}"> by Test Author </span></td>
<td data-column="itemset-actions-column"><a href="javascript:void(0)" role="button">Manage title</a></td></tr></tbody></table></div>
<div class="bookshelf-nested-table-digital"><table><tbody><tr id="${ID}" data-test-id="refreshedbookshelfnestedtable-p0r1">
<td data-column="missing_print">Kindle eBook <button type="button">+ Create Kindle eBook</button></td></tr></tbody></table></div>
<div class="bookshelf-nested-table-print"><table><tbody><tr id="${ID}" data-test-id="refreshedbookshelfnestedtable-p0r1">
<td id="${ID}-status" data-column="status"><div class="bookshelf-status-column">
<span id="zme-indie-bookshelf-dual-print-status-format-${ID}" class="a-text-bold"> Paperback </span>
<a href="javascript:void(0)" role="button" class="a-popover-trigger"><span id="zme-indie-bookshelf-dual-print-status-draft-status-popover-${ID}-label" class="a-size-base a-color-base bookshelftable-status-popover-trigger a-text-bold"> Draft </span></a>
<span id="zme-indie-bookshelf-dual-print-status-last-edit-date-${ID}"> Last modified on November 20, 2026 </span></div></td>
<td data-column="actions"><div class="a-popover-preload"><a id="print_edit_content-E1" class="format-action" href="/action/dualbookshelf.editpaperbackcontent/en_US/title-setup/paperback/${ID}/content?ref_=kdp_BS_D_ta_co">Edit paperback content</a></div></td>
</tr></tbody></table></div></div></td></tr></tbody></table>`;

async function readFixture() {
  const { chromium } = await import('playwright');
  const browser = await chromium.launch({ headless: true });
  try {
    const page = await browser.newPage({ baseURL: 'https://kdp.amazon.com' });
    await page.setContent(SHELF_HTML);
    return await page.$$eval(BOOKSHELF.rows, readShelfRowsInPage, BOOKSHELF);
  } finally {
    await browser.close();
  }
}

test('captured bookshelf row: title with subtitle, paperback id from the content link, state draft', async (t) => {
  let raw;
  try {
    raw = await readFixture();
  } catch (e) {
    t.skip(`no Chromium for Playwright: ${e.message.split('\n')[0]}`);
    return;
  }
  assert.equal(raw.length, 1, 'one row per title, not one per nested table');
  const rows = parseBookshelfRows(raw);
  assert.deepEqual(rows, [{ kdpId: ID, format: 'paperback', title: 'Fox Tale!: A Picture Book About a Fox', language: 'unknown', state: 'draft' }]);
  const { matched } = matchRows(rows, { books: [{ id: 'fox', languages: ['en'] }] }, () => 'Fox Tale!');
  assert.equal(matched.length, 1, 'the shelf shows no language and a "Title: Subtitle" title still matches the listing title');
});
