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
  const s = Math.round(value).toString().replace(/\B(?=(\d{3})+(?!\d))/g, '\u00A0');
  return `${s}\u00A0₽`;
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
