import { mock } from './mock';
import { request, toForm, USE_MOCK } from './http';
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
} from './types';

/**
 * Единая точка доступа к серверу. Экраны вызывают только `api.*`.
 * Пути ниже — это контракт, который нужно согласовать с бэкендом.
 */
export const api = {
  // --- вход и регистрация ---
  login: (login: string, password: string): Promise<LoginResult> =>
    USE_MOCK ? mock.login(login, password) : request('POST', '/auth/login', { body: { login, password }, auth: false }),
  register: (data: RegistrationInput): Promise<void> =>
    USE_MOCK ? mock.register(data) : request('POST', '/auth/registration-requests', { body: data, auth: false }),
  forgotPassword: (contact: string): Promise<void> =>
    USE_MOCK ? mock.forgotPassword(contact) : request('POST', '/auth/forgot-password', { body: { contact }, auth: false }),
  profile: (): Promise<Profile> => (USE_MOCK ? mock.profile() : request('GET', '/me')),

  // --- помещения арендатора ---
  myPremises: (): Promise<MyPremise[]> => (USE_MOCK ? mock.myPremises() : request('GET', '/me/premises')),

  // --- счета и оплата ---
  invoices: (): Promise<Invoice[]> => (USE_MOCK ? mock.invoices() : request('GET', '/invoices')),
  createPayment: (id: string): Promise<PaymentSession> =>
    USE_MOCK ? mock.createPayment(id) : request('POST', `/invoices/${id}/payments`),

  // --- показания счётчиков ---
  meterPrevious: (premiseId: string, type: MeterType): Promise<number> =>
    USE_MOCK
      ? mock.meterPrevious(premiseId, type)
      : request<{ previous: number }>('GET', `/meters/previous?premiseId=${premiseId}&type=${type}`).then((r) => r.previous),
  meterHistory: (): Promise<MeterReading[]> => (USE_MOCK ? mock.meterHistory() : request('GET', '/meters/readings')),
  sendMeter: (input: NewMeterReading): Promise<void> =>
    USE_MOCK
      ? mock.sendMeter(input)
      : request('POST', '/meters/readings', {
          form: toForm(
            { premiseId: input.premiseId, meterType: input.meterType, value: input.value, date: input.date },
            { photo: input.photo },
          ),
        }),

  // --- обращения ---
  requests: (): Promise<RequestItem[]> => (USE_MOCK ? mock.requests() : request('GET', '/requests')),
  request: (id: string): Promise<RequestItem> => (USE_MOCK ? mock.request(id) : request('GET', `/requests/${id}`)),
  requestMessages: (id: string): Promise<RequestMessage[]> =>
    USE_MOCK ? mock.requestMessages(id) : request('GET', `/requests/${id}/messages`),
  createRequest: (input: NewRequest): Promise<RequestItem> =>
    USE_MOCK
      ? mock.createRequest(input)
      : request('POST', '/requests', {
          form: toForm(
            {
              category: input.category,
              subject: input.subject,
              description: input.description,
              premiseId: input.premiseId,
              contactName: input.contactName,
              phone: input.phone,
              email: input.email,
            },
            { files: input.files },
          ),
        }),
  sendMessage: (id: string, text: string, files: LocalFile[]): Promise<void> =>
    USE_MOCK
      ? mock.sendMessage(id, text, files)
      : request('POST', `/requests/${id}/messages`, { form: toForm({ text }, { files }) }),

  // --- документы ---
  documents: (): Promise<DocumentItem[]> => (USE_MOCK ? mock.documents() : request('GET', '/documents')),
  uploadStatement: (file: LocalFile): Promise<void> =>
    USE_MOCK
      ? mock.uploadStatement(file)
      : request('POST', '/documents/statements', { form: toForm({}, { file }) }),
  statementTemplate: (): Promise<string> =>
    USE_MOCK
      ? mock.statementTemplate()
      : request<{ url: string }>('GET', '/documents/statement-template').then((r) => r.url),

  // --- свободные помещения ---
  listings: (): Promise<Listing[]> => (USE_MOCK ? mock.listings() : request('GET', '/listings')),
  listing: (id: string): Promise<Listing> => (USE_MOCK ? mock.listing(id) : request('GET', `/listings/${id}`)),

  // --- новости ---
  news: (): Promise<NewsItem[]> => (USE_MOCK ? mock.news() : request('GET', '/news')),
  newsItem: (id: string): Promise<NewsItem> => (USE_MOCK ? mock.newsItem(id) : request('GET', `/news/${id}`)),
  acknowledgeNews: (id: string): Promise<void> =>
    USE_MOCK ? mock.acknowledgeNews(id) : request('POST', `/news/${id}/acknowledge`),
  readAllNews: (): Promise<void> => (USE_MOCK ? mock.readAllNews() : request('POST', '/news/read-all')),

  // --- уведомления ---
  notifications: (): Promise<AppNotification[]> =>
    USE_MOCK ? mock.notifications() : request('GET', '/notifications'),
  readNotification: (id: string): Promise<void> =>
    USE_MOCK ? mock.readNotification(id) : request('POST', `/notifications/${id}/read`),
  readAllNotifications: (): Promise<void> =>
    USE_MOCK ? mock.readAllNotifications() : request('POST', '/notifications/read-all'),
};
