// `record`: the owner fills a title in by hand in the robot's browser; every control used is written to kdp/recordings/<book>-<lang>.json,
// in order. Page maps are generated from that file (map-gen.mjs). Slice 43 G. Read-only for KDP: the Publish guard still blocks.
import { existsSync, mkdirSync, readdirSync, readFileSync, renameSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { PUBLISH_WORDS, looksLikePublish } from './guard.mjs';
import { ensureKdpPage, log, openSession } from './browser.mjs';
import { titleUrl } from './pages/bookshelf.mjs';
import { readState } from './state.mjs';
import { pageKindOfPath } from './map-gen.mjs';

export const BINDING = '__kdpRecord';
export const KDP_PAGES = ['details', 'content', 'pricing'];
const VALUE_MAX = 200;

export const recordingPath = (root, book, lang) => join(root, 'kdp', 'recordings', `${book}-${lang}.json`);

export { pageKindOfPath };

export function newRecording(book, lang, kdpId) {
  return { book, lang, kdpId, version: 1, events: [] };
}

export const REDACTED = '[redacted]';
// Review kdpx-high 4: a recording never keeps what the owner types at Amazon's sign-in or verification pages, nor in a password or
// one-time-code box. The event stays (its order and control are what a map needs); only the value is replaced.
export const SECRET_PATH = /\/(?:ap|ax)\/|sign-?in|\bmfa\b|\/cvf\b|challenge|captcha|\/claim\b/i;
export const SECRET_FIELD = /(?<![a-z])pass(?:word|code|wd)?|\botp\b|one[-_ ]?time|verification|\b2fa\b|\bmfa\b|auth(?:entication)?[-_ ]?code|security[-_ ]?code|captcha|\bcode\b|\bpin\b|e-?mail|\bcvf\b|\bap[_-]/i;

/** True when the value of this event must not be kept: a password box, a secret-looking field name, a sign-in page or a host other than KDP's. */
export function isSecretEvent(event, url = '') {
  let host = 'kdp.amazon.com';
  try {
    host = new URL(url).hostname;
  } catch { /* a bare path: judged by the path alone */ }
  const named = [event.name, event.label, event.id, event.field, event.inputType].map((t) => String(t ?? '')).join(' ');
  return event.inputType === 'password' || SECRET_PATH.test(event.path ?? '') || host !== 'kdp.amazon.com' || SECRET_FIELD.test(named);
}

/** Adds one event (order = its place in the list). A file input keeps its file name only; text values are cut at 200 characters; secret values are redacted. */
export function appendEvent(recording, raw, url = '') {
  const path = (() => {
    try {
      return new URL(url).pathname;
    } catch {
      return String(url);
    }
  })();
  const event = { order: recording.events.length, page: pageKindOfPath(path), path, ...raw };
  if (event.kind !== 'click' && event.kind !== 'upload' && event.value != null && isSecretEvent(event, url)) {
    if (event.name === String(event.value)) event.name = ''; // an unlabelled box is named by its own value
    event.value = REDACTED;
  }
  if (event.value != null) event.value = String(event.value).slice(0, VALUE_MAX);
  if (event.kind === 'upload') event.value = String(event.value ?? '').split(/[\\/]/).pop();
  if (!event.blocked && [event.name, event.label, event.value, event.id].some((t) => looksLikePublish(t)) && event.kind === 'click') event.blocked = true;
  recording.events.push(event);
  return event;
}

export function writeRecording(file, recording) {
  mkdirSync(dirname(file), { recursive: true });
  const temp = `${file}.${process.pid}.tmp`;
  writeFileSync(temp, `${JSON.stringify(recording, null, 2)}\n`);
  renameSync(temp, file);
}

export function readRecording(file) {
  if (!existsSync(file)) return null;
  return JSON.parse(readFileSync(file, 'utf8'));
}

/** Every recording of a book and language: `<book>-<lang>.json` and older ones kept as `<book>-<lang>-<date>.json`, dated (older) first. */
export function readRecordings(root, book, lang) {
  const dir = dirname(recordingPath(root, book, lang));
  if (!existsSync(dir)) return [];
  const main = `${book}-${lang}.json`;
  const files = readdirSync(dir).filter((f) => f === main || (f.startsWith(`${book}-${lang}-`) && f.endsWith('.json')));
  files.sort((a, b) => (a === main) - (b === main) || a.localeCompare(b));
  return files.map((f) => JSON.parse(readFileSync(join(dir, f), 'utf8')));
}

/** The script injected into every frame before its own scripts run. Capture-phase listeners; a Publish click is stopped and reported as blocked. */
export function recorderScript(publishWords = PUBLISH_WORDS) {
  const words = JSON.stringify(publishWords.map((re) => [re.source, re.flags]));
  return `(() => {
  if (window.__kdpRecorderOn) return;
  window.__kdpRecorderOn = true;
  const publish = ${words}.map(([s, f]) => new RegExp(s, f));
  const isPublish = (t) => publish.some((re) => re.test(String(t || '')));
  const secretField = new RegExp(${JSON.stringify(SECRET_FIELD.source)}, ${JSON.stringify(SECRET_FIELD.flags)});
  const squash = (t) => String(t || '').replace(/\\s+/g, ' ').trim();
  const CLICKABLE = 'button, a, [role=button], [role=option], [role=combobox], [role=tab], [role=menuitem], [role=link], input[type=button], input[type=submit]';
  const roleOf = (el) => el.getAttribute('role') || ({ A: 'link', BUTTON: 'button', SELECT: 'select', TEXTAREA: 'textbox' }[el.tagName]) || (el.tagName === 'INPUT' ? (el.type === 'checkbox' || el.type === 'radio' || el.type === 'file' ? el.type : 'textbox') : el.tagName.toLowerCase());
  const labelOf = (el) => {
    const byFor = el.labels && el.labels.length ? [...el.labels].map((l) => l.innerText).join(' ') : '';
    const wrap = el.closest && el.closest('label') ? el.closest('label').innerText : '';
    const ids = (el.getAttribute('aria-labelledby') || '').split(/\\s+/).filter(Boolean).map((i) => (document.getElementById(i) || {}).innerText).join(' ');
    return squash(byFor || wrap || ids);
  };
  const isSecret = (el) => el.type === 'password' || secretField.test([el.getAttribute('aria-label'), labelOf(el), el.id, el.name, el.autocomplete, el.type].join(' '));
  const nameOf = (el) => squash(el.getAttribute('aria-label') || labelOf(el) || (el.tagName === 'INPUT' && !isSecret(el) ? el.value : '') || el.innerText || el.getAttribute('title') || el.getAttribute('placeholder'));
  const stableId = (el) => (el.id && !/\\d{3,}|^:|--r/i.test(el.id) ? el.id : '');
  const describe = (el, kind) => ({
    kind,
    role: roleOf(el),
    name: nameOf(el),
    label: labelOf(el),
    id: stableId(el),
    field: el.getAttribute('name') || '',
    inputType: el.tagName === 'INPUT' ? el.type : '',
    inDialog: Boolean(el.closest('[role=dialog], [aria-modal=true], .a-modal')),
  });
  const send = (data) => { try { window.${BINDING}(data); } catch (e) { /* binding missing on this frame */ } };
  const changeKind = (el) => (el.tagName === 'SELECT' ? 'select' : el.type === 'checkbox' || el.type === 'radio' ? 'check' : el.type === 'file' ? 'upload' : 'input');
  document.addEventListener('change', (e) => {
    const el = e.target;
    if (!el || !el.tagName || !/^(INPUT|SELECT|TEXTAREA)$/.test(el.tagName)) return;
    const kind = changeKind(el);
    const value = kind === 'upload' ? ((el.files && el.files[0] && el.files[0].name) || '') : kind === 'check' ? (el.type === 'radio' ? el.value : String(el.checked)) : kind === 'select' ? ((el.selectedOptions[0] && el.selectedOptions[0].text) || el.value) : el.value;
    send({ ...describe(el, kind), value: kind === 'input' && isSecret(el) ? '[redacted]' : value }); // the secret never leaves the page
  }, true);
  const blockIfPublish = (e, el) => {
    const d = describe(el, 'click');
    const texts = [d.name, d.label, d.id, d.field, el.value];
    if (!texts.some(isPublish)) return false;
    e.preventDefault();
    e.stopImmediatePropagation();
    send({ ...d, blocked: true });
    return true;
  };
  document.addEventListener('click', (e) => {
    const el = e.target && e.target.closest ? e.target.closest(CLICKABLE) : null;
    if (!el) return;
    if (blockIfPublish(e, el)) return;
    if (el.tagName === 'INPUT' && /^(checkbox|radio|file)$/.test(el.type)) return;
    send(describe(el, 'click'));
  }, true);
  document.addEventListener('submit', (e) => {
    const el = e.submitter || e.target;
    if (el && el.tagName) blockIfPublish(e, el);
  }, true);
})();`;
}

/** Installs the recorder on a context: every frame of every page, across navigations. `onEvent(raw, pageUrl)` gets each event. */
export async function installRecorder(context, onEvent) {
  await context.exposeBinding(BINDING, ({ page }, data) => onEvent(data, page.url())); // Playwright docs: /microsoft/playwright, BrowserContext.exposeBinding
  await context.addInitScript(recorderScript()); // runs in each new document, before its own scripts
}

export async function runRecord(root, book, lang) {
  const state = readState(root, book, lang);
  if (!state?.kdpId) throw new Error(`kdp/state/${book}/${lang}.json has no kdpId; run "status" first`);
  const file = recordingPath(root, book, lang);
  const recording = readRecording(file) ?? newRecording(book, lang, state.kdpId);
  const s = await openSession();
  try {
    await installRecorder(s.context, (raw, url) => {
      const event = appendEvent(recording, raw, url);
      writeRecording(file, recording);
      log(`recorded #${event.order} ${event.page} ${event.kind}${event.blocked ? ' BLOCKED' : ''}: ${event.name || event.label || event.id}`);
    });
    await s.page.goto(titleUrl(state.kdpId, 'details'));
    await ensureKdpPage(s.page);
    log(`recording to ${file}: fill the title in by hand (Details, Content, Pricing), press Save as Draft on each page, then close the browser window.`);
    await new Promise((resolve) => s.context.on('close', resolve));
  } finally {
    await s.close().catch(() => {});
  }
  writeRecording(file, recording);
  return { file, events: recording.events.length };
}
