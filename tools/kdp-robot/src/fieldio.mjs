// Finds one KDP field by its visible label and role, reads it, fills it. A field that is missing (or matches more than once)
// stops the run with the field's name; nothing is ever typed into another field.
import { guardField, safeCheck } from './browser.mjs';

export class FieldNotFound extends Error {
  constructor(field, found) {
    super(`Field "${field}" not found on the page (matches: ${found}); run stopped, nothing was typed elsewhere`);
    this.name = 'FieldNotFound';
    this.field = field;
  }
}

function scopeOf(page, spec) {
  return spec.group ? page.getByRole('group', { name: spec.group }) : page;
}

export async function single(locator, name) {
  const n = await locator.count();
  if (n !== 1) throw new FieldNotFound(name, n);
  return locator;
}

/** KDP's text boxes mostly have no accessible name: a spec with `css` (a static id/name selector from a capture) is found by it, never by label. */
async function locateByCss(page, key, spec, value) {
  if (spec.kind === 'rich') return single(page.frameLocator(spec.frame).locator('body'), key);
  if (spec.kind === 'radio') {
    const raw = spec.values[value];
    if (raw == null) throw new FieldNotFound(`${key} = ${value} (no such option in the page map)`, 0);
    return single(page.locator(`${spec.css}[value="${raw}"]`), `${key} = ${value}`);
  }
  return single(page.locator(spec.css), key);
}

async function locate(page, key, spec, value) {
  if (spec.css || spec.frame) return locateByCss(page, key, spec, value);
  if (spec.kind === 'radio') return single(scopeOf(page, spec).getByRole('radio', { name: value, exact: true }), `${key} = ${value}`);
  const all = page.getByLabel(spec.label);
  if (spec.nth == null) return single(all, key);
  const n = await all.count();
  if (n <= spec.nth) throw new FieldNotFound(`${key} (#${spec.nth + 1})`, n);
  return all.nth(spec.nth);
}

export async function readField(page, key, spec) {
  if (spec.kind === 'radio') {
    for (const option of spec.options) {
      const radio = spec.css ? page.locator(`${spec.css}[value="${spec.values[option]}"]`) : scopeOf(page, spec).getByRole('radio', { name: option, exact: true });
      if ((await radio.count()) === 1 && (await radio.isChecked())) return option;
    }
    return '';
  }
  const loc = await locate(page, key, spec);
  if (spec.kind === 'select') return optionValue(spec, (await loc.locator('option:checked').first().innerText()).trim());
  if (spec.kind === 'rich') return loc.innerText();
  return loc.inputValue();
}

/** A page map's label (text or RegExp) as a RegExp that matches the label text the page shows, which may carry a "*" or a hint. */
export function fieldLabel(label) {
  if (label instanceof RegExp) return label;
  return new RegExp(String(label).replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'i');
}

/** Fill, select and radio all pass the Publish guard first, like every click. */
export async function writeField(page, key, spec, value) {
  const loc = await locate(page, key, spec, value);
  // A radio's group name is shared by every radio of the group, so it is never an allowed identity: its own value attribute is.
  if (spec.kind === 'radio') return safeCheck(loc, `${key} = ${value}`, [value, spec.values?.[value]].filter((t) => t != null));
  await guardField(loc, key, [fieldLabel(spec.label), ...(spec.ref ? [spec.ref] : [])]);
  if (spec.css) await assertShownLabel(loc, key, spec);
  // KDP's native selects sit under a styled dropdown, so the click-ability checks are skipped (the guard above already ran),
  // but a locked select is refused here instead of being "set" with no effect.
  if (spec.kind === 'select') {
    if (!(await loc.isEnabled())) throw new FieldNotFound(`${key} (the select is disabled)`, 1);
    await loc.selectOption({ label: optionLabel(spec, value) }, { force: true });
  } else if (spec.kind === 'rich') await writeParagraphs(loc, value);
  else await loc.fill(value);
}

/** The catalog's value as the page's option text, for the few options whose text differs (age 0 is "Baby", role "contributor" is "Contributions by"). */
export function optionLabel(spec, value) {
  return spec.labels?.[String(value).toLowerCase()] ?? String(value);
}

/** The catalog value for the option text the page shows (the reverse of `optionLabel`); other texts are returned as they are. */
export function optionValue(spec, shown) {
  const hit = Object.entries(spec.labels ?? {}).find(([, label]) => label === shown);
  return hit ? hit[0] : shown;
}

/** The rich editor gets one paragraph per line of the value: fill() writes the first, Enter and typed text the rest. */
async function writeParagraphs(loc, value) {
  const paragraphs = String(value).split(/\n+/);
  await loc.fill(paragraphs[0]);
  for (const p of paragraphs.slice(1)) {
    await loc.press('Enter');
    await loc.pressSequentially(p);
  }
}

/** The label the page itself shows for a control found by id (aria-label, `label[for]`, wrapping label): when there is one it must match the page map's label. A control with no label of its own is accepted by its id. */
async function assertShownLabel(loc, key, spec) {
  if (spec.kind === 'rich' || spec.kind === 'radio') return;
  const shown = (await loc.evaluate((el) => {
    const byFor = el.id ? el.ownerDocument.querySelector(`label[for="${CSS.escape(el.id)}"]`) : null;
    return el.getAttribute('aria-label') || byFor?.innerText || el.closest('label')?.innerText || '';
  })).trim();
  if (shown && !fieldLabel(spec.label).test(shown)) throw new FieldNotFound(`${key} (the page labels it "${shown.slice(0, 60)}", not "${spec.label}")`, 1);
}

/** Read every spec'd field of a page into key -> text. A field KDP does not show yet reads as missing (left out), never as a stop. */
export async function readFields(page, keys, specs) {
  const live = {};
  for (const key of keys) {
    const spec = specs[key];
    if (!spec) continue;
    try {
      live[key] = await readField(page, key, spec);
    } catch (e) {
      if (!(e instanceof FieldNotFound)) throw e;
    }
  }
  return live;
}
