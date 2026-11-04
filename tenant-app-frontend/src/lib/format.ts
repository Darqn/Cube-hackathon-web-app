const pad = (n: number) => String(n).padStart(2, '0');

export function fmtDate(iso?: string | null): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (isNaN(d.getTime())) return '—';
  return `${pad(d.getDate())}.${pad(d.getMonth() + 1)}.${d.getFullYear()}`;
}

export function fmtTime(iso?: string | null): string {
  if (!iso) return '';
  const d = new Date(iso);
  if (isNaN(d.getTime())) return '';
  return `${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export function fmtDateTime(iso?: string | null): string {
  return `${fmtDate(iso)} ${fmtTime(iso)}`.trim();
}

export function fmtMoney(value: number): string {
  return new Intl.NumberFormat('ru-RU', { style: 'currency', currency: 'RUB', minimumFractionDigits: Number.isInteger(value) ? 0 : 2, maximumFractionDigits: 2 }).format(value);
}

export function fmtArea(value: number): string {
  return `${value}\u00A0м²`;
}

export function toNumber(text: string): number {
  return Number(text.replace(/\s/g, '').replace(',', '.'));
}

export function todayIso(): string {
  return new Date().toISOString();
}
