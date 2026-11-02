import test from 'node:test';
import assert from 'node:assert/strict';
import { generateKeyPairSync, createVerify } from 'node:crypto';

const { privateKey, publicKey } = generateKeyPairSync('rsa', { modulusLength: 2048 });
process.env.FIREBASE_SERVICE_ACCOUNT = JSON.stringify({
  client_email: 'api@the-rise-of-freny.iam.gserviceaccount.com',
  private_key: privateKey.export({ type: 'pkcs8', format: 'pem' }),
});

// Google and Firestore, faked: the token request is checked, Firestore keeps docs in a Map
const docs = new Map();
globalThis.fetch = async (url, init = {}) => {
  if (url === 'https://oauth2.googleapis.com/token') {
    const jwt = new URLSearchParams(init.body).get('assertion');
    const [h, p, s] = jwt.split('.');
    const ok = createVerify('RSA-SHA256').update(h + '.' + p).verify(publicKey, Buffer.from(s, 'base64url'));
    assert.ok(ok, 'the JWT is signed with the service account key');
    assert.equal(JSON.parse(Buffer.from(p, 'base64url')).scope, 'https://www.googleapis.com/auth/datastore');
    return new Response(JSON.stringify({ access_token: 'tok', expires_in: 3600 }));
  }
  assert.equal(init.headers.Authorization, 'Bearer tok');
  const path = url.split('/documents')[1];
  if (path === ':runQuery') {
    const col = JSON.parse(init.body).structuredQuery.from[0].collectionId;
    const rows = [...docs].filter(([k]) => k.startsWith('/' + col + '/')).map(([k, v]) => ({ document: { name: 'x' + k, fields: v } }));
    rows.sort((a, b) => Number(b.document.fields.score?.integerValue || 0) - Number(a.document.fields.score?.integerValue || 0));
    return new Response(JSON.stringify(rows));
  }
  if (init.method === 'PATCH') { docs.set(path, JSON.parse(init.body).fields); return new Response('{}'); }
  if (init.method === 'GET') return docs.has(path) ? new Response(JSON.stringify({ fields: docs.get(path) })) : new Response('', { status: 404 });
  if (init.method === 'DELETE') { docs.delete(path); return new Response('{}'); }
  if (init.method === 'POST') { docs.set(path + '/' + docs.size, JSON.parse(init.body).fields); return new Response('{}'); }
  throw new Error('unexpected ' + init.method + ' ' + url);
};

const { needsReview, writeEntry, holdForReview } = await import('../api/_lib/review.js');
const { getDoc } = await import('../api/_lib/admin-db.js');

test('a score far above the board goes to review, a normal one does not', async () => {
  await writeEntry('k'.repeat(64), 'a', 'leaderboard_hard', 'Ana', 200000);
  assert.equal(await needsReview('b', 'leaderboard_hard', 280000), false);   // under 1.5x
  assert.equal(await needsReview('b', 'leaderboard_hard', 2154225), true);
  assert.equal(await needsReview('b', 'leaderboard_hard', 100000), false);   // under the minimum
  assert.equal(await needsReview('a', 'leaderboard_hard', 2154225), true);   // no one else to compare: 0
});

test('the server writes entries that only go up, with the signature the rules check', async () => {
  await writeEntry('k'.repeat(64), 'c', 'leaderboard_easy', 'Caio', 5000);
  await writeEntry('k'.repeat(64), 'c', 'leaderboard_easy', 'Caio', 3000);
  const e = await getDoc('leaderboard_easy/c');
  assert.equal(e.score, 5000);
  assert.match(e.sig, /^[0-9a-f]{64}$/);
});

test('a held run keeps the player name from their save', async () => {
  docs.set('/saves/d', { playerName: { stringValue: 'Dani' } });
  await holdForReview('d', { board: 'leaderboard_hard', score: 999999, gameTime: 300, kills: [] }, 250, 'daily_hard_20261029');
  const p = await getDoc('pending/d__leaderboard_hard');
  assert.equal(p.name, 'Dani');
  assert.equal(p.score, 999999);
});
