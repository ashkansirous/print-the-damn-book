// Usage: node src/cli.mjs catalog | login | status | capture | record <book> <lang> | gen-maps <book> <lang> | draft <book> <lang> [--dry-run] [--content <dir>]
import { pathToFileURL } from 'node:url';
import { checkCatalog } from './catalog.mjs';
import { DEFAULT_CONTENT, REPO_ROOT, fsContext, loadCatalog } from './repo.mjs';

function option(args, name, fallback) {
  const i = args.indexOf(name);
  return i >= 0 ? args[i + 1] : fallback;
}

async function main(argv) {
  const [command, ...rest] = argv;
  const content = option(rest, '--content', DEFAULT_CONTENT);
  if (command === 'catalog') {
    const errors = checkCatalog(loadCatalog(), fsContext(REPO_ROOT, content));
    console.log(errors.length ? errors.map((e) => `FAIL ${e}`).join('\n') : 'PASS kdp/catalog.json');
    return errors.length ? 1 : 0;
  }
  if (command === 'login') {
    await (await import('./commands.mjs')).runLogin();
    return 0;
  }
  if (command === 'status') {
    await (await import('./commands.mjs')).runStatus(REPO_ROOT, content);
    return 0;
  }
  if (command === 'capture') {
    const errors = await (await import('./capture.mjs')).runCapture(REPO_ROOT);
    for (const e of errors) console.error(`FAIL ${e}`);
    return errors.length ? 1 : 0;
  }
  if (command === 'record' || command === 'gen-maps') {
    const [book, lang] = rest.filter((a) => !a.startsWith('--') && a !== content);
    if (!book || !lang) throw new Error(`usage: ${command} <book> <lang>`);
    const rec = await import('./recorder.mjs');
    if (command === 'record') {
      const { file, events } = await rec.runRecord(REPO_ROOT, book, lang);
      console.log(`recorded ${events} events to ${file}; next: node src/cli.mjs gen-maps ${book} ${lang}`);
    } else {
      const recordings = rec.readRecordings(REPO_ROOT, book, lang);
      if (!recordings.length) throw new Error(`no recording for ${book} ${lang}; run record first`);
      for (const f of (await import('./map-gen.mjs')).writeMaps(recordings)) console.log(`wrote ${f}`);
    }
    return 0;
  }
  if (command === 'draft') {
    const [book, lang] = rest.filter((a) => !a.startsWith('--') && a !== content);
    if (!book || !lang) throw new Error('usage: draft <book> <lang> [--dry-run] [--reset-stops]');
    const { runDraft } = await import('./draft.mjs');
    const { summary } = await runDraft(REPO_ROOT, loadCatalog(), book, lang, { dryRun: rest.includes('--dry-run'), resetStops: rest.includes('--reset-stops') });
    console.log(`summary: ${summary}`);
    return 0;
  }
  console.error('usage: node src/cli.mjs catalog | login | status | capture | record <book> <lang> | gen-maps <book> <lang> | draft <book> <lang> [--dry-run]');
  return 2;
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  main(process.argv.slice(2)).then((code) => process.exit(code), (e) => {
    console.error(`Error: ${e.message}`);
    process.exit(1);
  });
}
