// Usage: node check-listing.mjs <listing.md>...   Exit 0 when every listing passes, 1 otherwise.
import { readFileSync } from 'node:fs';
import { checkListing } from './listing-limits.mjs';

let failed = false;
for (const file of process.argv.slice(2)) {
  const errors = checkListing(readFileSync(file, 'utf8'));
  console.log(errors.length ? `FAIL ${file}` : `PASS ${file}`);
  for (const e of errors) console.log(`  - ${e}`);
  failed ||= errors.length > 0;
}
process.exit(failed || process.argv.length < 3 ? 1 : 0);
