// What `draft` needs from each generated page map (src/pages/recorded/<page>.json, made by `gen-maps` from the owner's recording).
// A page counts as confirmed only when its generated map exists and holds every control listed here, so a page the recording never
// reached is not drafted. Slice 43 H: no hand-written `confirmed` flag; review-fixes.test.mjs (test 10) checks these lists against the generated maps.
import { hasControl, readMap } from '../map-gen.mjs';

export const AI_OPTION_ENTIRE = 'Entire work, with minimal or no editing';
export const AI_OPTION_NONE = 'None';
export const AI_COMBO_NAME = 'Select'; // what each AI question's drop-down shows until an answer is picked

/** [role, name or id] of every control the code of that page clicks or fills. */
export const NEEDED = {
  content: [
    ['combobox', 'Trim Size'],
    ['option', /^8\.5 x 8\.5 in\b/],
    ['combobox', AI_COMBO_NAME],
    ['option', AI_OPTION_NONE],
    ['option', AI_OPTION_ENTIRE],
    ['button', 'Assign ISBN'],
    ['button', 'Upload manuscript'],
    ['button', 'Upload your cover file'],
    ['button', 'Launch Previewer'],
    ['button', 'Save and Continue'],
  ],
  preview: [['textbox', 'Approve']],
  pricing: [
    ['textbox', 'price-input-usd'],
    ['button', 'Save as Draft'],
  ],
  series: [], // the series map exists (2026-11-26 recording) but the series is not written: its "Submit updates" button matches the Publish guard
};

/** The needed controls a page's generated map lacks (all of them when there is no map). */
export function missingControls(page, map = readMap(page)) {
  if (!map) return NEEDED[page].map(([role, name]) => `${role} "${name}"`);
  return NEEDED[page].filter(([role, name]) => !hasControl(map, role, name)).map(([role, name]) => `${role} "${name}"`);
}

/** True when the page has a generated map holding every needed control (a page with no needs, like series, needs the map to exist). */
export function recordedConfirmed(page) {
  const map = readMap(page);
  return Boolean(map) && missingControls(page, map).length === 0 && (NEEDED[page].length > 0 || Boolean(map.controls?.length));
}
