import { ApiError } from './errors';
import type { LocalFile } from './types';

/**
 * Адрес сервера задаётся в файле .env:  EXPO_PUBLIC_API_URL=https://api.example.ru/api
 * Если он не задан, приложение работает на тестовых данных (api/mock.ts).
 */
export const BASE_URL = (process.env.EXPO_PUBLIC_API_URL ?? '').replace(/\/$/, '');
export const USE_MOCK = BASE_URL === '';

let accessToken: string | null = null;
let refreshToken: string | null = null;
let onUnauthorized: (() => void) | null = null;
let onTokens: ((access: string, refresh?: string) => void) | null = null;

export function setTokens(access: string | null, refresh?: string | null) {
  accessToken = access;
  if (refresh !== undefined) refreshToken = refresh;
}

export function setSessionHandlers(handlers: {
  onUnauthorized: () => void;
  onTokens: (access: string, refresh?: string) => void;
}) {
  onUnauthorized = handlers.onUnauthorized;
  onTokens = handlers.onTokens;
}

export function absoluteUrl(url: string): string {
  if (/^https?:\/\//i.test(url)) return url;
  return `${BASE_URL.replace(/\/api$/, '')}${url.startsWith('/') ? '' : '/'}${url}`;
}

type Options = { body?: unknown; form?: FormData; auth?: boolean };

async function refresh(): Promise<boolean> {
  if (!refreshToken) return false;
  try {
    const res = await fetch(`${BASE_URL}/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken }),
    });
    if (!res.ok) return false;
    const data = (await res.json()) as { accessToken: string; refreshToken?: string };
    accessToken = data.accessToken;
    if (data.refreshToken) refreshToken = data.refreshToken;
    onTokens?.(data.accessToken, data.refreshToken);
    return true;
  } catch {
    return false;
  }
}

export async function request<T>(
  method: string,
  path: string,
  opts: Options = {},
  retried = false,
): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' };
  if (opts.auth !== false && accessToken) headers.Authorization = `Bearer ${accessToken}`;
  if (opts.body !== undefined) headers['Content-Type'] = 'application/json';

  let res: Response;
  try {
    res = await fetch(`${BASE_URL}${path}`, {
      method,
      headers,
      body: opts.form ?? (opts.body !== undefined ? JSON.stringify(opts.body) : undefined),
    });
  } catch {
    throw new ApiError(0, 'Нет соединения с сервером. Проверьте интернет и повторите.');
  }

  if (res.status === 401 && opts.auth !== false) {
    if (!retried && (await refresh())) return request<T>(method, path, opts, true);
    onUnauthorized?.();
    throw new ApiError(401, 'Сессия истекла. Войдите снова.');
  }

  if (!res.ok) {
    let message = 'Ошибка сервера. Попробуйте позже.';
    let fields: Record<string, string> | undefined;
    try {
      const data = await res.json();
      if (typeof data?.message === 'string') message = data.message;
      if (data?.fields && typeof data.fields === 'object') fields = data.fields;
    } catch {
      /* тело ответа не JSON */
    }
    throw new ApiError(res.status, message, fields);
  }

  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

export function toForm(
  fields: Record<string, string | number | null | undefined>,
  files: Record<string, LocalFile | LocalFile[] | null | undefined> = {},
): FormData {
  const form = new FormData();
  for (const [key, value] of Object.entries(fields)) {
    if (value !== undefined && value !== null) form.append(key, String(value));
  }
  for (const [key, value] of Object.entries(files)) {
    const list = Array.isArray(value) ? value : value ? [value] : [];
    for (const f of list) {
      form.append(key, {
        uri: f.uri,
        name: f.name,
        type: f.mimeType ?? 'application/octet-stream',
      } as unknown as Blob);
    }
  }
  return form;
}
