import { ApiError } from './errors';
import { request, fileForm } from './http';
import type * as T from './types';
// Wire types reflect ASP.NET's camelCase JSON; adapters keep screen models stable.
type Entity = { id: string; createdAt: string };
type Premises = Entity & { name: string; type: string; building: string; floor: number; area: number; monthlyRent: number; utilities: string; description: string; status: string };
type Lease = { number: string; signedOn: string; startsOn: string; endsOn: string; status: string };
type Category = { id: string; code: string };
type Ticket = Entity & { categoryId: string; premisesId?: string; subject: string; description: string; contactName: string; phone: string; email: string; status: string };
type Detail = { request: Ticket; messages: (Entity & { authorId: string; text: string })[]; history: (Entity & { toStatus: string; reason: string })[]; attachments: { id: string; fileId: string }[] };
export type Meter = Entity & { premisesId: string; type: string; serialNumber: string; lastValue: number; active: boolean };
type WireNews = Entity & { title: string; body: string; category: string; importance: string; publishAt: string; requiresAcknowledgement: boolean };
type Receipt = { news: WireNews; readAt: string | null; acknowledgedAt: string | null };
const get = <V,>(path: string) => request<V>('GET', path);
// Both array and {total,items} endpoints are paginated by the server, max 100.
async function all<V>(path: string): Promise<V[]> {
  const out: V[] = [];
  for (let page = 1; ; page++) {
    const data = await get<V[] | { items: V[]; total: number }>(`${path}${path.includes('?') ? '&' : '?'}page=${page}&size=100`);
    const items = Array.isArray(data) ? data : data.items;
    out.push(...items);
    if (items.length === 0 || (Array.isArray(data) ? items.length < 100 : out.length >= data.total)) return out;
  }
}
const code = (c: string): T.RequestCategory => c === 'water-heating' ? 'water' : c as T.RequestCategory;
const states: Record<string, T.RequestStatus> = { New: 'new', Accepted: 'accepted', InProgress: 'in_progress', Clarification: 'need_info', Completed: 'done', Closed: 'closed', Rejected: 'rejected' };
const premiseType = (s: string): T.PremiseType => ({ Office: 'office', Warehouse: 'warehouse', Retail: 'retail' } as const)[s] ?? 'office';
async function categories() { return get<Category[]>('/request-categories'); }
function ticket(r: Ticket, cats: Category[], attachments: T.Attachment[] = []): T.RequestItem {
  return { ...r, number: r.id.slice(0, 8), category: code(cats.find(c => c.id === r.categoryId)?.code ?? 'rent'), premiseId: r.premisesId, status: states[r.status] ?? 'new', updatedAt: r.createdAt, attachments };
}
async function upload(file: T.LocalFile) {
  const f = await request<{ id: string; state: string }>('POST', '/files', { form: await fileForm(file) });
  if (f.state !== 'Ready') throw new ApiError(409, 'Файл загружен и ожидает проверки. Прикрепить его можно после проверки.');
  return f.id;
}
async function attach(id: string, files: T.LocalFile[]) {
  for (const file of files) await request('POST', `/requests/${id}/attachments`, { body: { fileId: await upload(file) } });
}
const news = (n: WireNews, read = false, acknowledged = false): T.NewsItem => ({ id: n.id, title: n.title, body: n.body, category: ({ Emergency: 'accident', PlannedWork: 'planned', General: 'announcement' } as const)[n.category] ?? 'other', urgent: n.importance === 'Urgent', createdAt: n.publishAt, read, acknowledged, requiresAcknowledgement: n.requiresAcknowledgement });
async function newsList() { return (await all<Receipt>('/news')).map(x => news(x.news, !!x.readAt, !!x.acknowledgedAt)); }
async function listing(x: { listing: { id: string; title: string; description: string }; premises: Premises }): Promise<T.Listing> {
  const photos = await get<{ url: string }[]>(`/listings/${x.listing.id}/photos`);
  return { id: x.listing.id, name: x.listing.title, description: x.listing.description, type: premiseType(x.premises.type), building: x.premises.building, floor: x.premises.floor, area: x.premises.area, price: x.premises.monthlyRent, utilities: x.premises.utilities, status: x.premises.status === 'Reserved' ? 'reserved' : 'free', photos: photos.map(p => p.url) };
}
export const backend = {
  login: (loginName: string, password: string) => request<T.LoginResult>('POST', '/auth/login', { body: { loginName, password }, auth: false }),
  logout: () => request<void>('POST', '/auth/logout'),
  register: async (data: T.RegistrationInput) => { await request('POST', '/auth/register', { body: data, auth: false }); },
  forgotPassword: async (email: string) => { await request('POST', '/auth/forgot-password', { body: { email }, auth: false }); },
  profile: async (): Promise<T.Profile> => {
    const p = await get<{ id: string; userName: string; email: string; fullName: string; phoneNumber: string; role: string }>('/auth/me');
    if (p.role !== 'Tenant') throw new ApiError(403, 'Этот кабинет предназначен для арендаторов. Для сотрудников используйте административный API.');
    const d = await get<{ organization: { name: string; inn: string } }>('/dashboard');
    return { id: p.id, login: p.userName, email: p.email, contactName: p.fullName, phone: p.phoneNumber, orgName: d.organization.name, inn: d.organization.inn };
  },
  myPremises: async (): Promise<T.MyPremise[]> => (await all<{ lease: Lease; premises: Premises }>('/me/leases')).map(({ lease: l, premises: p }) => ({ id: p.id, name: p.name, type: premiseType(p.type), building: p.building, floor: p.floor, area: p.area, contractNumber: l.number, contractDate: l.signedOn, contractFrom: l.startsOn, contractTo: l.endsOn, status: l.status !== 'Active' || l.endsOn < new Date().toISOString().slice(0, 10) ? 'terminated' : Date.parse(l.endsOn) - Date.now() < 30 * 86400000 ? 'expiring' : 'active' })),
  invoices: async (): Promise<T.Invoice[]> => (await all<Entity & { number: string; issuedOn: string; dueOn: string; purpose: string; kind: string; amount: number; paid: number; status: string; fileId: string | null; canPay: boolean }>('/invoices')).map(i => ({ id: i.id, number: i.number, issuedAt: i.issuedOn, dueDate: i.dueOn, purpose: i.purpose, kind: ({ Rent: 'rent', Utilities: 'utilities', VehicleAccess: 'parking' } as const)[i.kind] ?? 'other', amount: i.amount, paidAmount: i.paid, status: ({ Paid: 'paid', PartiallyPaid: 'partial', Overdue: 'overdue' } as const)[i.status] ?? 'unpaid', pdfUrl: i.fileId ? `/api/v1/invoices/${i.id}/file` : null, canPay: i.canPay })),
  createPayment: async (id: string): Promise<T.PaymentSession> => { const r = await request<{ checkoutUrl: string }>('POST', `/invoices/${id}/payment-attempts`, { headers: { 'Idempotency-Key': `${Date.now()}-${Math.random().toString(36).slice(2)}` } }); return { paymentUrl: r.checkoutUrl }; },
  meters: () => get<Meter[]>('/meters'),
  meterPrevious: async (premiseId: string, type: T.MeterType) => (await findMeter(premiseId, type)).lastValue,
  meterHistory: async (): Promise<T.MeterReading[]> => {
    const [meters, premises] = await Promise.all([get<Meter[]>('/meters'), backend.myPremises()]);
    return (await Promise.all(meters.map(async m => (await all<Entity & { previousValue: number; value: number; photoId?: string }>(`/meters/${m.id}/readings`)).map(r => ({ id: r.id, premiseId: m.premisesId, premiseName: premises.find(p => p.id === m.premisesId)?.name ?? m.serialNumber, meterType: (m.type === 'Water' ? 'water' : 'electricity') as T.MeterType, previous: r.previousValue, value: r.value, date: r.createdAt, photoUrl: r.photoId ? `/api/v1/files/${r.photoId}` : null }))))).flat().sort((a, b) => b.date.localeCompare(a.date));
  },
  recognizeMeter: async (file: T.LocalFile): Promise<{ value: string | null; message: string | null }> =>
    request<{ value: string | null; message: string | null }>('POST', '/meters/recognize', { form: await fileForm(file) }),
  sendMeter: async (i: T.NewMeterReading) => { const m = i.meterId ? { id: i.meterId } : await findMeter(i.premiseId, i.meterType); await request('POST', `/meters/${m.id}/readings`, { body: { value: i.value, photoId: i.photo ? await upload(i.photo) : null } }); },
  requests: async () => { const [rows, cats] = await Promise.all([all<Ticket>('/requests'), categories()]); return rows.map(r => ticket(r, cats)); },
  request: async (id: string) => { const [d, cats] = await Promise.all([get<Detail>(`/requests/${id}`), categories()]); return ticket(d.request, cats, d.attachments.map(a => ({ id: a.id, name: `Вложение ${a.fileId.slice(0, 8)}`, url: `/api/v1/files/${a.fileId}` }))); },
  requestMessages: async (id: string): Promise<T.RequestMessage[]> => {
    const [d, me] = await Promise.all([get<Detail>(`/requests/${id}`), get<{ id: string }>('/auth/me')]);
    return [...d.messages.map(m => ({ ...m, requestId: id, kind: 'message' as const, author: m.authorId === me.id ? 'tenant' as const : 'staff' as const, authorName: m.authorId === me.id ? 'Вы' : 'Участник обращения', attachments: [] })), ...d.history.map(h => ({ id: h.id, createdAt: h.createdAt, requestId: id, kind: 'status' as const, author: 'staff' as const, authorName: 'Сотрудник', text: h.reason, status: states[h.toStatus], attachments: [] }))].sort((a, b) => a.createdAt.localeCompare(b.createdAt));
  },
  createRequest: async (i: T.NewRequest) => {
    // Upload first: a rejected file must not silently create a duplicate request on retry.
    const fileIds = []; for (const f of i.files) fileIds.push(await upload(f));
    const cats = await categories(); const cat = cats.find(c => code(c.code) === i.category);
    if (!cat) throw new ApiError(400, 'Категория недоступна. Выберите другую.');
    const r = await request<Ticket>('POST', '/requests', { body: { categoryId: cat.id, premisesId: i.premiseId || null, subject: i.subject, description: i.description, contactName: i.contactName, phone: i.phone, email: i.email } });
    try { for (const fileId of fileIds) await request('POST', `/requests/${r.id}/attachments`, { body: { fileId } }); }
    catch { throw new ApiError(409, `Обращение ${r.id.slice(0, 8)} создано, но часть вложений не прикреплена. Откройте его в списке; не создавайте повторно.`); }
    return ticket(r, cats);
  },
  sendMessage: async (id: string, text: string, files: T.LocalFile[]) => { if (!text.trim() && !files.length) throw new ApiError(400, 'Добавьте текст или файл.'); await attach(id, files); if (text.trim()) await request('POST', `/requests/${id}/messages`, { body: { text, internal: false } }); },
  documents: async (): Promise<T.DocumentItem[]> => (await all<Entity & { kind: string; title: string; documentDate: string; number: string; status: string }>('/documents')).map(d => ({ id: d.id, title: d.title, date: d.documentDate, contractNumber: d.number, category: ({ Contract: 'contract', Addendum: 'addendum', Statement: 'statement', Act: 'act', Invoice: 'invoice', UPD: 'invoice' } as const)[d.kind] ?? 'other', status: ({ UnderReview: 'review', Accepted: 'accepted', Rejected: 'rejected', Expired: 'expired' } as const)[d.status] ?? 'active', url: `/api/v1/documents/${d.id}/file` })),
  uploadStatement: async (file: T.LocalFile) => { await request('POST', '/statements', { body: { fileId: await upload(file), title: file.name, leaseId: null, premisesId: null } }); },
  statementTemplate: async () => { const d = (await all<{ id: string; kind: string }>('/documents')).find(d => d.kind === 'Template'); if (!d) throw new ApiError(404, 'Шаблон пока не опубликован. Обратитесь к администратору.'); return `/api/v1/documents/${d.id}/file`; },
  listings: async () => Promise.all((await all<Parameters<typeof listing>[0]>('/listings')).map(listing)),
  listing: async (id: string) => listing(await get<Parameters<typeof listing>[0]>(`/listings/${id}`)),
  inquire: async (id: string, contactName: string, phone: string, email: string) => { await request('POST', `/listings/${id}/inquiries`, { body: { contactName, phone, email, message: 'Прошу связаться со мной по вопросу аренды.' } }); },
  news: newsList,
  newsItem: async (id: string) => { const [n, list] = await Promise.all([get<WireNews>(`/news/${id}`), newsList()]); await request('POST', `/news/${id}/read`); return news(n, true, list.find(x => x.id === id)?.acknowledged); },
  acknowledgeNews: (id: string) => request<void>('POST', `/news/${id}/acknowledge`),
  readAllNews: async () => { for (const n of await newsList()) if (!n.read) await request('POST', `/news/${n.id}/read`); },
  notifications: async (): Promise<T.AppNotification[]> => (await all<Entity & { kind: string; text: string; read: boolean; resourceId?: string }>('/notifications')).map(n => ({ ...n, title: ({ InvoiceCreated: 'Новый счёт', RequestStatus: 'Статус обращения', RequestMessage: 'Ответ на обращение', ReadingReviewed: 'Показания проверены', StatementReviewed: 'Заявление рассмотрено' } as Record<string, string>)[n.kind] ?? 'Уведомление', text: ({ InvoiceCreated: 'Для вас выставлен новый счёт.', RequestMessage: 'В обращении появился новый ответ.', RequestStatus: 'Изменился статус вашего обращения.', ReadingReviewed: 'Проверьте результат рассмотрения показаний.' } as Record<string, string>)[n.kind] ?? n.text, link: n.kind.startsWith('Request') && n.resourceId ? `/requests/${n.resourceId}` : undefined })),
  readNotification: (id: string) => request<void>('POST', `/notifications/${id}/read`),
  readAllNotifications: async () => { for (const n of await backend.notifications()) if (!n.read) await backend.readNotification(n.id); },
};
async function findMeter(premiseId: string, type: T.MeterType) {
  const rows = (await backend.meters()).filter(m => m.active && m.premisesId === premiseId && m.type === (type === 'water' ? 'Water' : 'Electricity'));
  if (rows.length !== 1) throw new ApiError(400, rows.length ? 'В помещении несколько счётчиков этого типа. Выберите конкретный счётчик.' : 'В этом помещении нет активного счётчика выбранного типа.');
  return rows[0];
}
