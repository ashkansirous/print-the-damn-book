// Usage: node check-listing.mjs <listing.md>...   Exit 0 when every listing passes, 1 otherwise.
import { readFileSync } from 'node:fs';
import { checkListing, listingBookId, ownerWrittenIds } from './listing-limits.mjs';

// A listing of a book the catalog marks `ownerWrittenText` must answer Text "None" (Slice 43 K).
const owner = ownerWrittenIds(JSON.parse(readFileSync(new URL('../../../kdp/catalog.json', import.meta.url), 'utf8')));

let failed = false;
for (const file of process.argv.slice(2)) {
  const errors = checkListing(readFileSync(file, 'utf8'), { ownerWritten: owner.has(listingBookId(file)) });
  console.log(errors.length ? `FAIL ${file}` : `PASS ${file}`);
  for (const e of errors) console.log(`  - ${e}`);
  failed ||= errors.length > 0;
}
process.exit(failed || process.argv.length < 3 ? 1 : 0);
