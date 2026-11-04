export interface Profile {
  id: string;
  orgName: string;
  inn: string;
  contactName: string;
  phone: string;
  email: string;
  login: string;
}

export type PremiseType = 'office' | 'warehouse' | 'retail';

export interface MyPremise {
  id: string;
  name: string;
  type: PremiseType;
  subtype?: string;
  building: string;
  floor?: number | null;
  area: number;
  contractNumber: string;
  contractDate: string;
  contractFrom: string;
  contractTo: string;
  status: 'active' | 'expiring' | 'terminated';
}

export type InvoiceKind = 'rent' | 'utilities' | 'parking' | 'other';
export type InvoiceStatus = 'paid' | 'unpaid' | 'partial' | 'overdue';

export interface Invoice {
  id: string;
  number: string;
  issuedAt: string;
  dueDate: string;
  purpose: string;
  kind: InvoiceKind;
  amount: number;
  paidAmount: number;
  status: InvoiceStatus;
  pdfUrl?: string | null;
}

export type MeterType = 'electricity' | 'water';

export interface MeterReading {
  id: string;
  premiseId: string;
  premiseName: string;
  meterType: MeterType;
  previous: number;
  value: number;
  date: string;
  photoUrl?: string | null;
}

export interface LocalFile {
  uri: string;
  name: string;
  mimeType?: string;
}

export interface NewMeterReading {
  premiseId: string;
  meterType: MeterType;
  value: number;
  date: string;
  photo?: LocalFile | null;
}

export type RequestCategory =
  | 'rent'
  | 'electricity'
  | 'water'
  | 'construction'
  | 'accounting'
  | 'termination'
  | 'telecom';

export type RequestStatus =
  | 'new'
  | 'accepted'
  | 'in_progress'
  | 'need_info'
  | 'done'
  | 'closed'
  | 'rejected';

export interface Attachment {
  id: string;
  name: string;
  url: string;
}

export interface RequestItem {
  id: string;
  number: number;
  category: RequestCategory;
  subject: string;
  description: string;
  premiseId?: string | null;
  premiseName?: string | null;
  status: RequestStatus;
  createdAt: string;
  updatedAt: string;
  contactName: string;
  phone: string;
  email: string;
  attachments: Attachment[];
}

export interface RequestMessage {
  id: string;
  requestId: string;
  kind: 'message' | 'status';
  author: 'tenant' | 'staff';
  authorName: string;
  text: string;
  createdAt: string;
  attachments: Attachment[];
  status?: RequestStatus;
}

export interface NewRequest {
  category: RequestCategory;
  subject: string;
  description: string;
  premiseId?: string | null;
  contactName: string;
  phone: string;
  email: string;
  files: LocalFile[];
}

export type DocCategory = 'contract' | 'addendum' | 'statement' | 'act' | 'invoice' | 'other';

export interface DocumentItem {
  id: string;
  category: DocCategory;
  title: string;
  date: string;
  validTo?: string | null;
  contractNumber?: string | null;
  premiseName?: string | null;
  status?: 'active' | 'expired' | 'review' | 'accepted' | 'rejected' | null;
  url: string;
}

export type ListingStatus = 'free' | 'reserved' | 'rented';

export interface Listing {
  id: string;
  name: string;
  type: PremiseType;
  subtype?: string;
  area: number;
  building: string;
  floor: number;
  price: number;
  utilities?: string;
  description: string;
  photos: string[];
  status: ListingStatus;
  isNew?: boolean;
}

export type NewsCategory = 'accident' | 'planned' | 'announcement' | 'other';

export interface NewsItem {
  id: string;
  title: string;
  category: NewsCategory;
  body: string;
  createdAt: string;
  urgent: boolean;
  read: boolean;
  acknowledged: boolean;
}

export interface AppNotification {
  id: string;
  title: string;
  text: string;
  createdAt: string;
  read: boolean;
  link?: string;
}

export interface RegistrationInput {
  email: string;
  fullName: string;
  organization: string;
  inn: string;
  phone?: string;
}

export interface LoginResult {
  accessToken: string;
  refreshToken?: string;
}

export interface PaymentSession {
  paymentUrl: string;
}
