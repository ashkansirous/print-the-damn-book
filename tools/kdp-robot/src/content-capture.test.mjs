// The content and pricing page maps against markup captured from the real KDP pages on 2026-11-20 (controls only; styling, scripts, title
// and author dropped). Runs in headless Chromium; skipped when Playwright's Chromium is not installed.
import test from 'node:test';
import assert from 'node:assert/strict';
import { CONTENT, CONTENT_SPECS, PAGE as CONTENT_PAGE } from './pages/content.mjs';
import { PRICING_SPECS, PAGE as PRICING_PAGE } from './pages/pricing.mjs';

const r = (attrs) => `<label><input type="radio" ${attrs}></label>`;
const CONTENT_HTML = `
${r('name="isbn-type" aria-checked="true" value="FREE" checked')}<button type="button">Assign ISBN</button>${r('name="isbn-type" value="OWNER"')}
${['BW_WHITE', 'BW_CREAM', 'BW_GROUNDWOOD', 'COLOR_WHITE', 'COLOR_COLOR'].map((v) => r(`id="ink-paper-${v}" value="${v}"`)).join('')}
<div role="combobox" aria-labelledby="tl" id="select--r9-"></div><label id="tl" for="select--r9-">Trim Size 6 x 9 in</label>
${r('id="bleed-no" value="no-bleed" checked')}${r('id="bleed-yes" value="bleed"')}
${r('id="cover-finish-MATTE" value="MATTE" checked')}${r('id="cover-finish-GLOSSY" value="GLOSSY"')}
<input accept=".azw,.doc,.docx,.epub,.htm,.html,.pdf,.rtf,.txt,.zip,.kpf" type="file" style="display:none">
<button type="button" aria-label="Upload manuscript">Upload manuscript</button>
${r('name="cover-choice" value="COVER_CREATOR" checked')}${r('name="cover-choice" value="UPLOAD"')}
<input accept=".pdf" type="file" style="display:none"><button type="button" aria-label="Upload your cover file">Upload your cover file</button>
${r('name="has-ai-content" value="yes"')}${r('name="has-ai-content" value="no"')}
<button type="button">Save as Draft</button><button type="button" data-testid="save-and-continue-button">Save and Continue</button>`;
const PRICING_HTML = `${r('id="worldwide-rights" name="territory-selection-type" value="worldwide"')}${r('id="individual-territories" name="territory-selection-type" value="individual"')}<button type="button">Save as Draft</button>`;

async function withPage(html, fn) {
  const { chromium } = await import('playwright');
  const browser = await chromium.launch({ headless: true });
  try {
    const page = await browser.newPage();
    await page.setContent(html);
    return await fn(page);
  } finally {
    await browser.close();
  }
}

const hasChromium = await import('playwright').then(async ({ chromium }) => { try { await (await chromium.launch()).close(); return true; } catch { return false; } }, () => false);

test('content map: every radio option, the two file inputs and the save button are found exactly once in the captured markup', { skip: !hasChromium }, async () => {
  await withPage(CONTENT_HTML, async (page) => {
    for (const [key, spec] of Object.entries(CONTENT_SPECS)) {
      if (!spec.css) continue;
      for (const [option, raw] of Object.entries(spec.values)) assert.equal(await page.locator(`${spec.css}[value="${raw}"]`).count(), 1, `${key} = ${option}`);
    }
    assert.equal(await page.locator(CONTENT.manuscriptInput).count(), 1);
    assert.equal(await page.locator(CONTENT.coverInput).count(), 1);
    assert.equal(await page.locator(CONTENT.isbnChoice).count(), 1);
    assert.equal(await page.locator('input[name="cover-choice"][value="UPLOAD"]').count(), 1);
    for (const name of [CONTENT.isbnButton, CONTENT.saveButton, CONTENT.uploadButtons.manuscript, CONTENT.uploadButtons.cover]) {
      assert.equal(await page.getByRole('button', { name, exact: true }).count(), 1, name);
    }
    assert.equal(await page.getByRole('combobox', { name: CONTENT_SPECS.trim.label }).count(), 1);
  });
});

test('pricing map: the territory radios are found; the page is confirmed from the recording (price input and Save as Draft)', { skip: !hasChromium }, async () => {
  await withPage(PRICING_HTML, async (page) => {
    const spec = PRICING_SPECS.territories;
    for (const raw of Object.values(spec.values)) assert.equal(await page.locator(`${spec.css}[value="${raw}"]`).count(), 1);
  });
  assert.equal(PRICING_PAGE.confirmed, true);
  assert.equal(CONTENT_PAGE.confirmed, true);
});
