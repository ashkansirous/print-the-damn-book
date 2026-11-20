// Pure decisions of a draft run (tested without a browser): did the save work, did the file check finish,
// may the free ISBN be assigned, is a file already uploaded. Success markers are unconfirmed on the live site until the [session] run.
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { KDP_ID } from './pages/bookshelf.mjs';

const ERROR_WORDS = /\b(error|failed|failure|couldn.t|could not|can.t|cannot|invalid|unable|not valid|problem|issue|required|too long|too short|must be|missing)\b/i;
const SAVED = /(draft|changes|book|information)\s+(has been|have been|was|were|is)?\s*saved|saved as (a )?draft|saved successfully/i;
const UPLOADED = {
  manuscript: /manuscript[^.\n]{0,80}(success|uploaded|complete|processed)/i,
  cover: /cover[^.\n]{0,80}(success|uploaded|complete|processed)/i,
};
// Only an actual 978/979 number next to the ISBN label counts; the label "Free KDP ISBN" alone shows before any ISBN exists.
const ISBN_ASSIGNED = /\bISBN(?:-13)?\b\s*[:\-]?\s*97[89][\d-]{10,}/i;
const TRIM_SHOWN = /8\.5\s*x\s*8\.5/i;
// KDP's known progress texts ("In Progress...", "Not Started...", "Processing"); they never block a success marker, any other unknown alert still does.
const PROGRESS_TEXT = /^(in progress|not started|processing|uploading)\b[^\n]{0,20}$/i;

export class KdpRejected extends Error {
  constructor(what, messages) {
    super(`KDP did not accept ${what}: ${messages.join(' | ')}`);
    this.name = 'KdpRejected';
    this.messages = messages;
  }
}

export class KdpNoSignal extends Error {
  constructor(what, ms) {
    super(`KDP showed no success or error marker for ${what} within ${Math.round(ms / 1000)} s; run stopped`);
    this.name = 'KdpNoSignal';
  }
}

/**
 * A visible alert that is neither an error nor the success marker being waited for, and was not already on the page before the
 * action (`ignore`), blocks a success: fail closed, the run then ends with no signal and KDP's words in the summary.
 */
const verdict = (alerts, ok, okState, successRe, ignore) => {
  const errors = alerts.filter((a) => ERROR_WORDS.test(a));
  if (errors.length) return { state: 'error', messages: errors };
  const unknown = alerts.filter((a) => !successRe.test(a) && !PROGRESS_TEXT.test(a.trim()) && !ignore.includes(a));
  return { state: ok && !unknown.length ? okState : 'pending', messages: [] };
};

/** After "Save as Draft": 'error' (KDP's alert texts), 'saved' (a success marker) or 'pending'. */
export function judgeSave({ alerts = [], bodyText = '', saving = false, ignore = [] }) {
  if (saving) return { state: 'pending', messages: [] };
  return verdict(alerts, SAVED.test(`${alerts.join('\n')}\n${bodyText}`), 'saved', SAVED, ignore);
}

/** After a file is handed to KDP: 'error', 'done' (a success marker for that kind of file) or 'pending'. */
export function judgeFileCheck(kind, { alerts = [], bodyText = '', saving = false, ignore = [] }) {
  if (saving) return { state: 'pending', messages: [] };
  return verdict(alerts, UPLOADED[kind].test(`${alerts.join('\n')}\n${bodyText}`), 'done', UPLOADED[kind], ignore);
}

/**
 * Polls `read()` -> {alerts, bodyText} until the judge says done or error. An error throws with KDP's own message,
 * no marker before the timeout throws too: never a silent pass. `baseline` is the verdict before the action; a success
 * marker that was already there only counts once the alert texts changed.
 */
export async function waitForVerdict({ what, judge, read, timeoutMs, pollMs = 2000, baseline = null, onMessages = () => {}, onWait = () => {} }) {
  const started = Date.now();
  for (;;) {
    const page = await read();
    onMessages(page.alerts);
    const v = judge({ ...page, ignore: baseline?.alerts ?? [] });
    if (v.state === 'error') throw new KdpRejected(what, v.messages);
    const stale = baseline && baseline.state !== 'pending' && baseline.key === page.alerts.join('|');
    if (v.state !== 'pending' && !stale) return v;
    if (Date.now() - started >= timeoutMs) throw new KdpNoSignal(what, timeoutMs);
    onWait();
    await new Promise((r) => setTimeout(r, pollMs));
  }
}

/** The verdict before an action, so a success marker that was already on the page never counts as the action's result. */
export const baselineOf = (judge, page) => {
  const alerts = page.alerts ?? [];
  // The page's own alerts are ignored here, so a harmless alert never turns a leftover success marker into 'pending' (which would switch the stale guard off).
  return { ...judge({ ...page, ignore: alerts }), key: alerts.join('|'), alerts };
};

export class IdConflict extends Error {
  constructor(known, seen) {
    super(`the KDP title id is ${known} but the page shows ${seen}; the id is set once and never re-pointed, run stopped`);
    this.name = 'IdConflict';
  }
}

export class Ambiguous extends Error {
  constructor(what, count) {
    super(`${count} bookshelf rows fit ${what}; the robot will not guess which one is the title, run stopped`);
    this.name = 'Ambiguous';
  }
}

export class NotEditable extends Error {
  constructor(state) {
    super(state === 'unknown'
      ? 'the title is not on the bookshelf rows the robot read, or its status text is not exactly a draft label; draft edits only a title whose row reads as a draft, run stopped. Run status and check the title on KDP'
      : `the title is "${state}" on the bookshelf; draft edits only a title whose row reads as a draft, run stopped`);
    this.name = 'NotEditable';
  }
}

export class PageMapNotConfirmed extends Error {
  constructor(page) {
    super(`page map not confirmed: ${page}; run capture first`);
    this.name = 'PageMapNotConfirmed';
  }
}

/** `draft` reached a page whose map is not confirmed: the page was captured (nothing saved on it) and the run stops here. */
export class StoppedAtUnconfirmedPage extends PageMapNotConfirmed {
  constructor(page, capturedHtml) {
    super(page);
    this.message = `page map not confirmed: ${page}; nothing was saved on it. The page was captured to ${capturedHtml}: confirm src/pages/${page}.mjs from that capture (set confirmed: true), then run draft again`;
    this.name = 'StoppedAtUnconfirmedPage';
  }
}

/** The id is set once: the same id is fine, a different one stops the run. Returns the id to keep. */
export function keepKdpId(known, seen) {
  if (known && !KDP_ID.test(known)) throw new Error(`the known title id "${String(known).slice(0, 40)}" is not a KDP title id; the run stops`);
  if (!seen) return known;
  if (known && known !== seen) throw new IdConflict(known, seen);
  return seen;
}

/** The one bookshelf row of a title (title + language + format matched): none -> null, two or more -> Ambiguous. */
export function singleRow(matched, what) {
  if (matched.length > 1) throw new Ambiguous(what, matched.length);
  return matched[0]?.row ?? null;
}

export function assertEditable(state) {
  if (state !== 'draft') throw new NotEditable(state);
}

/** `draft` refuses to run while any page map it needs is unconfirmed (set `confirmed: true` only after checking it against a capture). */
export function requireConfirmedPages(pages) {
  for (const p of pages) if (!p.confirmed) throw new PageMapNotConfirmed(p.name);
}

export function requireKdpId(id) {
  if (!id) throw new Error('no KDP id after Save as Draft: the URL did not carry the title id. The title was saved on KDP, so kdp/state now marks it (savedWithoutId) and the next run creates no new title until the bookshelf shows one matching row');
  return id;
}

export const hasIsbn = (bodyText) => ISBN_ASSIGNED.test(bodyText);
export const liveTrim = (bodyText) => (TRIM_SHOWN.test(bodyText) ? '8.5 x 8.5 in' : undefined);

export class IsbnConflict extends Error {
  constructor() {
    super('kdp/state says an ISBN was assigned but the live page shows none; the assignment is irreversible, so the robot will not repeat it. Check the title on KDP and fix kdp/state by hand.');
    this.name = 'IsbnConflict';
  }
}

/** The free-ISBN assignment is irreversible: only when neither the live page nor kdp/state knows an ISBN. */
export function isbnPlan({ liveHas, stateHas }) {
  if (liveHas) return 'skip';
  if (stateHas) throw new IsbnConflict();
  return 'assign';
}

export function fileHash(path) {
  return createHash('sha256').update(readFileSync(path)).digest('hex');
}

export const needsUpload = (state, kind, hash) => state?.files?.[kind] !== hash;

/** The state of one title from the parsed bookshelf rows; no row for the id is 'unknown', never a guess. */
export function stateForTitle(rows, kdpId) {
  return rows.find((r) => r.kdpId === kdpId)?.state ?? 'unknown';
}

/** Which selected-category text on the page stands for category i: an exact match first, else the one in that position. */
export function liveCategories(selected, fields) {
  const norm = (t) => t.split(/\s*[>›]\s*/).map((p) => p.trim()).join(' > ');
  const list = selected.map(norm);
  const live = {};
  fields.forEach((f, i) => {
    const hit = list.includes(f.value) ? f.value : list[i];
    if (hit !== undefined) live[f.key] = hit;
  });
  return live;
}
