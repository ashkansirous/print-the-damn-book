// Page maps generated from a recording (recorder.mjs): one JSON file per KDP page in src/pages/recorded/. No hand-written map.
// Slice 43 H. `checkMap` is what the test uses: a map may only name a control the recording holds.
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

export const MAPS_DIR = join(dirname(fileURLToPath(import.meta.url)), 'pages', 'recorded');
export const MAP_PAGES = ['details', 'content', 'pricing', 'series', 'preview'];

/** 'details' | 'content' | 'pricing' | 'series' | 'preview' | 'other', from a URL path (new/details counts as details; KDP serves content and pricing under /print-setup/). */
export function pageKindOfPath(path) {
  const m = /\/(?:title-setup|print-setup)\/paperback\/[^/]+\/(details|content|pricing)\b/.exec(path);
  if (m) return m[1];
  if (/\/print-preview\//.test(path)) return 'preview';
  return /series/i.test(path) ? 'series' : 'other';
}

/** The KDP page an event belongs to, read from its path (the first recording labelled content, pricing and the previewer "other"). */
export const pageOf = (e) => (e.path ? pageKindOfPath(e.path) : e.page);

/** The control's identity: its role and the most stable thing the page gave (id, then field name, then label, then accessible name). */
export function controlKey(e) {
  return `${e.role}|${e.id || e.field || e.label || e.name}`;
}

const newControl = (e, key) => ({ key, kind: e.kind, role: e.role, name: e.name, label: e.label, id: e.id, field: e.field, inDialog: Boolean(e.inDialog), order: e.order, values: [] });

/** A recording or a list of them (every recording of a book, oldest first) as a list. */
const asList = (recordings) => (Array.isArray(recordings) ? recordings : [recordings]);

/** One map per page from every recording's events (blocked ones are left out): each control once, with every value seen, in first-seen order across the recordings. */
export function mapsFromRecordings(recordings) {
  const maps = {};
  for (const recording of asList(recordings)) {
    for (const e of recording.events ?? []) {
      const page = pageOf(e);
      if (e.blocked || !MAP_PAGES.includes(page)) continue;
      const map = (maps[page] ??= { page, source: `${recording.book}-${recording.lang}`, kdpId: recording.kdpId, controls: [] });
      const key = controlKey(e);
      let control = map.controls.find((c) => c.key === key);
      if (!control) {
        control = newControl(e, key);
        map.controls.push(control);
      }
      if (e.value != null && e.value !== '' && !control.values.includes(e.value)) control.values.push(e.value);
    }
  }
  return maps;
}

/** The same for one recording. */
export const mapsFromRecording = (recording) => mapsFromRecordings([recording]);

/** Errors for every control the map names that no recording holds (empty list = the map is backed by the recordings). */
export function checkMap(map, recordings) {
  const events = asList(recordings).flatMap((r) => r.events ?? []);
  const seen = new Set(events.filter((e) => !e.blocked && pageOf(e) === map.page).map(controlKey));
  return (map.controls ?? []).filter((c) => !seen.has(c.key)).map((c) => `${map.page} map names "${c.key}", which the recording does not hold`);
}

/** The generated map of one KDP page, or null when no recording produced one. */
export function readMap(page, dir = MAPS_DIR) {
  const file = join(dir, `${page}.json`);
  return existsSync(file) ? JSON.parse(readFileSync(file, 'utf8')) : null;
}

/** Whether a generated map holds a control: its role, and its accessible name or id equal to a string or matching a RegExp. */
export function hasControl(map, role, name) {
  const same = (c) => c.role === role && (name instanceof RegExp ? name.test(c.name) || name.test(c.id) : c.name === name || c.id === name);
  return Boolean(map?.controls?.some(same));
}

export function writeMaps(recordings, dir = MAPS_DIR) {
  mkdirSync(dir, { recursive: true });
  const written = [];
  for (const [page, map] of Object.entries(mapsFromRecordings(recordings))) {
    const file = join(dir, `${page}.json`);
    writeFileSync(file, `${JSON.stringify(map, null, 2)}\n`);
    written.push(file);
  }
  return written;
}
