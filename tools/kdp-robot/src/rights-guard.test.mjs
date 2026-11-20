// The rights radio's live label carries a help link with the word "publishing"; the guard must clear exactly that static text and nothing else.
process.env.KDP_ROBOT_NO_PACE = '1';
import test from 'node:test';
import assert from 'node:assert/strict';
import { writeField } from './fieldio.mjs';
import { PublishRefused, refusal } from './guard.mjs';
import { DETAILS_SPECS } from './pages/details.mjs';

const OWN = 'I own the copyright and I hold necessary publishing rights.';
const LIVE_LABEL = 'I own the copyright and I hold necessary publishing rights. What are publishing rights?';

function radioPage(labelText) {
  const calls = [];
  const loc = {
    calls,
    count: async () => 1,
    innerText: async () => '',
    getAttribute: async () => null,
    evaluate: async () => labelText,
    check: async () => calls.push('check'),
  };
  return { loc, page: { locator: () => loc } };
}

test('the live rights label with its help link is cleared by its static guard label and the radio is checked', async () => {
  const { loc, page } = radioPage(LIVE_LABEL);
  await writeField(page, 'rights', DETAILS_SPECS.rights, OWN);
  assert.deepEqual(loc.calls, ['check']);
});

test('the same radio without the static guard label is refused with today\'s message (the old map)', async () => {
  const { loc, page } = radioPage(LIVE_LABEL);
  const { guardLabels, ...oldSpec } = DETAILS_SPECS.rights;
  await assert.rejects(writeField(page, 'rights', oldSpec, OWN), PublishRefused);
  assert.deepEqual(loc.calls, []);
});

test('a radio whose live text differs from the static guard label is still refused', async () => {
  for (const text of [`${LIVE_LABEL} Publish now`, 'Publish your book', 'I own the copyright and I hold necessary publishing rights. What are publishing rights? Submit']) {
    const { loc, page } = radioPage(text);
    await assert.rejects(writeField(page, 'rights', DETAILS_SPECS.rights, OWN), PublishRefused);
    assert.deepEqual(loc.calls, []);
  }
});

test('guard.refusal still refuses publish text that is not exactly the expected label', () => {
  assert.equal(refusal([LIVE_LABEL], DETAILS_SPECS.rights.guardLabels[OWN]), null);
  assert.match(refusal([LIVE_LABEL], OWN), /Publish or Submit/);
});
