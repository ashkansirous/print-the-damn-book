// Colour and font come from the Nocturne tokens in styles.css (staged next to the page).
const BODY_STYLE = 'margin:0;font-family:var(--font-body);color:var(--color-text);background:transparent';

/** A standalone page that holds one artboard (`#art`) at the top-left corner. */
export function artboardPage(artboardHtml) {
  return `<!DOCTYPE html>
<html><head><meta charset="utf-8">
<link rel="stylesheet" href="styles.css">
<link rel="stylesheet" href="ph/regular/style.css">
<link rel="stylesheet" href="ph/fill/style.css">
<style>html,body{margin:0;background:transparent}body{${BODY_STYLE}}</style>
</head><body>${artboardHtml}</body></html>`;
}
