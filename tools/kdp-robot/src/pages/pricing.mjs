// "Paperback Rights & Pricing" page. Slice 43 H (2026-11-21): the price boxes and Save as Draft come from the owner's recording
// (src/pages/recorded/pricing.json, made by `gen-maps`): KDP's boxes carry the ids price-input-usd, price-input-gbp, price-input-eur and
// price-input-sek, and the accessible name "Price for Amazon.com in USD" and so on. The page counts as confirmed only while that map
// holds the controls `draft` uses (pages/recorded.mjs); there is no hand-set flag. The territory radios are from the 2026-11-20 capture.
import { recordedConfirmed } from './recorded.mjs';

export const PAGE = { name: 'pricing', get confirmed() { return recordedConfirmed('pricing'); } };

export const PRICING_SPECS = {
  territories: { kind: 'radio', css: 'input[name="territory-selection-type"]', values: { 'All territories (worldwide rights)': 'worldwide', 'Individual territories': 'individual' }, options: ['All territories (worldwide rights)', 'Individual territories'] },
  // The US list price only. The recording also holds GBP, EUR and SEK boxes (and a later 10.50 / 8.10 edit): whether KDP derives them from the USD
  // price is not known, so they are not set and the first replay shows what KDP holds (open point for the owner).
  'price-us': { kind: 'text', css: '[id="price-input-usd"]', ref: 'price-input-usd', label: /amazon\.com/i },
};

/** KDP shows prices with a decimal point in the en_US UI. */
export function formatPrice(n) {
  const price = typeof n === 'number' ? n : Number.NaN;
  if (!Number.isFinite(price) || price <= 0) throw new Error(`price "${n}" is not a number above 0`);
  return price.toFixed(2);
}
