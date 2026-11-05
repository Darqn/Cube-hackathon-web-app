import { test, expect, type Page } from "@playwright/test";
const base = process.env.CUBE_E2E_API!;
const password = process.env.CUBE_E2E_PASSWORD!;
let admin: string;
let tenant: string;
let worker: string;
let requestId: string;
let meterId: string;
let readingId: string;
test.describe.configure({ mode: "serial" });
test.setTimeout(60000);
async function call(
  path: string,
  body?: unknown,
  method?: string,
  token = admin,
) {
  const r = await fetch(base + path, {
    method: method ?? (body ? "POST" : "GET"),
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: body ? JSON.stringify(body) : undefined,
  });
  expect(r.ok, `${path} status ${r.status}`).toBeTruthy();
  return r.status === 204 ? undefined : r.json();
}
const visible = (p: Page, text: string) =>
  p.getByText(text, { exact: true }).filter({ visible: true });
async function login(p: Page) {
  await p.goto("/");
  await p
    .getByRole("textbox", { name: "Логин", exact: true })
    .fill("employee-complete");
  await p.locator('input[aria-label="Пароль"]:visible').fill(password);
  await p.getByRole("button", { name: "Войти", exact: true }).click();
  await expect(p).toHaveURL(/staff\/overview/);
}
async function nav(p: Page, label: string) {
  if (
    await p
      .getByRole("button", { name: "Меню сотрудника", exact: true })
      .isVisible()
  )
    await p
      .getByRole("button", { name: "Меню сотрудника", exact: true })
      .click();
  await p.getByRole("link", { name: label, exact: true }).click();
}
async function choose(p: Page, label: string, value: string) {
  await p.getByRole("button", { name: label, exact: true }).click();
  await p.getByRole("button", { name: value, exact: true }).click();
}
async function file(p: Page, name: string) {
  const chooser = p.waitForEvent("filechooser");
  await p.getByRole("button", { name: "Файл", exact: true }).click();
  await (
    await chooser
  ).setFiles({
    name,
    mimeType: "application/pdf",
    buffer: Buffer.from("%PDF-1.4\nEmployee attachment"),
  });
}
async function send(p: Page, text: string) {
  await p
    .getByRole("textbox", { name: "Текст ответа", exact: true })
    .fill(text);
  await p.getByRole("button", { name: "Отправить ответ", exact: true }).click();
  await expect(
    p.getByRole("textbox", { name: "Текст ответа", exact: true }),
  ).toHaveValue("");
  await expect(visible(p, text)).toBeVisible();
}
test.beforeAll(async () => {
  admin = (
    await call("/auth/login", {
      loginName: "frontend-admin",
      password: process.env.CUBE_E2E_ADMIN,
    })
  ).accessToken;
  await call("/admin/staff", {
    login: "employee-complete",
    email: "complete@example.test",
    password,
    fullName: "Рабочий сотрудник",
  });
  worker = (
    await call("/auth/login", { loginName: "employee-complete", password })
  ).accessToken;
  const number = "770000035";
  const weights = [2, 4, 10, 3, 5, 9, 4, 6, 8];
  const inn =
    number +
    String(
      (weights.reduce((sum, w, i) => sum + w * Number(number[i]), 0) % 11) % 10,
    );
  await call("/auth/register", {
    login: "employee-client",
    password,
    email: "employee-client@example.test",
    fullName: "Контакт сотрудника",
    organization: "Клиент сотрудника",
    inn,
    phone: "1234567",
  });
  const applicant = (await call("/admin/registration-applications")).find(
    (r: { userName: string }) => r.userName === "employee-client",
  );
  const approved = await call(
    `/admin/registration-applications/${applicant.id}/approve`,
    { existingTenantId: null },
  );
  const tenantId = approved.tenantId;
  const staff = (await call("/admin/users")).find(
    (r: { userName: string }) => r.userName === "employee-complete",
  );
  const category = (await call("/request-categories")).find(
    (c: { code: string }) => c.code === "electricity",
  );
  await call(
    `/admin/request-categories/${category.id}/routing`,
    { responsibleUserId: staff.id },
    "PUT",
  );
  const premise = await call("/admin/premises", {
    name: "Офис сотрудника",
    type: "Office",
    address: "Примерная, 2",
    building: "Корпус сотрудника",
    section: "А",
    floor: 1,
    area: 50,
    monthlyRent: 20000,
    description: "Рабочее помещение",
    utilities: "По счётчикам",
    status: "Available",
  });
  await call("/admin/leases", {
    tenantId,
    premisesId: premise.id,
    number: "EMP-LEASE",
    signedOn: "2026-01-01",
    startsOn: "2026-01-01",
    endsOn: "2030-12-31",
  });
  const meter = await call("/admin/meters", {
    tenantId,
    premisesId: premise.id,
    type: "Electricity",
    serialNumber: "EMP-METER",
    unit: "кВт·ч",
    baseline: 0,
  });
  meterId = meter.id;
  tenant = (
    await call("/auth/login", { loginName: "employee-client", password })
  ).accessToken;
  const r = await call(
    "/requests",
    {
      categoryId: category.id,
      premisesId: premise.id,
      subject: "Рабочая задача сотрудника",
      description: "Проверить электроснабжение в офисе.",
      contactName: "Контакт сотрудника",
      phone: "1234567",
      email: "employee-client@example.test",
    },
    undefined,
    tenant,
  );
  requestId = r.id;
  await call(
    `/admin/requests/${r.id}/assignment`,
    { assigneeId: staff.id, dueAt: "2026-01-01T00:00:00Z", version: r.version },
    "PUT",
  );
  await call(
    `/requests/${r.id}/messages`,
    { text: "Уточнение от арендатора" },
    undefined,
    tenant,
  );
  const form = new FormData();
  form.append(
    "file",
    new Blob(
      [
        Buffer.from(
          "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=",
          "base64",
        ),
      ],
      { type: "image/png" },
    ),
    "meter.png",
  );
  const upload = await fetch(base + "/files", {
    method: "POST",
    headers: { Authorization: `Bearer ${tenant}` },
    body: form,
  });
  expect(upload.ok).toBeTruthy();
  const f = await upload.json();
  readingId = (
    await call(
      `/meters/${meterId}/readings`,
      { value: 314, photoId: f.id },
      undefined,
      tenant,
    )
  ).id;
});
test("employee queue, public/private file replies and status workflow", async ({
  page,
}) => {
  const errors: string[] = [];
  page.on("pageerror", (e) => errors.push(e.message));
  await login(page);
  await expect(visible(page, "Ваши направления")).toBeVisible();
  await nav(page, "Обращения");
  await choose(page, "Очередь", "Просроченные");
  await choose(page, "Категория обращения", "Вопрос по электричеству");
  await page
    .getByRole("textbox", { name: "Поиск обращений", exact: true })
    .fill("Клиент сотрудника");
  await expect(visible(page, "Рабочая задача сотрудника")).toBeVisible();
  await page.screenshot({
    path: "test-results/employee-desktop.png",
    fullPage: true,
    animations: "disabled",
  });
  await page
    .getByRole("button", { name: "Открыть обращение", exact: true })
    .click();
  await expect(visible(page, "Уточнение от арендатора")).toBeVisible();
  await file(page, "staff-public.pdf");
  await send(page, "Публичный ответ с файлом");
  await choose(page, "Вид сообщения", "Внутренняя заметка сотрудникам");
  await file(page, "staff-private.pdf");
  await send(page, "PRIVATE EMPLOYEE FILE NOTE");
  const staffDetail = await call(
    `/requests/${requestId}`,
    undefined,
    undefined,
    worker,
  );
  const hidden = staffDetail.attachments.find(
    (f: { name: string }) => f.name === "staff-private.pdf",
  );
  const publicFile = staffDetail.attachments.find(
    (f: { name: string }) => f.name === "staff-public.pdf",
  );
  expect(hidden).toBeTruthy();
  expect(publicFile).toBeTruthy();
  const t = await fetch(base + `/requests/${requestId}`, {
    headers: { Authorization: `Bearer ${tenant}` },
  });
  const body = await t.text();
  expect(body).toContain("staff-public.pdf");
  expect(body).not.toContain("staff-private.pdf");
  expect(body).not.toContain("PRIVATE EMPLOYEE FILE NOTE");
  expect(
    (
      await fetch(base + `/files/${hidden.fileId}`, {
        headers: { Authorization: `Bearer ${tenant}` },
      })
    ).status,
  ).toBe(404);
  await page
    .getByRole("button", { name: "Изменить статус", exact: true })
    .click();
  await choose(page, "Новый статус", "Принято в работу");
  await page
    .getByRole("textbox", { name: /^Причина изменения/ })
    .fill("Принято сотрудником");
  await page.getByRole("button", { name: "Сохранить", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "Закрыть форму", exact: true }),
  ).toHaveCount(0);
  await expect(visible(page, "Принято в работу").first()).toBeVisible();
  expect(errors).toEqual([]);
});
test("responsible employee downloads photo and reviews meter reading", async ({
  page,
}) => {
  await login(page);
  await nav(page, "Показания");
  await page
    .getByRole("textbox", { name: "Поиск счётчиков", exact: true })
    .fill("EMP-METER");
  await page
    .getByRole("button", { name: "Открыть показание", exact: true })
    .click();
  await expect(visible(page, "Показание EMP-METER")).toBeVisible();
  const download = page.waitForEvent("download");
  await page
    .getByRole("button", { name: "Скачать фото показания", exact: true })
    .click();
  expect((await download).suggestedFilename()).toBeTruthy();
  await page
    .getByRole("button", { name: "Рассмотреть показание", exact: true })
    .click();
  await choose(page, "Решение", "Отклонить");
  await page
    .getByRole("textbox", { name: /^Комментарий решения/ })
    .fill("Требуется уточнить фото");
  await page.getByRole("button", { name: "Сохранить", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "Закрыть форму", exact: true }),
  ).toHaveCount(0);
  await expect(
    visible(page, "Проверка завершена. Повторное рассмотрение недоступно."),
  ).toBeVisible();
  const history = await call(
    `/meters/${meterId}/readings`,
    undefined,
    undefined,
    tenant,
  );
  expect(history.find((r: { id: string }) => r.id === readingId).status).toBe(
    "Rejected",
  );
});
test("employee mobile inbox and secure logout", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await login(page);
  await nav(page, "Уведомления");
  await expect(
    page.getByRole("button", { name: "Прочитать все", exact: true }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Прочитать все", exact: true })
    .click();
  await expect(visible(page, "Новых уведомлений нет")).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth + 1,
    ),
  ).toBeTruthy();
  await page.screenshot({
    path: "test-results/employee-mobile.png",
    fullPage: true,
  });
  await page.getByRole("button", { name: "Выйти", exact: true }).click();
  await page
    .getByRole("button", { name: "Подтвердить выход", exact: true })
    .click();
  await expect(
    page.getByRole("textbox", { name: "Логин", exact: true }),
  ).toBeVisible();
  await page.goto("/staff/requests");
  await expect(page).toHaveURL(/login/);
});
