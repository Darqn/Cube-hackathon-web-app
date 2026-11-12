import { expect, test, type Page, type Route } from '@playwright/test';
import path from 'node:path';

const base = process.env.CUBE_E2E_API!;
const password = process.env.CUBE_E2E_PASSWORD!;
const example = path.resolve(__dirname, '../../../meter-example.png');
const recognitionRoute = '**/api/v1/meters/recognize';
test.describe.configure({ mode: 'serial' });
test.setTimeout(45000);
let token: string;
let tenantToken: string;
let meterId: string;

test.beforeAll(async ({ request }) => {
  const call = async (route: string, data?: unknown) => {
    const response = await request.fetch(`${base}${route}`, {
      method: data ? 'POST' : 'GET', data,
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    });
    expect(response.ok(), `${route}: ${await response.text()}`).toBeTruthy();
    return response.json();
  };
  token = (await call('/auth/login', { loginName: 'frontend-admin', password: process.env.CUBE_E2E_ADMIN })).accessToken;
  await call('/auth/register', { login: 'ocr-tenant', password, email: 'ocr@example.test', phone: '1234567', fullName: 'Проверка OCR', organization: 'Организация OCR', inn: '1234567894' });
  const applicant = (await call('/admin/registration-applications')).find((user: { userName: string }) => user.userName === 'ocr-tenant');
  expect(applicant).toBeTruthy();
  await call(`/admin/registration-applications/${applicant.id}/approve`, { existingTenantId: null });
  const tenantId = (await call('/admin/users')).find((user: { id: string }) => user.id === applicant.id).tenantId;
  const premise = await call('/admin/premises', { name: 'Офис OCR', type: 'Office', address: 'Тестовый адрес', building: 'OCR', section: 'А', floor: 1, area: 20, monthlyRent: 1000, description: 'OCR', utilities: 'По счётчикам', status: 'Available' });
  await call('/admin/leases', { tenantId, premisesId: premise.id, number: 'OCR-001', signedOn: '2026-01-01', startsOn: '2026-01-01', endsOn: '2030-12-31' });
  const meter = await call('/admin/meters', { tenantId, premisesId: premise.id, type: 'Electricity', serialNumber: 'OCR-ELECTRICITY', unit: 'kWh', baseline: 100 });
  meterId = meter.id;
  await call('/admin/meters', { tenantId, premisesId: premise.id, type: 'Water', serialNumber: 'OCR-WATER', unit: 'm3', baseline: 10 });
  tenantToken = (await call('/auth/login', { loginName: 'ocr-tenant', password })).accessToken;
});

async function openForm(page: Page) {
  await page.goto('/');
  await page.getByRole('textbox', { name: 'Логин', exact: true }).fill('ocr-tenant');
  await page.locator('input[aria-label="Пароль"]:visible').fill(password);
  await page.getByRole('button', { name: 'Войти', exact: true }).click();
  await page.getByRole('button', { name: 'Передать показания', exact: true }).click();
  await expect(page.getByLabel('Предыдущее показание', { exact: true })).toHaveValue(/^(100|12481)$/);
  await expect(page.getByRole('button', { name: 'Распознать показания', exact: true })).toBeDisabled();
  const choose = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Файл', exact: true }).click();
  await (await choose).setFiles(example);
  await expect(page.getByText('meter-example.png', { exact: true })).toBeVisible();
}

test('real OCR suggests a value, requires confirmation and keeps the reading watermark', async ({ page, request }) => {
  await openForm(page);
  const recognized = page.waitForResponse(response => response.url().endsWith('/meters/recognize'));
  await page.getByRole('button', { name: 'Распознать показания', exact: true }).click();
  expect(await (await recognized).json()).toEqual({ value: '12480', message: null });
  await expect(page.getByLabel('Новое показание, кВт·ч')).toHaveValue('12480');
  await expect(page.getByText('Проверьте распознанное показание перед отправкой', { exact: true })).toBeVisible();
  let readings = await request.get(`${base}/meters/${meterId}/readings`, { headers: { Authorization: `Bearer ${tenantToken}` } });
  expect(readings.ok()).toBeTruthy();
  expect(await readings.json()).toHaveLength(0);
  // Correct the suggestion before submitting; OCR itself must not save anything.
  await page.getByLabel('Новое показание, кВт·ч').fill('12481');
  await page.getByRole('button', { name: 'Отправить', exact: true }).click();
  await expect(page.getByText(/стало 12481/).first()).toBeVisible();
  readings = await request.get(`${base}/meters/${meterId}/readings`, { headers: { Authorization: `Bearer ${tenantToken}` } });
  expect(readings.ok()).toBeTruthy();
  expect((await readings.json())[0].value).toBe(12481);
});

test('failed recognition preserves manual input and water remains manual', async ({ page }) => {
  await openForm(page);
  await page.getByLabel('Новое показание, кВт·ч').fill('13000');
  await page.route(recognitionRoute, route => route.fulfill({ json: { value: null, message: 'Не удалось уверенно распознать. Введите показание вручную' } }));
  await page.getByRole('button', { name: 'Распознать показания', exact: true }).click();
  await expect(page.getByText('Не удалось уверенно распознать. Введите показание вручную', { exact: true })).toBeVisible();
  await expect(page.getByLabel('Новое показание, кВт·ч')).toHaveValue('13000');
  await page.getByRole('button', { name: 'Вода', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Распознать показания', exact: true })).toBeDisabled();
  await expect(page.getByText('Распознавание пока доступно только для электросчётчика.', { exact: true })).toBeVisible();
});

for (const change of ['manual edit', 'remove photo']) {
  test(`late OCR response cannot overwrite a newer ${change}`, async ({ page }) => {
    await openForm(page);
    let release!: () => void;
    let received!: (route: Route) => void;
    const gate = new Promise<void>(resolve => { release = resolve; });
    const started = new Promise<Route>(resolve => { received = resolve; });
    await page.route(recognitionRoute, async route => {
      received(route);
      await gate;
      await route.fulfill({ json: { value: '12480', message: null } });
    });
    const response = page.waitForResponse(result => result.url().endsWith('/meters/recognize'));
    await page.getByLabel('Новое показание, кВт·ч').fill('14000');
    await page.getByRole('button', { name: 'Распознать показания', exact: true }).click();
    await started;
    const progress = page.getByRole('button', { name: 'Распознать показания', exact: true }).getByRole('progressbar');
    await expect(progress).toBeVisible();
    if (change === 'manual edit') await page.getByLabel('Новое показание, кВт·ч').fill('14001');
    else await page.getByLabel('Убрать meter-example.png', { exact: true }).click();
    release();
    await response;
    await expect(progress).toHaveCount(0);
    await expect(page.getByLabel('Новое показание, кВт·ч')).toHaveValue(change === 'manual edit' ? '14001' : '14000');
    await expect(page.getByText('Проверьте распознанное показание перед отправкой', { exact: true })).toHaveCount(0);
  });
}
