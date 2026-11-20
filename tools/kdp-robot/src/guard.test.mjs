import test from 'node:test';
import assert from 'node:assert/strict';
import { ALLOWED_BUTTONS, assertNotPublish, assertSafeClick, looksLikePublish, PublishRefused, refusal } from './guard.mjs';

const PUBLISH_LABELS = [
  'Publish Your Paperback Book', 'Publish your paperback', 'Publish', 'Submit for review', 'Submit',
  'Veröffentlichen', 'Taschenbuch veröffentlichen', 'Publier votre livre broché', 'Publicar tu libro de tapa blanda',
  'Pubblica il tuo libro', 'Publiceer je paperback', 'Publicera', 'Julkaise', 'Opublikuj', 'Yayınla', 'Опубликовать',
  'Soumettre', 'Enviar', 'Indienen', 'Einreichen', '出版', '公開', '发布', '提交', '게시', 'نشر',
];

test('every known Publish label is refused', () => {
  for (const label of PUBLISH_LABELS) {
    assert.ok(looksLikePublish(label), label);
    assert.throws(() => assertSafeClick(label), PublishRefused, label);
  }
});

test('every allowed button passes and none looks like Publish', () => {
  for (const name of ALLOWED_BUTTONS) {
    assert.equal(looksLikePublish(name), false, name);
    assert.doesNotThrow(() => assertSafeClick(name));
  }
});

test('publish and submit inflections and id styles are refused', () => {
  for (const t of ['Publishing options', 'Published', 'Submitted', 'Submission', 'publish_now', 'publish-button', 'submit_form']) {
    assert.ok(looksLikePublish(t), t);
    assert.throws(() => assertNotPublish([t, t], ['Some other label']), PublishRefused, t);
  }
  assert.throws(() => assertSafeClick('Publish'), PublishRefused);
});

test('only a control labelled exactly as a static field label is exempt: the publishing-rights radio', () => {
  const radio = 'I own the copyright and I hold necessary publishing rights.';
  assert.equal(refusal([radio, null, radio], radio), null);
  assert.doesNotThrow(() => assertNotPublish([radio, null, radio], [radio]));
  assert.throws(() => assertNotPublish([radio, `${radio} Publish now`], [radio]), PublishRefused);
  // a page control that names itself like the allowed label but carries Publish text is refused
  assert.ok(refusal([radio, 'Publish now'], radio));
  assert.ok(refusal([`rights = ${radio}`], radio), 'the key = label suffix form is no exemption');
  assert.ok(refusal(['Publish'], undefined));
  assert.ok(refusal(['Publish'], /publish/i));
  assert.throws(() => assertNotPublish(['x', 'Publishing rights'], [radio]), PublishRefused);
  assert.throws(() => assertNotPublish(['x', 'publish_now'], [/publishing/i]), PublishRefused);
});

test('an unlisted button is refused too', () => {
  assert.throws(() => assertSafeClick('Delete title'), PublishRefused);
  assert.throws(() => assertSafeClick(''), PublishRefused);
});
