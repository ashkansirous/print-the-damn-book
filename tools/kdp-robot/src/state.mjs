// kdp/state/<book>/<lang>.json: what KDP held after the last run. Never overwrites a file it cannot read.
import { existsSync, mkdirSync, readFileSync, renameSync, writeFileSync } from 'node:fs';
import { randomUUID } from 'node:crypto';
import { dirname, join } from 'node:path';
import { KDP_ID } from './pages/bookshelf.mjs';

export const KDP_STATES = ['draft', 'in_review', 'live', 'unknown'];

export function statePath(root, book, lang) {
  return join(root, 'kdp', 'state', book, `${lang}.json`);
}

export function readState(root, book, lang) {
  const file = statePath(root, book, lang);
  if (!existsSync(file)) return null;
  let state;
  try {
    state = JSON.parse(readFileSync(file, 'utf8'));
  } catch (e) {
    throw new Error(`${file} cannot be read (${e.message}); fix or move it, the robot will not overwrite it`);
  }
  if (state?.kdpId != null && !KDP_ID.test(state.kdpId)) {
    throw new Error(`${file} has kdpId "${String(state.kdpId).slice(0, 40)}", which is not a KDP title id; fix or move it, the robot will not use it`);
  }
  return state;
}

export function nextState(previous, patch) {
  const next = {
    kdpId: null, state: 'unknown', fields: {}, files: {}, ...previous, ...patch,
    fields: { ...previous?.fields, ...patch.fields },
    files: { ...previous?.files, ...patch.files },
  };
  if (next.kdpId) delete next.savedWithoutId; // once the id is known the "saved without an id" marker is moot
  return next;
}

export function writeState(root, book, lang, state) {
  readState(root, book, lang); // throws when the old file is unreadable
  const file = statePath(root, book, lang);
  mkdirSync(dirname(file), { recursive: true });
  const temp = `${file}.${process.pid}.${randomUUID()}.tmp`;
  writeFileSync(temp, `${JSON.stringify(state, null, 2)}\n`);
  try {
    renameSync(temp, file); // same folder, so the old file is replaced whole or not at all
  } catch (e) {
    // The temp file may hold the only copy of what KDP just assigned (the ISBN): never delete it.
    throw new Error(`could not replace ${file} (${e.message}); the new state is kept in ${temp}: copy it over ${file} by hand before running again`);
  }
  return file;
}
