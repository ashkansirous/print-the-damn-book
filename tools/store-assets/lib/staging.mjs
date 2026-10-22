import { cpSync, existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { DESIGN_DIR, NODE_MODULES, SAMPLE_FILES, WORK_DIR, findSampleDir } from './paths.mjs';

const INTER_WEIGHTS = [400, 500, 600, 700];
const GOOGLE_FONTS_IMPORT = /@import url\([^)]*fonts\.googleapis\.com[^)]*\);?/;

function interFontFaces() {
  return INTER_WEIGHTS.map(
    (w) => `@font-face{font-family:"Inter";font-weight:${w};font-style:normal;font-display:block;` +
      `src:url("fonts/inter-latin-${w}-normal.woff2") format("woff2")}`,
  ).join('\n');
}

function stageFonts() {
  const interDir = path.join(NODE_MODULES, '@fontsource', 'inter', 'files');
  for (const w of INTER_WEIGHTS) {
    cpSync(path.join(interDir, `inter-latin-${w}-normal.woff2`), path.join(WORK_DIR, 'fonts', `inter-latin-${w}-normal.woff2`));
  }
  const phosphorDir = path.join(NODE_MODULES, '@phosphor-icons', 'web', 'src');
  cpSync(path.join(phosphorDir, 'regular'), path.join(WORK_DIR, 'ph', 'regular'), { recursive: true });
  cpSync(path.join(phosphorDir, 'fill'), path.join(WORK_DIR, 'ph', 'fill'), { recursive: true });
}

/** The design's stylesheet with the network font import swapped for the local Inter files. */
function stageStyles() {
  const css = readFileSync(path.join(DESIGN_DIR, 'nocturne', 'styles.css'), 'utf8');
  if (!GOOGLE_FONTS_IMPORT.test(css)) throw new Error('nocturne/styles.css no longer has the Google Fonts @import');
  writeFileSync(path.join(WORK_DIR, 'styles.css'), `${interFontFaces()}\n${css.replace(GOOGLE_FONTS_IMPORT, '')}`);
}

const BOOK_IMAGE_RE = /book\/([^."'\s)]+)\.jpg/g;

/** Names of the sample pages the artboards actually reference (book/<name>.jpg). */
export function referencedBookImages(jobs) {
  const names = new Set();
  for (const job of jobs) {
    for (const match of job.html.matchAll(BOOK_IMAGE_RE)) names.add(match[1]);
  }
  return [...names].sort();
}

function sampleFileFor(name) {
  const file = SAMPLE_FILES[name];
  if (!file) throw new Error(`Artboards reference book/${name}.jpg, which has no entry in SAMPLE_FILES`);
  return file;
}

/** Sample file names for the referenced pages; throws if any is missing, before anything is wiped. */
function checkedSampleFiles(sampleDir, names) {
  const files = names.map(sampleFileFor);
  const missing = files.filter((file) => !existsSync(path.join(sampleDir, file)));
  if (missing.length) throw new Error(`Missing in "${sampleDir}": ${missing.join(', ')}`);
  return files;
}

function stageBook(sampleDir, names, files) {
  names.forEach((name, i) => cpSync(path.join(sampleDir, files[i]), path.join(WORK_DIR, 'book', `${name}.jpg`)));
}

/** Copies fonts, stylesheet, icons and the sample pages the jobs use next to the generated pages. Returns the work dir. */
export function stageWorkDir(jobs) {
  const sampleDir = findSampleDir();
  const names = referencedBookImages(jobs);
  const files = checkedSampleFiles(sampleDir, names);
  rmSync(WORK_DIR, { recursive: true, force: true });
  mkdirSync(path.join(WORK_DIR, 'book'), { recursive: true });
  stageFonts();
  stageStyles();
  cpSync(path.join(DESIGN_DIR, 'icons'), path.join(WORK_DIR, 'icons'), { recursive: true });
  stageBook(sampleDir, names, files);
  return WORK_DIR;
}
