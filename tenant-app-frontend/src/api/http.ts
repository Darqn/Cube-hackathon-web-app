import { ApiError } from './errors';
import type { LocalFile } from './types';

export const BASE_URL = (process.env.EXPO_PUBLIC_API_URL || 'http://127.0.0.1:5080/api/v1').replace(/\/$/, '');
export const USE_MOCK = process.env.EXPO_PUBLIC_USE_MOCK === 'true';
let accessToken: string | null = null;
let unauthorized: (() => void) | undefined;
export function setTokens(access: string | null, _refresh?: string | null) { accessToken = access; }
export function authHeaders(): Record<string, string> { return accessToken ? { Authorization: `Bearer ${accessToken}` } : {}; }
export function setSessionHandlers(handlers: { onUnauthorized: () => void }) { unauthorized = handlers.onUnauthorized; }
export function absoluteUrl(path: string) {
  const base = new URL(BASE_URL);
  const url = new URL(path, `${base.origin}/`);
  if (url.origin !== base.origin) throw new ApiError(400, 'Файл находится на другом сервере.');
  return url.href;
}
const messages: Record<string, string> = {
  existing_tenant_requires_explicit_association: 'Организация с таким ИНН уже есть. Явно выберите её в форме подтверждения.',
  organization_inn_mismatch: 'ИНН выбранной организации не совпадает с ИНН заявки.',
  already_processed: 'Заявка уже обработана. Обновите список.',
  cannot_block_self: 'Нельзя заблокировать собственную учётную запись.',
  premises_has_active_lease: 'У помещения есть действующий договор. Сначала завершите договор.',
  premises_unavailable: 'Помещение недоступно для привязки. Проверьте статус и действующие договоры.',
  active_lease_required: 'Нужен действующий договор аренды для выбранного помещения.',
  invalid_transition: 'Этот переход статуса недоступен. Обновите карточку.',
  staff_creation_failed: 'Не удалось создать сотрудника. Проверьте уникальность логина и email и сложность пароля.',
  invalid_assignee: 'Выберите активного сотрудника или администратора.',
  tenant_not_found: 'Арендатор не найден. Обновите справочники.',
  file_not_found: 'Файл не найден, ожидает проверки или принадлежит другому арендатору.',
  acknowledgement_not_required: 'Для этой новости подтверждение не требуется.',

  invalid_credentials: 'Неверный логин или пароль, либо доступ ещё не подтверждён администратором.',
  registration_invalid_or_duplicate: 'Проверьте логин, email и сложность пароля. Возможно, аккаунт уже существует.',
  invalid_inn: 'Проверьте ИНН: неверные цифры или контрольная сумма.',
  invalid_reading: 'Показание должно быть не меньше предыдущего и не больше 999999999999.',
  payment_provider_not_configured: 'Онлайн-оплата пока недоступна. Обратитесь в бухгалтерию.',
  request_closed: 'Обращение закрыто. Создайте новое обращение.',
  stale_version: 'Данные изменились. Обновите страницу и повторите действие.',
  database_constraint_or_concurrency_conflict: 'Данные изменились или конфликтуют с существующими. Обновите страницу.',
  unsupported_or_invalid_file: 'Формат файла не поддерживается или содержимое не соответствует расширению.',
  invalid_office_document: 'Некорректный документ Office.',
  unsafe_office_archive: 'Документ Office не прошёл проверку безопасности.',
  invalid_file_type: 'Этот формат файла не поддерживается. Используйте PDF, JPEG или PNG.',
  file_not_ready: 'Файл ожидает проверки и пока недоступен.',
};
export async function checkedFetch(url: string, options: RequestInit = {}, auth = true): Promise<Response> {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), 30000);
  let res: Response;
  try { res = await fetch(url, { ...options, signal: controller.signal }); }
  catch { throw new ApiError(0, 'Сервер недоступен. Проверьте соединение и повторите.'); }
  finally { clearTimeout(timer); }
  if (res.status === 401 && auth) unauthorized?.();
  if (!res.ok) {
    let code = '';
    try { const data = await res.json(); code = data.code ?? data.title ?? ''; } catch { /* empty body */ }
    const defaults: Record<number, string> = { 401: 'Сессия истекла. Войдите снова.', 403: 'Нет доступа к этому действию.', 404: 'Запись или файл не найдены.', 413: 'Размер файла не должен превышать 20 МиБ.', 429: 'Слишком много запросов. Подождите минуту.', 400: 'Проверьте заполненные поля.', 409: 'Конфликт данных. Обновите страницу.' };
    throw new ApiError(res.status, messages[code] ?? defaults[res.status] ?? 'Не удалось выполнить запрос. Попробуйте позже.');
  }
  return res;
}
export async function request<T>(method: string, path: string, opts: { body?: unknown; form?: FormData; auth?: boolean; headers?: Record<string, string> } = {}): Promise<T> {
  const headers = { Accept: 'application/json', ...(opts.auth === false ? {} : authHeaders()), ...(opts.body === undefined ? {} : { 'Content-Type': 'application/json' }), ...opts.headers };
  const res = await checkedFetch(`${BASE_URL}${path}`, { method, headers, body: opts.form ?? (opts.body === undefined ? undefined : JSON.stringify(opts.body)) }, opts.auth !== false);
  return res.status === 204 || res.headers.get('content-length') === '0' ? undefined as T : await res.json();
}
export async function fileForm(file: LocalFile) {
  const form = new FormData();
  if (typeof document !== 'undefined') {
    const blob = await (await fetch(file.uri)).blob();
    if (blob.size > 20 * 1024 * 1024) throw new ApiError(413, 'Размер файла не должен превышать 20 МиБ.');
    form.append('file', blob, file.name);
  } else form.append('file', { uri: file.uri, name: file.name, type: file.mimeType ?? 'application/octet-stream' } as unknown as Blob);
  return form;
}
