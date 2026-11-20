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

async function locate(page, key, spec, value) {
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
      const radio = scopeOf(page, spec).getByRole('radio', { name: option, exact: true });
      if ((await radio.count()) === 1 && (await radio.isChecked())) return option;
    }
    return '';
  }
  const loc = await locate(page, key, spec);
  if (spec.kind === 'select') return (await loc.locator('option:checked').first().innerText()).trim();
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
  if (spec.kind === 'radio') return safeCheck(loc, `${key} = ${value}`, [value]);
  await guardField(loc, key, [fieldLabel(spec.label)]);
  if (spec.kind === 'select') await loc.selectOption({ label: value });
  else await loc.fill(value);
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
