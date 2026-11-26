// summary.md of one run: every field with old and new value, the files uploaded, KDP's own messages, screenshots.
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const cell = (v) => String(v ?? '').replace(/\|/g, '\\|').replace(/\n/g, ' ');
const short = (v) => (cell(v).length > 80 ? `${cell(v).slice(0, 77)}...` : cell(v));

/** The one line a stopped run hands to the owner (Slice 43 I): the field it could not find, or the first line of the error. */
export function stopLineOf(e) {
  const first = String(e?.message ?? e).split('\n')[0].replace(/\s+/g, ' ').trim().slice(0, 200);
  return e?.field ? `could not find field "${e.field}": ${first}` : first;
}

/** A book may stop twice (the first run, then one rerun after one fix); a third run needs the owner's --reset-stops. */
export const MAX_STOPPED_RUNS = 2;

export function bookshelfLink(kdpId) {
  return kdpId ? `https://kdp.amazon.com/en_US/title-setup/paperback/${kdpId}/details` : 'https://kdp.amazon.com/en_US/bookshelf';
}

function headerLines(run) {
  return [
    `# KDP draft: ${run.book} (${run.lang})`,
    '',
    ...(run.stop ? [`STOP: ${run.stop}`, ''] : []),
    `Run: ${run.when}${run.dryRun ? ' (dry run: no browser was opened, nothing was sent to KDP)' : ''}`,
    `KDP title id: ${run.kdpId ?? '(not created yet)'}`,
    `Bookshelf page: ${bookshelfLink(run.kdpId)}`,
    `State on KDP: ${run.state ?? 'unknown'}. The robot never presses Publish; nothing was published.`,
    '',
  ];
}

function fieldLines(diff) {
  const rows = diff.map((d) => `| ${d.page} | ${cell(d.label)} | ${short(d.old) || '(empty)'} | ${short(d.value)} | ${d.status} |`);
  const counts = ['new', 'changed', 'same'].map((s) => `${diff.filter((d) => d.status === s).length} ${s}`).join(', ');
  return [`## Fields (${counts})`, '', '| Page | Field | Old | New | Status |', '| --- | --- | --- | --- | --- |', ...rows, ''];
}

const listSection = (title, items) => [`## ${title}`, '', ...(items.length ? items : ['- none']), ''];

export function renderSummary(run) {
  return [
    ...headerLines(run),
    ...fieldLines(run.diff),
    ...listSection('Files uploaded', run.uploads.map((u) => `- ${u.kind}: ${u.path}`)),
    ...listSection('KDP messages', run.messages.map((m) => `- ${m.replace(/\n/g, ' ')}`)),
    ...listSection('Screenshots', run.screenshots.map((s) => `- ${s}`)),
  ].join('\n');
}

export function writeSummary(root, run) {
  const dir = join(root, 'tmp', 'kdp-robot', `${run.book}-${run.lang}`);
  mkdirSync(dir, { recursive: true });
  const file = join(dir, 'summary.md');
  writeFileSync(file, renderSummary(run));
  return file;
}
