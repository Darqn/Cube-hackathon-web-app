import { ApiError } from './errors';
import type {
  AppNotification,
  DocumentItem,
  Invoice,
  Listing,
  LocalFile,
  LoginResult,
  MeterReading,
  MeterType,
  MyPremise,
  NewMeterReading,
  NewRequest,
  NewsItem,
  PaymentSession,
  Profile,
  RegistrationInput,
  RequestItem,
  RequestMessage,
  RequestStatus,
} from './types';

/** Тестовые данные. Живут в памяти: созданное в приложении видно до перезапуска. */

const wait = <T>(value: T, ms = 450): Promise<T> =>
  new Promise((resolve) => setTimeout(() => resolve(value), ms));

const ago = (days: number, h = 10, m = 0) => {
  const d = new Date();
  d.setDate(d.getDate() - days);
  d.setHours(h, m, 0, 0);
  return d.toISOString();
};
const ahead = (days: number) => ago(-days, 0, 0);

const PDF = 'https://www.w3.org/WAI/ER/tests/xhtml/testfiles/resources/pdf/dummy.pdf';
const photo = (seed: string) => `https://picsum.photos/seed/${seed}/800/560`;
let seq = 1000;
const uid = () => `m${++seq}`;

const profile: Profile = {
  id: 't1',
  orgName: 'ООО «СтройЛогистик»',
  inn: '2540123456',
  contactName: 'Иванов Сергей Петрович',
  phone: '+7 914 000-12-34',
  email: 'info@stroylogistik.example',
  login: 'demo',
};

let premises: MyPremise[] = [
  {
    id: 'p1', name: 'Офис 305', type: 'office', subtype: 'Офис', building: 'Главное здание', floor: 3,
    area: 42, contractNumber: '№15/2025', contractDate: '2024-12-20', contractFrom: '2025-01-01',
    contractTo: '2026-12-31', status: 'expiring',
  },
  {
    id: 'p2', name: 'Склад 12', type: 'warehouse', subtype: 'Тёплый склад', building: 'Складская зона', floor: 1,
    area: 180, contractNumber: '№21/2025', contractDate: '2025-02-13', contractFrom: '2025-02-15',
    contractTo: '2027-02-14', status: 'active',
  },
];

let invoices: Invoice[] = [
  { id: 'i1', number: '2026/11-305', issuedAt: ago(3), dueDate: ahead(7), purpose: 'Аренда за ноябрь 2026, офис 305', kind: 'rent', amount: 85000, paidAmount: 0, status: 'unpaid', pdfUrl: PDF },
  { id: 'i2', number: '2026/11-К12', issuedAt: ago(3), dueDate: ahead(7), purpose: 'Коммунальные платежи за октябрь 2026', kind: 'utilities', amount: 12400, paidAmount: 0, status: 'unpaid', pdfUrl: PDF },
  { id: 'i3', number: '2026/10-305', issuedAt: ago(33), dueDate: ago(23), purpose: 'Аренда за октябрь 2026, офис 305', kind: 'rent', amount: 85000, paidAmount: 40000, status: 'overdue', pdfUrl: PDF },
  { id: 'i4', number: '2026/10-П', issuedAt: ago(30), dueDate: ago(20), purpose: 'Проезды автотранспорта, октябрь 2026', kind: 'parking', amount: 3600, paidAmount: 3600, status: 'paid', pdfUrl: PDF },
  { id: 'i5', number: '2026/09-305', issuedAt: ago(63), dueDate: ago(53), purpose: 'Аренда за сентябрь 2026, офис 305', kind: 'rent', amount: 85000, paidAmount: 85000, status: 'paid', pdfUrl: PDF },
  { id: 'i6', number: '2026/09-Ш', issuedAt: ago(70), dueDate: ago(60), purpose: 'Пени за просрочку платежа', kind: 'other', amount: 1850, paidAmount: 1850, status: 'paid', pdfUrl: PDF },
];

let meters: MeterReading[] = [
  { id: 'r1', premiseId: 'p1', premiseName: 'Офис 305', meterType: 'electricity', previous: 12120, value: 12480, date: ago(34), photoUrl: null },
  { id: 'r2', premiseId: 'p1', premiseName: 'Офис 305', meterType: 'water', previous: 331, value: 356, date: ago(34), photoUrl: null },
  { id: 'r3', premiseId: 'p2', premiseName: 'Склад 12', meterType: 'electricity', previous: 40210, value: 41350, date: ago(35), photoUrl: null },
];

let requests: RequestItem[] = [
  {
    id: 'q1258', number: 1258, category: 'electricity', subject: 'Проблема с освещением в офисе 305',
    description: 'В офисе не работает половина светильников. Автомат не выбивает.',
    premiseId: 'p1', premiseName: 'Офис 305', status: 'in_progress', createdAt: ago(5, 9, 40), updatedAt: ago(2, 14, 5),
    contactName: profile.contactName, phone: profile.phone, email: profile.email, attachments: [],
  },
  {
    id: 'q1257', number: 1257, category: 'water', subject: 'Слабый напор воды в санузле',
    description: 'Напор воды упал со вчерашнего дня.', premiseId: 'p1', premiseName: 'Офис 305',
    status: 'done', createdAt: ago(6, 11, 15), updatedAt: ago(4, 16, 30),
    contactName: profile.contactName, phone: profile.phone, email: profile.email, attachments: [],
  },
  {
    id: 'q1256', number: 1256, category: 'rent', subject: 'Запрос на дополнительное помещение',
    description: 'Интересует склад около 100 м² в ближайшие два месяца.', premiseId: null, premiseName: null,
    status: 'new', createdAt: ago(8, 15, 0), updatedAt: ago(8, 15, 0),
    contactName: profile.contactName, phone: profile.phone, email: profile.email, attachments: [],
  },
  {
    id: 'q1250', number: 1250, category: 'accounting', subject: 'Запрос акта сверки',
    description: 'Просим подготовить акт сверки за III квартал.', premiseId: null, premiseName: null,
    status: 'closed', createdAt: ago(40, 10, 0), updatedAt: ago(36, 12, 0),
    contactName: profile.contactName, phone: profile.phone, email: profile.email, attachments: [],
  },
];

const msg = (
  requestId: string, kind: 'message' | 'status', author: 'tenant' | 'staff', authorName: string,
  text: string, createdAt: string, status?: RequestStatus,
): RequestMessage => ({ id: uid(), requestId, kind, author, authorName, text, createdAt, attachments: [], status });

let messages: RequestMessage[] = [
  msg('q1258', 'status', 'staff', 'Система', 'Обращение создано', ago(5, 9, 40), 'new'),
  msg('q1258', 'status', 'staff', 'Главный энергетик', 'Принято в работу', ago(5, 10, 5), 'accepted'),
  msg('q1258', 'message', 'staff', 'Главный энергетик', 'Добрый день! Электрик будет у вас завтра с 10:00 до 12:00. Обеспечьте, пожалуйста, доступ в помещение.', ago(5, 10, 8)),
  msg('q1258', 'message', 'tenant', profile.contactName, 'Хорошо, будем на месте.', ago(5, 10, 30)),
  msg('q1258', 'status', 'staff', 'Главный энергетик', 'Статус изменён: в процессе', ago(2, 14, 5), 'in_progress'),
  msg('q1257', 'status', 'staff', 'Система', 'Обращение создано', ago(6, 11, 15), 'new'),
  msg('q1257', 'message', 'staff', 'Начальник службы ЭМО', 'Промыли фильтр на вводе, напор восстановлен.', ago(4, 16, 20)),
  msg('q1257', 'status', 'staff', 'Начальник службы ЭМО', 'Статус изменён: выполнено', ago(4, 16, 30), 'done'),
  msg('q1256', 'status', 'staff', 'Система', 'Обращение создано', ago(8, 15, 0), 'new'),
];

let documents: DocumentItem[] = [
  { id: 'd1', category: 'contract', title: 'Договор аренды №15/2025', date: '2025-01-01', validTo: '2026-12-31', contractNumber: '№15/2025', premiseName: 'Офис 305', status: 'active', url: PDF },
  { id: 'd2', category: 'contract', title: 'Договор аренды №21/2025', date: '2025-02-15', validTo: '2027-02-14', contractNumber: '№21/2025', premiseName: 'Склад 12', status: 'active', url: PDF },
  { id: 'd3', category: 'addendum', title: 'Доп. соглашение №1 к договору №15/2025', date: '2025-03-01', contractNumber: '№15/2025', premiseName: 'Офис 305', url: PDF },
  { id: 'd4', category: 'statement', title: 'Заявление на предоставление парковочного места', date: ago(25), status: 'review', url: PDF },
  { id: 'd5', category: 'act', title: 'Акт сверки взаиморасчётов', date: ago(60), url: PDF },
  { id: 'd6', category: 'invoice', title: 'УПД №54 за октябрь 2026', date: ago(30), url: PDF },
  { id: 'd7', category: 'other', title: 'Правила внутреннего распорядка', date: '2025-01-05', url: PDF },
];

const listings: Listing[] = [
  { id: 'l1', name: 'Офис №210', type: 'office', subtype: 'Офис', area: 35, building: 'Главный корпус', floor: 2, price: 45000, utilities: 'Коммунальные услуги оплачиваются отдельно по счётчикам', description: 'Светлый офис в главном здании. Подходит для офиса продаж, бухгалтерии, административного отдела.', photos: [photo('office210a'), photo('office210b')], status: 'free', isNew: true },
  { id: 'l2', name: 'Склад №7', type: 'warehouse', subtype: 'Тёплый склад', area: 180, building: 'Складская зона', floor: 1, price: 120000, utilities: 'Отопление включено в стоимость, электричество по счётчику', description: 'Тёплый склад с воротами для фуры. Высота потолков 6 м, полы антипыль.', photos: [photo('sklad7a'), photo('sklad7b')], status: 'free', isNew: true },
  { id: 'l3', name: 'Склад №15', type: 'warehouse', subtype: 'Холодный склад', area: 250, building: 'Холодильный комплекс', floor: 1, price: 90000, description: 'Холодный склад, температурный режим +5 °C. Удобный подъезд, охраняемая территория.', photos: [photo('sklad15a')], status: 'free' },
  { id: 'l4', name: 'Торговое помещение №24', type: 'retail', area: 60, building: 'Торговый комплекс', floor: 1, price: 75000, description: 'Торговая площадь с витражным остеклением. Высокий пешеходный трафик.', photos: [photo('retail24a'), photo('retail24b')], status: 'reserved' },
  { id: 'l5', name: 'Офис №412', type: 'office', subtype: 'Офис', area: 58, building: 'Главный корпус', floor: 4, price: 62000, description: 'Офис из двух комнат с отдельным входом с лестничной площадки.', photos: [], status: 'free' },
];

let news: NewsItem[] = [
  { id: 'n1', title: 'Временное отключение воды', category: 'accident', urgent: true, read: false, acknowledged: false, createdAt: ago(0, 8, 30), body: 'В связи с порывом на территории АО «Дальреммаш» временно не будет воды во всех зданиях, кроме главного здания, где расположено кафе «Восточное». Аварийная бригада работает на месте. Время восстановления уточняется.' },
  { id: 'n2', title: 'Плановое отключение электроэнергии', category: 'planned', urgent: false, read: false, acknowledged: false, createdAt: ago(1, 16, 45), body: 'С 20:00 до 23:00 в ближайшую субботу будет проводиться плановое обслуживание электрооборудования. Просим заранее отключить чувствительное оборудование.' },
  { id: 'n3', title: 'Изменение режима работы КПП', category: 'announcement', urgent: false, read: true, acknowledged: false, createdAt: ago(3, 9, 15), body: 'С понедельника изменяется режим работы въездных КПП №1 и №2. Просим учитывать при планировании приезда.' },
  { id: 'n4', title: 'Плановые ремонтные работы', category: 'planned', urgent: false, read: true, acknowledged: false, createdAt: ago(4, 11, 20), body: 'В течение следующей недели будут проводиться работы по ремонту кровли складского комплекса. Возможен шум в рабочее время.' },
  { id: 'n5', title: 'Напоминание о передаче показаний счётчиков', category: 'other', urgent: false, read: true, acknowledged: false, createdAt: ago(6, 8, 0), body: 'Просим передать показания приборов учёта воды и электричества до 25 числа включительно. Это можно сделать на экране «Передать показания».' },
  { id: 'n6', title: 'Временное ограничение парковки', category: 'announcement', urgent: false, read: true, acknowledged: false, createdAt: ago(7, 14, 10), body: 'В течение трёх дней будет временно ограничена парковка на внутренней территории у корпуса №2.' },
];

let notifications: AppNotification[] = [
  { id: 'a1', title: 'Новый счёт', text: 'Выставлен счёт 2026/11-305 на 85 000 ₽', createdAt: ago(3, 9, 0), read: false, link: '/invoices' },
  { id: 'a2', title: 'Срочное уведомление', text: 'Временное отключение воды', createdAt: ago(0, 8, 31), read: false, link: '/news/n1' },
  { id: 'a3', title: 'Новое сообщение', text: 'Главный энергетик ответил по обращению №1258', createdAt: ago(2, 14, 6), read: false, link: '/requests/q1258' },
  { id: 'a4', title: 'Показания приняты', text: 'Показания электричества по офису 305 приняты', createdAt: ago(34, 12, 0), read: true, link: '/meters' },
];

const clone = <T>(v: T): T => JSON.parse(JSON.stringify(v)) as T;
const notify = (title: string, text: string, link?: string) => {
  notifications = [{ id: uid(), title, text, createdAt: new Date().toISOString(), read: false, link }, ...notifications];
};

export const mock = {
  login(login: string, password: string): Promise<LoginResult> {
    if (login.trim().toLowerCase() === 'demo' && password === 'demo1234') {
      return wait({ accessToken: 'mock-token', refreshToken: 'mock-refresh' });
    }
    return wait(null, 600).then(() => {
      throw new ApiError(401, 'Неверный логин или пароль');
    });
  },
  register: (_: RegistrationInput) => wait(undefined, 700),
  forgotPassword: (_: string) => wait(undefined, 600),
  profile: () => wait(clone(profile)),

  myPremises: () => wait(clone(premises)),

  invoices: () => wait(clone(invoices)),
  createPayment(id: string): Promise<PaymentSession> {
    invoices = invoices.map((i) => (i.id === id ? { ...i, status: 'paid', paidAmount: i.amount } : i));
    return wait({ paymentUrl: 'https://example.com/' }, 600);
  },

  meterPrevious(premiseId: string, type: MeterType): Promise<number> {
    const last = meters
      .filter((m) => m.premiseId === premiseId && m.meterType === type)
      .sort((a, b) => b.date.localeCompare(a.date))[0];
    return wait(last ? last.value : type === 'water' ? 100 : 1000, 250);
  },
  recognizeMeter: async (_file: LocalFile): Promise<{ value: string | null; message: string | null }> => ({ value: null, message: 'В демонстрационном режиме распознавание недоступно. Введите показание вручную' }),
  meterHistory: () => wait(clone([...meters].sort((a, b) => b.date.localeCompare(a.date)))),
  sendMeter(input: NewMeterReading): Promise<void> {
    const premise = premises.find((p) => p.id === input.premiseId);
    const last = meters
      .filter((m) => m.premiseId === input.premiseId && m.meterType === input.meterType)
      .sort((a, b) => b.date.localeCompare(a.date))[0];
    meters = [
      {
        id: uid(), premiseId: input.premiseId, premiseName: premise?.name ?? '', meterType: input.meterType,
        previous: last ? last.value : 0, value: input.value, date: input.date, photoUrl: input.photo?.uri ?? null,
      },
      ...meters,
    ];
    notify('Показания приняты', `${premise?.name}: показания ${input.value} переданы`, '/meters');
    return wait(undefined, 700);
  },

  requests: () => wait(clone([...requests].sort((a, b) => b.createdAt.localeCompare(a.createdAt)))),
  request(id: string): Promise<RequestItem> {
    const r = requests.find((x) => x.id === id);
    if (!r) return Promise.reject(new ApiError(404, 'Обращение не найдено'));
    return wait(clone(r), 300);
  },
  requestMessages: (id: string) =>
    wait(clone(messages.filter((m) => m.requestId === id).sort((a, b) => a.createdAt.localeCompare(b.createdAt))), 300),
  createRequest(input: NewRequest): Promise<RequestItem> {
    const number = Math.max(...requests.map((r) => Number(r.number))) + 1;
    const premise = premises.find((p) => p.id === input.premiseId);
    const now = new Date().toISOString();
    const item: RequestItem = {
      id: `q${number}`, number, category: input.category, subject: input.subject, description: input.description,
      premiseId: input.premiseId ?? null, premiseName: premise?.name ?? null, status: 'new', createdAt: now, updatedAt: now,
      contactName: input.contactName, phone: input.phone, email: input.email,
      attachments: input.files.map((f) => ({ id: uid(), name: f.name, url: f.uri })),
    };
    requests = [item, ...requests];
    messages = [...messages, msg(item.id, 'status', 'staff', 'Система', 'Обращение создано', now, 'new')];
    // имитация ответа сотрудника — чтобы увидеть обновление переписки
    setTimeout(() => {
      requests = requests.map((r) => (r.id === item.id ? { ...r, status: 'accepted', updatedAt: new Date().toISOString() } : r));
      messages = [
        ...messages,
        msg(item.id, 'status', 'staff', 'Ответственный сотрудник', 'Принято в работу', new Date().toISOString(), 'accepted'),
        msg(item.id, 'message', 'staff', 'Ответственный сотрудник', 'Здравствуйте! Обращение получено, свяжемся с вами в ближайшее время.', new Date().toISOString()),
      ];
      notify('Статус обращения изменён', `Обращение №${number}: принято в работу`, `/requests/${item.id}`);
    }, 6000);
    return wait(clone(item), 800);
  },
  sendMessage(id: string, text: string, files: LocalFile[]): Promise<void> {
    messages = [
      ...messages,
      { ...msg(id, 'message', 'tenant', profile.contactName, text, new Date().toISOString()),
        attachments: files.map((f) => ({ id: uid(), name: f.name, url: f.uri })) },
    ];
    return wait(undefined, 400);
  },

  documents: () => wait(clone(documents)),
  uploadStatement(file: LocalFile): Promise<void> {
    documents = [
      { id: uid(), category: 'statement', title: file.name, date: new Date().toISOString(), status: 'review', url: file.uri },
      ...documents,
    ];
    return wait(undefined, 800);
  },
  statementTemplate: () => wait(PDF, 200),

  listings: () => wait(clone(listings)),
  listing(id: string): Promise<Listing> {
    const l = listings.find((x) => x.id === id);
    return l ? wait(clone(l), 300) : Promise.reject(new ApiError(404, 'Помещение не найдено'));
  },

  news: () => wait(clone(news)),
  newsItem(id: string): Promise<NewsItem> {
    news = news.map((n) => (n.id === id ? { ...n, read: true } : n));
    const n = news.find((x) => x.id === id);
    return n ? wait(clone(n), 250) : Promise.reject(new ApiError(404, 'Новость не найдена'));
  },
  acknowledgeNews(id: string): Promise<void> {
    news = news.map((n) => (n.id === id ? { ...n, acknowledged: true, read: true } : n));
    return wait(undefined, 300);
  },
  readAllNews(): Promise<void> {
    news = news.map((n) => ({ ...n, read: true }));
    return wait(undefined, 300);
  },

  notifications: () => wait(clone(notifications), 250),
  readNotification(id: string): Promise<void> {
    notifications = notifications.map((n) => (n.id === id ? { ...n, read: true } : n));
    return wait(undefined, 150);
  },
  readAllNotifications(): Promise<void> {
    notifications = notifications.map((n) => ({ ...n, read: true }));
    return wait(undefined, 250);
  },
};
