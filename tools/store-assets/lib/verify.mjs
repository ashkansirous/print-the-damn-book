import { readFileSync } from 'node:fs';
import { PNG } from 'pngjs';
import { readIcoEntries } from './ico.mjs';
import { ICO_FILE, ICO_SIZES, JobKind } from './jobs.mjs';
import { COLOR_TYPE_RGB, COLOR_TYPE_RGBA, readPngHeader } from './png.mjs';

const CHANNELS = 4;
const CORNER_ALPHA_OFFSET = 3;
const MIN_SMALL_ICON_DIFFERENCE = 1;

function verifyPng(job) {
  const problems = [];
  const header = readPngHeader(readFileSync(job.file));
  if (header.width !== job.outW || header.height !== job.outH) {
    problems.push(`${job.file}: is ${header.width}x${header.height}, expected ${job.outW}x${job.outH}`);
  }
  if (header.colorType !== COLOR_TYPE_RGB) problems.push(`${job.file}: colour type ${header.colorType}, expected RGB (2)`);
  return problems;
}

function verifyIcoEntry(entry, expected, rendered) {
  const label = `${ICO_FILE} ${expected.size}px`;
  const header = readPngHeader(entry.png);
  const problems = [];
  if (header.width !== expected.size || header.height !== expected.size) {
    problems.push(`${label}: payload is ${header.width}x${header.height}`);
  }
  if (header.colorType !== COLOR_TYPE_RGBA) problems.push(`${label}: payload is not 32-bit RGBA`);
  if (!entry.png.equals(rendered.png)) problems.push(`${label}: payload differs from the ${expected.source} render`);
  if (PNG.sync.read(entry.png).data[CORNER_ALPHA_OFFSET] !== 0) problems.push(`${label}: top-left corner is not transparent`);
  return problems;
}

/** Mean absolute channel difference between an image and the 256px image sampled down to its size. */
function differenceFromDownscaled(small, large) {
  const step = large.width / small.width;
  let total = 0;
  for (let y = 0; y < small.height; y++) {
    for (let x = 0; x < small.width; x++) {
      const s = (y * small.width + x) * CHANNELS;
      const l = (Math.floor((y + 0.5) * step) * large.width + Math.floor((x + 0.5) * step)) * CHANNELS;
      for (let c = 0; c < CHANNELS; c++) total += Math.abs(small.data[s + c] - large.data[l + c]);
    }
  }
  return total / (small.width * small.height * CHANNELS);
}

/** The small-size entries must come from app-icon-sm.svg, so they cannot match a downscaled 256 entry. */
function verifySmallEntriesDiffer(entries) {
  const large = PNG.sync.read(entries[0].png);
  return entries.slice(1).flatMap((entry, i) => {
    if (ICO_SIZES[i + 1].source === ICO_SIZES[0].source) return [];
    const difference = differenceFromDownscaled(PNG.sync.read(entry.png), large);
    return difference < MIN_SMALL_ICON_DIFFERENCE
      ? [`${ICO_FILE} ${entry.size}px: matches a downscaled ${large.width}px entry, so ${ICO_SIZES[i + 1].source} was not used`]
      : [];
  });
}

function verifyIco(icoImages) {
  const entries = readIcoEntries(readFileSync(ICO_FILE));
  const found = entries.map((e) => e.size);
  const expected = ICO_SIZES.map((s) => s.size);
  if (found.join() !== expected.join()) return [`${ICO_FILE}: holds ${found.join(',')}, expected ${expected.join(',')}`];
  const entryProblems = ICO_SIZES.flatMap((spec, i) => verifyIcoEntry(entries[i], spec, icoImages[i]));
  return [...entryProblems, ...verifySmallEntriesDiffer(entries)];
}

/** Reads every generated file back from disk and returns the problems found (empty = all good). */
export function verifyOutputs(jobs, icoImages) {
  const pngProblems = jobs.filter((j) => j.kind !== JobKind.Ico).flatMap(verifyPng);
  return [...pngProblems, ...verifyIco(icoImages)];
}
