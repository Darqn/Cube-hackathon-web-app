import { test, afterEach } from 'node:test';
import assert from 'node:assert/strict';
import { backend } from '../src/api/backend';
import { setTokens, setSessionHandlers, absoluteUrl, request } from '../src/api/http';
const original = globalThis.fetch;
afterEach(() => { globalThis.fetch = original; setTokens(null); });
function fake(handler: (url: string, init: RequestInit) => unknown) {
 globalThis.fetch = (async (url, init = {}) => {
  const value = handler(String(url), init);
  return value instanceof Response ? value : new Response(JSON.stringify(value), { headers: { 'Content-Type': 'application/json' } });
 }) as typeof fetch;
}
test('login sends loginName, no nonexistent refresh endpoint', async () => {
 fake((url, init) => { assert.ok(url.endsWith('/api/v1/auth/login')); assert.deepEqual(JSON.parse(init.body as string), { loginName: 'tenant', password: 'pass' }); assert.equal((init.headers as Record<string,string>).Authorization, undefined); return { accessToken: 'opaque' }; });
 assert.equal((await backend.login('tenant', 'pass')).accessToken, 'opaque');
});
test('registration preserves credentials and pending response', async () => {
 fake((url, init) => { assert.ok(url.endsWith('/auth/register')); assert.equal(JSON.parse(init.body as string).login, 'tenant'); return { status: 'PendingApproval' }; });
 await backend.register({ login: 'tenant', password: 'Strong-Pass123!', email: 'a@example.test', phone: '1', fullName: 'Tenant', organization: 'Org', inn: '7707083893' });
});
test('invoices unwrap pages and map partial payment and private PDF', async () => {
 fake(() => ({ total: 1, items: [{ id: 'i', number: '1', kind: 'VehicleAccess', issuedOn: '2026-01-01', dueOn: '2026-02-01', amount: 100, paid: 20, status: 'PartiallyPaid', fileId: 'f', canPay: false }] }));
 const [i] = await backend.invoices(); assert.equal(i.kind, 'parking'); assert.equal(i.status, 'partial'); assert.equal(i.paidAmount, 20); assert.equal(i.pdfUrl, '/api/v1/invoices/i/file'); assert.equal(i.canPay, false);
});
test('requests load subsequent pages and map category GUID/status', async () => {
 fake(url => url.endsWith('/request-categories') ? [{ id: 'cat', code: 'water-heating' }] : { total: 101, items: Array.from({ length: url.includes('page=1&') ? 100 : 1 }, (_, i) => ({ id: `r${i}`, categoryId: 'cat', status: 'Clarification', createdAt: '2026-01-01' })) });
 const rows = await backend.requests(); assert.equal(rows.length, 101); assert.equal(rows[0].category, 'water'); assert.equal(rows[0].status, 'need_info');
});
test('request details supply conversation/history and authorized attachment routes', async () => {
 fake(url => url.endsWith('/request-categories') ? [{ id: 'cat', code: 'rent' }] : url.endsWith('/auth/me') ? { id: 'me' } : { request: { id: 'r', categoryId: 'cat', status: 'New' }, messages: [{ id: 'm', authorId: 'me', text: 'Hello', createdAt: '2026-01-01' }], history: [{ id: 'h', toStatus: 'Accepted', reason: 'OK', createdAt: '2026-01-02' }], attachments: [{ id: 'a', fileId: 'f' }] });
 assert.equal((await backend.request('r')).attachments[0].url, '/api/v1/files/f');
 const messages = await backend.requestMessages('r'); assert.equal(messages[0].author, 'tenant'); assert.equal(messages[1].status, 'accepted');
});
test('meter submission selects actual meter ID, sends JSON value/photoId', async () => {
 fake((url, init) => { if (url.endsWith('/meters')) return [{ id: 'meter', premisesId: 'premise', type: 'Water', active: true }]; assert.ok(url.endsWith('/meters/meter/readings')); assert.deepEqual(JSON.parse(init.body as string), { value: 42, photoId: null }); return {}; });
 await backend.sendMeter({ premiseId: 'premise', meterType: 'water', value: 42, date: '2026-01-01' });
});
test('meter recognition sends an authorized multipart image and preserves the suggestion or fallback', async () => {
 const previousDocument = globalThis.document;
 globalThis.document = {} as Document;
 setTokens('ocr-session');
 try {
  let fallback = false;
  fake((url, init) => {
   if (url === 'blob:meter-photo') return new Response(new Uint8Array([137, 80, 78, 71]), { headers: { 'Content-Type': 'image/png' } });
   assert.ok(url.endsWith('/meters/recognize'));
   assert.equal(init.method, 'POST');
   assert.equal((init.headers as Record<string,string>).Authorization, 'Bearer ocr-session');
   assert.equal((init.headers as Record<string,string>)['Content-Type'], undefined);
   const file = (init.body as FormData).get('file') as File;
   assert.equal(file.name, 'meter.png'); assert.equal(file.type, 'image/png'); assert.equal(file.size, 4);
   return fallback ? { value: null, message: 'Введите показание вручную' } : { value: '12480', message: null };
  });
  const file = { uri: 'blob:meter-photo', name: 'meter.png', mimeType: 'image/png' };
  assert.deepEqual(await backend.recognizeMeter(file), { value: '12480', message: null });
  fallback = true;
  assert.deepEqual(await backend.recognizeMeter(file), { value: null, message: 'Введите показание вручную' });
 } finally {
  if (previousDocument === undefined) Reflect.deleteProperty(globalThis, 'document');
  else globalThis.document = previousDocument;
 }
});
test('401 clears session without refresh/retrying writes; ProblemDetails translated', async () => {
 let calls = 0; let cleared = false; setTokens('token'); setSessionHandlers({ onUnauthorized: () => { cleared = true; } });
 fake((_url, init) => { calls++; assert.equal((init.headers as Record<string,string>).Authorization, 'Bearer token'); return new Response(JSON.stringify({ code: 'invalid_credentials' }), { status: 401 }); });
 await assert.rejects(request('POST', '/requests', { body: {} }), /Неверный логин/); assert.equal(calls, 1); assert.ok(cleared);
});
test('news read and acknowledgement are distinct and reflect receipts', async () => {
 const n = { id: 'n', title: 'News', category: 'PlannedWork', importance: 'Important', publishAt: '2026-01-01', requiresAcknowledgement: true }; let read = false;
 fake(url => { if (url.endsWith('/read')) { read = true; return new Response(null, { status: 204 }); } return url.includes('/news?') ? { total: 1, items: [{ news: n, acknowledgedAt: '2026-01-02', readAt: null }] } : n; });
 const item = await backend.newsItem('n'); assert.ok(read); assert.ok(item.acknowledged); assert.ok(item.requiresAcknowledgement); assert.equal(item.category, 'planned');
});
test('never attach authorization to an external file origin', () => {
 assert.equal(absoluteUrl('/api/v1/files/f'), 'http://127.0.0.1:5080/api/v1/files/f'); assert.throws(() => absoluteUrl('https://example.com/private'), /другом сервере/);
});
