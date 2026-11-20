// "Paperback Rights & Pricing" page. Labels unconfirmed on the live site (see bookshelf.mjs).
// `confirmed` becomes true only after this map was checked against a `capture` of the live page; `draft` refuses until then.
export const PAGE = { name: 'pricing', confirmed: false };

export const PRICING_SPECS = {
  territories: { kind: 'radio', group: /territories/i, options: ['All territories (worldwide rights)', 'Individual territories'] },
  'price-us': { kind: 'text', label: /amazon\.com/i },
};

/** KDP shows prices with a decimal point in the en_US UI. */
export function formatPrice(n) {
  const price = typeof n === 'number' ? n : Number.NaN;
  if (!Number.isFinite(price) || price <= 0) throw new Error(`price "${n}" is not a number above 0`);
  return price.toFixed(2);
}
