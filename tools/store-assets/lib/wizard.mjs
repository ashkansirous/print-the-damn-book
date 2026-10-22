import { readFileSync } from 'node:fs';
import path from 'node:path';
import { DESIGN_DIR } from './paths.mjs';

/**
 * Renders the BookWizard prototype (docs/design/BookWizard.dc.html) to static HTML without the
 * Claude Design runtime: runs its Component class for the values, then expands its sc-if / sc-for
 * template. Event handlers are dropped; the result is a still picture of one state.
 */

const WIZARD_FILE = path.join(DESIGN_DIR, 'BookWizard.dc.html');
const TEMPLATE_START = '</helmet>';
const TEMPLATE_END = '</x-dc>';
const SCRIPT_RE = /<script type="text\/x-dc" data-dc-script[^>]*>([\s\S]*?)<\/script>/;
const BLOCK_TAG_RE = /<(\/?)(sc-if|sc-for)\b[^>]*>/g;
const EXPR_RE = /\{\{\s*([^}]+?)\s*\}\}/g;
const EVENT_ATTR_RE = /\s(?:on[A-Z]\w*|style-hover)="[^"]*"/g;
const BOOLEAN_ATTR_RE = /\s(checked|disabled)="\{\{\s*([^}]+?)\s*\}\}"/g;

function loadComponentClass(source) {
  const scriptMatch = SCRIPT_RE.exec(source);
  if (!scriptMatch) throw new Error('BookWizard.dc.html no longer has a <script type="text/x-dc" data-dc-script> block');
  const script = scriptMatch[1];
  const dcLogic = 'class DCLogic{constructor(props){this.props=props}setState(s){Object.assign(this.state,s)}}';
  return new Function(`${dcLogic}\n${script}\nreturn Component;`)();
}

function resolve(expr, scope) {
  if (expr === 'true') return true;
  if (expr === 'false') return false;
  return expr.split('.').reduce((value, key) => (value == null ? value : value[key]), scope);
}

function interpolate(text, scope) {
  const withBooleans = text.replace(BOOLEAN_ATTR_RE, (_, attr, expr) => (resolve(expr, scope) ? ` ${attr}` : ''));
  return withBooleans.replace(EXPR_RE, (_, expr) => {
    const value = resolve(expr, scope);
    if (value === undefined) throw new Error(`BookWizard.dc.html: unresolved expression {{ ${expr} }}`);
    return String(value ?? '');
  });
}

function assertFullyExpanded(html) {
  const leftover = /\{\{[^}]*\}\}|<\/?sc-(?:if|for)\b/.exec(html);
  if (leftover) throw new Error(`BookWizard.dc.html: unexpanded template text "${leftover[0]}"`);
}

function attributeExpr(openTag, name) {
  const match = new RegExp(String.raw`${name}="\{\{\s*([^}]+?)\s*\}\}"`).exec(openTag);
  return match[1];
}

/** Index range of the block opened at `start`, found by counting nested sc-* tags. */
function findBlockEnd(template, start) {
  BLOCK_TAG_RE.lastIndex = start;
  let depth = 0;
  for (let tag = BLOCK_TAG_RE.exec(template); tag; tag = BLOCK_TAG_RE.exec(template)) {
    depth += tag[1] ? -1 : 1;
    if (depth === 0) return { closeStart: tag.index, end: BLOCK_TAG_RE.lastIndex };
  }
  throw new Error('Unbalanced sc-* tags in BookWizard.dc.html');
}

function expandBlock(template, start, scope) {
  const { closeStart, end } = findBlockEnd(template, start);
  const openEnd = template.indexOf('>', start) + 1;
  const openTag = template.slice(start, openEnd);
  const inner = template.slice(openEnd, closeStart);
  if (openTag.startsWith('<sc-if')) {
    return { html: resolve(attributeExpr(openTag, 'value'), scope) ? render(inner, scope) : '', end };
  }
  const items = resolve(attributeExpr(openTag, 'list'), scope);
  const name = /\bas="([^"]+)"/.exec(openTag)[1];
  return { html: items.map((item) => render(inner, { ...scope, [name]: item })).join(''), end };
}

function render(template, scope) {
  let out = '';
  let cursor = 0;
  for (let start = template.indexOf('<sc-', cursor); start !== -1; start = template.indexOf('<sc-', cursor)) {
    out += interpolate(template.slice(cursor, start), scope);
    const block = expandBlock(template, start, scope);
    out += block.html;
    cursor = block.end;
  }
  return out + interpolate(template.slice(cursor), scope);
}

let cachedWizard;

/** The component class and template, read and evaluated once per run. */
function loadWizard() {
  if (!cachedWizard) {
    const source = readFileSync(WIZARD_FILE, 'utf8');
    const start = source.indexOf(TEMPLATE_START);
    const end = source.indexOf(TEMPLATE_END);
    if (start === -1 || end === -1) throw new Error('BookWizard.dc.html no longer has the expected template markers');
    cachedWizard = {
      Component: loadComponentClass(source),
      template: source.slice(start + TEMPLATE_START.length, end).replace(EVENT_ATTR_RE, ''),
    };
  }
  return cachedWizard;
}

/** HTML of the wizard (rail variant, framed) for one prototype state. */
export function renderWizard({ scenario, startStep, defaultView, height }) {
  const { Component, template } = loadWizard();
  const props = { variant: 'rail', scenario, startStep, defaultView, framed: true, height };
  const values = new Component(props).renderVals();
  const html = render(template, values);
  assertFullyExpanded(html);
  return html;
}
