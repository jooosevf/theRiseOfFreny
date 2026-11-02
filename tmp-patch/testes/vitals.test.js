import test from 'node:test';
import assert from 'node:assert/strict';
import { checkVitals } from '../api/_lib/vitals.js';

const start = [[0, 'max', 4], [0, 'life', 4]];

test('an old build without the life log passes until REQUIRE_VITALS is on', () => {
  assert.deepEqual(checkVitals({ gameTime: 100 }), {});
  assert.match(checkVitals({ gameTime: 100 }, { required: true }).reason, /recarregue/);
});

test('a normal run: hits, a potion, the helmet', () => {
  const vitals = [...start, [20, 'hit', 1], [30, 'hit', 2], [31, 'heal', 1], [40, 'armor', 3], [50, 'hit', 2], [60, 'fall', 1]];
  assert.deepEqual(checkVitals({ gameTime: 300, vitals, flags: [] }), {});
});

test('the run ends at the hit that empties his hearts', () => {
  const vitals = [...start, [20, 'hit', 2], [90, 'hit', 2]];
  assert.deepEqual(checkVitals({ gameTime: 90.5, vitals }), {});
});

test('frozen hearts (Cheat Engine): he keeps playing after they ran out', () => {
  const vitals = [...start, [20, 'hit', 2], [40, 'hit', 2], [60, 'hit', 1]];
  assert.match(checkVitals({ gameTime: 600, vitals }).reason, /morreu aos 40\.0/);
  assert.match(checkVitals({ gameTime: 41, vitals: vitals.slice(0, 4) }).reason ?? '', /^$/);
  assert.match(checkVitals({ gameTime: 600, vitals: vitals.slice(0, 4) }).reason, /continuou/);
});

test('the armor takes hits first, but not falls', () => {
  const armored = [...start, [5, 'armor', 3], [10, 'hit', 3], [20, 'hit', 3]];
  assert.deepEqual(checkVitals({ gameTime: 300, vitals: armored }), {});
  const falls = [...start, [5, 'armor', 3], [10, 'fall', 2], [20, 'fall', 2]];
  assert.match(checkVitals({ gameTime: 300, vitals: falls }).reason, /morreu/);
});

test('a heart bought mid-run arrives full', () => {
  const vitals = [...start, [10, 'hit', 3], [20, 'max', 5], [30, 'hit', 1]];
  assert.deepEqual(checkVitals({ gameTime: 300, vitals }), {});
});

test('impossible values are refused', () => {
  assert.match(checkVitals({ gameTime: 9, vitals: [[0, 'max', 20], [0, 'life', 20]] }).reason, /máxima/);
  assert.match(checkVitals({ gameTime: 9, vitals: [[0, 'life', 4]] }).reason, /começo/);
  assert.match(checkVitals({ gameTime: 9, vitals: [...start, [5, 'armor', 9]] }).reason, /armadura/);
  assert.match(checkVitals({ gameTime: 9, vitals: [...start, [5, 'nuke', 1]] }).reason, /inválida/);
});

test('a run the game flagged is refused', () => {
  assert.match(checkVitals({ gameTime: 9, vitals: start, flags: ['mem'] }).reason, /adulterada \(mem\)/);
});

test('a long run without a single hit goes to review', () => {
  assert.match(checkVitals({ gameTime: 600, vitals: start }).suspect, /10 min sem levar dano/);
  assert.deepEqual(checkVitals({ gameTime: 120, vitals: start }), {});
});
