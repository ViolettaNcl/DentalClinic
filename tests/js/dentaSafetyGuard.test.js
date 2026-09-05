import test from 'node:test';
import assert from 'node:assert/strict';
import { sanitizeDentaText } from '../../wwwroot/assets/js/core/dentaSafetyGuard.js';

const cases = [
  ['ru', 'Это абсолютно безболезненно и проходит без боли и страха.', ['абсолютно безболезненно', 'без боли и страха']],
  ['en', 'It is completely painless and happens without pain or fear.', ['completely painless', 'without pain or fear']],
  ['fr', "C'est totalement indolore et se déroule sans douleur ni crainte.", ['totalement indolore', 'sans douleur ni crainte']],
  ['el', 'Είναι εντελώς ανώδυνο και γίνεται χωρίς πόνο και φόβο.', ['εντελώς ανώδυνο', 'χωρίς πόνο και φόβο']],
  ['ar', 'الإجراء غير مؤلم تمامًا ويتم دون ألم أو خوف.', ['غير مؤلم تمامًا', 'دون ألم أو خوف']],
];

for (const [lang, input, forbidden] of cases) {
  test(`Denta safety guard removes absolute pain claims: ${lang}`, () => {
    const output = sanitizeDentaText(input);
    for (const phrase of forbidden) assert.equal(output.toLowerCase().includes(phrase.toLowerCase()), false);
    assert.ok(output.length > 0);
  });
}

test('Denta safety guard leaves ordinary text unchanged', () => {
  const input = 'The clinic is open Monday to Saturday.';
  assert.equal(sanitizeDentaText(input), input);
});
