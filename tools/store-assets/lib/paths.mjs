import { execFileSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const TOOL_DIR = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
export const REPO_ROOT = path.resolve(TOOL_DIR, '..', '..');
export const DESIGN_DIR = path.join(REPO_ROOT, 'docs', 'design');
export const WORK_DIR = path.join(TOOL_DIR, '.work');
export const STORE_OUT_DIR = path.join(REPO_ROOT, 'assets', 'store');
export const APP_ASSETS_DIR = path.join(REPO_ROOT, 'src', 'PrintTheDamnBook.App', 'Assets');
export const NODE_MODULES = path.join(TOOL_DIR, 'node_modules');

const SAMPLE_DIR_NAME = 'Book sample';
const SAMPLE_DIR_ENV = 'BOOK_SAMPLE_DIR';

/** Design image name (book/<name>.jpg) to the file it comes from in the sample folder. */
export const SAMPLE_FILES = {
  cover: 'Cover.jpg',
  back: 'Back cover.jpg',
  ...Object.fromEntries(Array.from({ length: 10 }, (_, i) => [String(i + 1), `${i + 1}.jpg`])),
};

/** The main checkout's folder, or null when git is missing, too old or this is not a checkout. */
function primaryCheckoutRoot() {
  try {
    const common = execFileSync('git', ['rev-parse', '--path-format=absolute', '--git-common-dir'], {
      cwd: REPO_ROOT,
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'ignore'],
    }).trim();
    return path.dirname(common);
  } catch {
    return null;
  }
}

/** Candidate folders in lookup order; the git lookup only runs when the cheaper one misses. */
function* sampleDirCandidates() {
  yield path.join(REPO_ROOT, SAMPLE_DIR_NAME);
  const primary = primaryCheckoutRoot();
  if (primary) yield path.join(primary, SAMPLE_DIR_NAME);
}

/** Finds the git-ignored sample pages. An explicit BOOK_SAMPLE_DIR is never replaced by another folder. */
export function findSampleDir() {
  const fromEnv = process.env[SAMPLE_DIR_ENV];
  if (fromEnv) {
    if (existsSync(fromEnv)) return fromEnv;
    throw new Error(`${SAMPLE_DIR_ENV} points at "${fromEnv}", which does not exist.`);
  }
  const searched = [];
  for (const dir of sampleDirCandidates()) {
    if (existsSync(dir)) return dir;
    searched.push(dir);
  }
  throw new Error(
    `Sample pages not found. Looked in:\n  ${searched.join('\n  ')}\n` +
      `Set ${SAMPLE_DIR_ENV} or create "${SAMPLE_DIR_NAME}" in the repo root.`,
  );
}
