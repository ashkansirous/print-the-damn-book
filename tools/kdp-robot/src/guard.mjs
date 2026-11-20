// The Publish guard. The robot never publishes: a control whose name looks like publish/submit in any KDP UI language
// is refused, and only the buttons in ALLOWED_BUTTONS may be clicked at all.

const PUBLISH_WORDS = [
  /publish/i, /submi(t|ss)/i, /ver(ö|o)ffentlich/i, /publi(er|ca|ez)/i, /pubblica/i, /publiceer/i, /publiceren/i, /publicer/i,
  /publicera/i, /julkaise/i, /opublikuj/i, /publikuj/i, /yay[ıi]nla/i, /опубликов/i, /отправить/i, /nach\s+pr(ü|u)fung/i,
  /soumettre/i, /enviar/i, /invia/i, /indienen/i, /absenden/i, /einreichen/i, /出版/, /公開/, /発行/, /发布/, /發布/, /提交/, /게시/, /نشر/,
];

export const ALLOWED_BUTTONS = [
  'Save as Draft',
  'Save and Continue',
  'Choose categories',
  'Save categories',
  'Create',
  'Paperback',
  'Create paperback',
  'Assign me a KDP ISBN',
  'Assign ISBN',
  'Cancel',
  'Add another contributor',
];

export class PublishRefused extends Error {
  constructor(name, reason) {
    super(`Refused to click "${name}": ${reason}`);
    this.name = 'PublishRefused';
  }
}

export function looksLikePublish(name) {
  return PUBLISH_WORDS.some((re) => re.test(String(name ?? '')));
}

const squash = (t) => String(t ?? '').replace(/\s+/g, ' ').trim();

/**
 * THE only exemption logic. `controlTexts` are the texts the page shows for the control (visible text, aria-label, value,
 * wrapping label), never the robot's own intended name. Returns a refusal reason, or null. A text containing publish/submit
 * is refused unless `expectedLabel` is a string and the text equals it exactly (case-insensitive). `expectedLabel` must be a
 * literal from the robot's own static page maps or ALLOWED_BUTTONS; nothing read from the page may be passed as one.
 */
export function refusal(controlTexts, expectedLabel) {
  const expected = typeof expectedLabel === 'string' ? squash(expectedLabel).toLowerCase() : '';
  for (const t of controlTexts ?? []) {
    if (!looksLikePublish(t)) continue;
    if (expected !== '' && squash(t).toLowerCase() === expected) continue;
    return `the control the page resolved to looks like a Publish or Submit control: "${squash(t).slice(0, 80)}"`;
  }
  return null;
}

/** Throws PublishRefused unless `refusal` clears the texts with no label or with one of the static string labels in `allowed`. */
export function assertNotPublish(controlTexts, allowed = []) {
  const labels = (allowed ?? []).filter((a) => typeof a === 'string');
  const reasons = [refusal(controlTexts, undefined), ...labels.map((l) => refusal(controlTexts, l))];
  if (reasons.some((r) => r === null)) return;
  throw new PublishRefused(squash((controlTexts ?? []).find((t) => looksLikePublish(t))).slice(0, 80), reasons[0]);
}

/** Whether one resolved text is one of the allowed labels: a string must equal it (case-insensitive), a RegExp must match it. */
function textIsAllowed(text, allowed) {
  const t = squash(text);
  if (!t) return false;
  return allowed.some((a) => (a instanceof RegExp ? a.test(t) : squash(a).toLowerCase() === t.toLowerCase()));
}

/**
 * On top of the Publish-word refusal: the control the page resolved to must show a label from `allowed` (ALLOWED_BUTTONS or a
 * page map's named field). `texts[0]` is the robot's own intended name and does not count; a control that shows none of the
 * allowed labels (a "Go live" button behind a trim-size locator) is refused.
 */
export function assertAllowedControl(texts, allowed) {
  if (!Array.isArray(allowed) || allowed.length === 0) throw new PublishRefused('(no allowed label given)', 'every click needs the labels it may land on');
  const shown = texts.slice(1);
  if (shown.some((t) => textIsAllowed(t, allowed))) return;
  const first = shown.map(squash).find(Boolean) ?? String(texts[0] ?? '');
  throw new PublishRefused(first.slice(0, 80), 'the control the page resolved to is not one of the labels the robot may use here');
}

export function assertSafeClick(name) {
  if (looksLikePublish(name)) throw new PublishRefused(name, 'it looks like a Publish or Submit control');
  if (!ALLOWED_BUTTONS.some((a) => a.toLowerCase() === String(name).trim().toLowerCase())) {
    throw new PublishRefused(name, 'it is not one of the buttons the robot may click');
  }
}
