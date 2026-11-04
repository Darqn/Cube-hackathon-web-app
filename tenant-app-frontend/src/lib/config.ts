import type { ComponentProps } from 'react';
import type { Ionicons } from '@expo/vector-icons';
import type {
  DocCategory,
  InvoiceKind,
  InvoiceStatus,
  ListingStatus,
  NewsCategory,
  PremiseType,
  RequestCategory,
  RequestStatus,
} from '@/api/types';

export type IconName = ComponentProps<typeof Ionicons>['name'];
export type Tone = 'neutral' | 'info' | 'warning' | 'success' | 'danger';

export const REQUEST_CATEGORIES: {
  key: RequestCategory;
  title: string;
  short: string;
  assignee: string;
  icon: IconName;
}[] = [
  { key: 'rent', title: 'По вопросу аренды', short: 'Аренда', assignee: 'Менеджер по аренде', icon: 'business-outline' },
  { key: 'electricity', title: 'По вопросу электричества', short: 'Электричество', assignee: 'Главный энергетик', icon: 'flash-outline' },
  { key: 'water', title: 'Водоснабжение и отопление', short: 'Вода и отопление', assignee: 'Начальник службы ЭМО', icon: 'water-outline' },
  { key: 'construction', title: 'По вопросу строительства', short: 'Строительство', assignee: 'Главный инженер', icon: 'hammer-outline' },
  { key: 'accounting', title: 'По вопросу бухгалтерии', short: 'Бухгалтерия', assignee: 'Бухгалтер', icon: 'calculator-outline' },
  { key: 'termination', title: 'Расторжение договора аренды', short: 'Расторжение', assignee: 'Отдел аренды', icon: 'document-text-outline' },
  { key: 'telecom', title: 'По вопросу интернета и связи', short: 'Интернет и связь', assignee: 'Системный администратор', icon: 'wifi-outline' },
];

export const categoryByKey = (key: RequestCategory) =>
  REQUEST_CATEGORIES.find((c) => c.key === key) ?? REQUEST_CATEGORIES[0];

export const REQUEST_STATUS: Record<RequestStatus, { label: string; tone: Tone }> = {
  new: { label: 'Новое', tone: 'neutral' },
  accepted: { label: 'Принято в работу', tone: 'info' },
  in_progress: { label: 'В процессе', tone: 'warning' },
  need_info: { label: 'Требуется уточнение', tone: 'danger' },
  done: { label: 'Выполнено', tone: 'success' },
  closed: { label: 'Закрыто', tone: 'neutral' },
  rejected: { label: 'Отклонено', tone: 'danger' },
};

export const REQUEST_FILTERS: { key: string; label: string; statuses: RequestStatus[] | null }[] = [
  { key: 'all', label: 'Все', statuses: null },
  { key: 'new', label: 'Новые', statuses: ['new'] },
  { key: 'work', label: 'В работе', statuses: ['accepted', 'in_progress', 'need_info'] },
  { key: 'done', label: 'Выполненные', statuses: ['done'] },
  { key: 'closed', label: 'Закрытые', statuses: ['closed', 'rejected'] },
];

export const INVOICE_STATUS: Record<InvoiceStatus, { label: string; tone: Tone }> = {
  paid: { label: 'Оплачен', tone: 'success' },
  unpaid: { label: 'Не оплачен', tone: 'danger' },
  partial: { label: 'Оплачен частично', tone: 'warning' },
  overdue: { label: 'Просрочен', tone: 'danger' },
};

export const INVOICE_KIND: Record<InvoiceKind, { label: string; icon: IconName }> = {
  rent: { label: 'Аренда', icon: 'document-text-outline' },
  utilities: { label: 'Коммунальные платежи', icon: 'water-outline' },
  parking: { label: 'Проезды автотранспорта', icon: 'car-outline' },
  other: { label: 'Иные начисления', icon: 'receipt-outline' },
};

export const DOC_TABS: { key: DocCategory; label: string; icon: IconName }[] = [
  { key: 'contract', label: 'Договоры', icon: 'document-text-outline' },
  { key: 'addendum', label: 'Доп. соглашения', icon: 'documents-outline' },
  { key: 'statement', label: 'Заявления', icon: 'create-outline' },
  { key: 'act', label: 'Акты', icon: 'checkmark-circle-outline' },
  { key: 'invoice', label: 'Счёт-фактуры / УПД', icon: 'receipt-outline' },
  { key: 'other', label: 'Прочие', icon: 'folder-outline' },
];

export const PREMISE_TYPE: Record<PremiseType, { label: string; plural: string; icon: IconName }> = {
  office: { label: 'Офис', plural: 'Офисы', icon: 'desktop-outline' },
  warehouse: { label: 'Склад', plural: 'Склады', icon: 'cube-outline' },
  retail: { label: 'Торговая площадь', plural: 'Торговые площади', icon: 'storefront-outline' },
};

export const LISTING_STATUS: Record<ListingStatus, { label: string; tone: Tone }> = {
  free: { label: 'Свободно', tone: 'success' },
  reserved: { label: 'Забронировано', tone: 'warning' },
  rented: { label: 'Сдано', tone: 'neutral' },
};

export const NEWS_CATEGORY: Record<NewsCategory, { label: string; tone: Tone; icon: IconName }> = {
  accident: { label: 'Авария', tone: 'danger', icon: 'warning-outline' },
  planned: { label: 'Плановые работы', tone: 'warning', icon: 'construct-outline' },
  announcement: { label: 'Объявление', tone: 'info', icon: 'information-circle-outline' },
  other: { label: 'Прочее', tone: 'neutral', icon: 'shield-checkmark-outline' },
};

export const PREMISE_STATUS: Record<'active' | 'expiring' | 'terminated', { label: string; tone: Tone }> = {
  active: { label: 'Действует', tone: 'success' },
  expiring: { label: 'Скоро истекает', tone: 'warning' },
  terminated: { label: 'Расторгнут', tone: 'neutral' },
};

export const DOC_STATUS: Record<'active' | 'expired' | 'review' | 'accepted' | 'rejected', { label: string; tone: Tone }> = {
  active: { label: 'Действует', tone: 'success' },
  expired: { label: 'Истёк', tone: 'neutral' },
  review: { label: 'На рассмотрении', tone: 'info' },
  accepted: { label: 'Принято', tone: 'success' },
  rejected: { label: 'Отклонено', tone: 'danger' },
};
