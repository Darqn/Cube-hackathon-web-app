import { test, expect, type Page } from "@playwright/test";
const base = process.env.CUBE_E2E_API!;
const password = process.env.CUBE_E2E_PASSWORD!;
let token: string;
let tenantId: string;
let pendingId: string;
let tenantToken: string;
let requestId: string;
const inn = "500100732259";
test.describe.configure({ mode: "serial" });
test.setTimeout(60000);
async function call(path: string, body?: unknown, method?: string) {
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
async function login(page: Page, username: string, pwd: string) {
  await page.goto("/");
  await page
    .getByRole("textbox", { name: "Логин", exact: true })
    .fill(username);
  await page.locator('input[aria-label="Пароль"]:visible').fill(pwd);
  await page.getByRole("button", { name: "Войти", exact: true }).click();
  await expect(page).toHaveURL(
    username === "frontend-admin" ? /manage\/overview/ : /staff\/overview/,
  );
}
const exact = (s: string) =>
  new RegExp("^" + s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&") + "(?: \\*)?$");
async function fill(page: Page, label: string, value: string) {
  await page.getByRole("textbox", { name: exact(label) }).fill(value);
}
async function choose(page: Page, label: string, value: string) {
  await page.getByRole("button", { name: label, exact: true }).click();
  await page.getByRole("button", { name: value, exact: true }).click();
}
async function save(page: Page) {
  await page.getByRole("button", { name: "Сохранить", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "Закрыть форму", exact: true }),
  ).toHaveCount(0);
}
const text = (page: Page, s: string) =>
  page.getByText(s, { exact: true }).filter({ visible: true });
async function section(page: Page, name: string) {
  await page.getByRole("link", { name, exact: true }).click();
  await expect(text(page, name).first()).toBeVisible();
}
test.beforeAll(async () => {
  token = (
    await call("/auth/login", {
      loginName: "frontend-admin",
      password: process.env.CUBE_E2E_ADMIN,
    })
  ).accessToken;
  await call("/auth/register", {
    login: "management-tenant",
    password,
    email: "management@example.test",
    fullName: "Получатель управления",
    organization: "Организация управления",
    inn,
    phone: "1234567",
  });
  const apps = await call("/admin/registration-applications");
  pendingId = apps.find(
    (u: { userName: string }) => u.userName === "management-tenant",
  ).id;
});
test("administrator manages users, organizations, premises, leases, documents, invoices, listings and news", async ({
  page,
}) => {
  const errors: string[] = [];
  page.on("pageerror", (e) => errors.push(e.message));
  await login(page, "frontend-admin", process.env.CUBE_E2E_ADMIN!);
  await section(page, "Заявки на регистрацию");
  await page
    .getByRole("button", { name: "Подтвердить доступ", exact: true })
    .click();
  await save(page);
  const users = await call("/admin/users");
  tenantId = users.find((u: { id: string }) => u.id === pendingId).tenantId;
  expect(tenantId).toBeTruthy();
  await section(page, "Пользователи и роли");
  await page
    .getByRole("button", { name: "Создать сотрудника", exact: true })
    .click();
  await fill(page, "Логин сотрудника", "ui-engineer");
  await fill(page, "Email сотрудника", "engineer@example.test");
  await fill(page, "ФИО сотрудника", "Инженер управления");
  await page
    .locator('input[aria-label="Пароль сотрудника *"]:visible')
    .fill(password);
  await save(page);
  await expect(text(page, "Инженер управления")).toBeVisible();
  await section(page, "Помещения");
  await page.getByRole("button", { name: "Добавить", exact: true }).click();
  await fill(page, "Название помещения", "Офис управления");
  await fill(page, "Адрес", "Улица Примерная, 1");
  await fill(page, "Корпус", "Корпус Б");
  await fill(page, "Площадь, м²", "48.5");
  await fill(page, "Аренда в месяц, ₽", "45000.50");
  await fill(page, "Описание", "Офис для тестирования управления");
  await save(page);
  await expect(text(page, "Офис управления")).toBeVisible();
  await section(page, "Договоры аренды");
  await page
    .getByRole("button", { name: "Привязать помещение", exact: true })
    .click();
  await choose(page, "Арендатор", "Организация управления");
  await choose(page, "Помещение", "Офис управления");
  await fill(page, "Номер договора", "ADMIN-LEASE");
  await fill(page, "Дата заключения", "2026-01-01");
  await fill(page, "Начало аренды", "2026-01-01");
  await fill(page, "Окончание аренды", "2030-12-31");
  await save(page);
  await expect(text(page, "ADMIN-LEASE").first()).toBeVisible();
  await section(page, "Документы");
  await page
    .getByRole("button", { name: "Загрузить документ", exact: true })
    .click();
  await choose(page, "Арендатор", "Организация управления");
  await fill(page, "Название документа", "Административный договор");
  await fill(page, "Номер документа", "DOC-ADMIN");
  const chooser = page.waitForEvent("filechooser");
  await page
    .getByRole("button", { name: "Файл документа", exact: true })
    .click();
  await (
    await chooser
  ).setFiles({
    name: "admin-contract.pdf",
    mimeType: "application/pdf",
    buffer: Buffer.from("%PDF-1.4\n%%EOF"),
  });
  await save(page);
  await expect(text(page, "Административный договор")).toBeVisible();
  await section(page, "Счета");
  await page.getByRole("button", { name: "Добавить", exact: true }).click();
  await choose(page, "Арендатор", "Организация управления");
  await fill(page, "Номер счёта", "ADMIN-INVOICE");
  await fill(page, "Дата счёта", "2026-01-01");
  await fill(page, "Срок оплаты", "2030-12-31");
  await fill(page, "Назначение платежа", "Аренда управления");
  await fill(page, "Сумма, ₽", "45000.50");
  await save(page);
  await expect(text(page, "ADMIN-INVOICE")).toBeVisible();
  await section(page, "Счётчики");
  await page.getByRole("button", { name: "Добавить", exact: true }).click();
  await choose(page, "Арендатор", "Организация управления");
  await choose(page, "Помещение", "Офис управления");
  await fill(page, "Заводской номер", "ADMIN-METER");
  await save(page);
  await expect(text(page, "ADMIN-METER")).toBeVisible();
  await section(page, "Объявления");
  await page.getByRole("button", { name: "Добавить", exact: true }).click();
  await choose(page, "Помещение", "Офис управления");
  await fill(page, "Заголовок объявления", "Объявление управления");
  await fill(page, "Описание объявления", "Описание доступного помещения");
  await save(page);
  await expect(text(page, "Объявление управления")).toBeVisible();
  await section(page, "Новости и оповещения");
  await page.getByRole("button", { name: "Добавить", exact: true }).click();
  await fill(page, "Заголовок новости", "Оповещение управления");
  await fill(page, "Краткое описание", "Плановые работы");
  await fill(page, "Полный текст", "Будут проведены технические работы.");
  await page.getByRole("button", { name: "Сейчас", exact: true }).click();
  await save(page);
  await expect(text(page, "Оповещение управления")).toBeVisible();
  await section(page, "Арендаторы");
  await page
    .getByRole("textbox", { name: "Поиск", exact: true })
    .fill("Организация управления");
  await page.getByRole("button", { name: "Подробнее", exact: true }).click();
  await expect(
    page.getByText(/№ADMIN-LEASE$/).filter({ visible: true }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Закрыть карточку", exact: true })
    .click();
  await page.screenshot({
    path: "test-results/admin-desktop.png",
    fullPage: true,
  });
  await section(page, "Журнал действий");
  await expect(text(page, "Подтверждение регистрации").first()).toBeVisible();
  expect(errors).toEqual([]);
  const tenantLogin = await call("/auth/login", {
    loginName: "management-tenant",
    password,
  });
  tenantToken = tenantLogin.accessToken;
  const meters = await call("/admin/meters");
  const meter = meters.find(
    (m: { serialNumber: string }) => m.serialNumber === "ADMIN-METER",
  );
  const reading = await fetch(base + `/meters/${meter.id}/readings`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${tenantToken}`,
    },
    body: JSON.stringify({ value: 123, photoId: null }),
  });
  expect(reading.ok).toBeTruthy();
  const cats = await call("/request-categories");
  const staff = (await call("/admin/users")).find(
    (u: { userName: string }) => u.userName === "ui-engineer",
  );
  const category = cats.find((c: { code: string }) => c.code === "electricity");
  await call(
    `/admin/request-categories/${category.id}/routing`,
    { responsibleUserId: staff.id },
    "PUT",
  );
  const response = await fetch(base + "/requests", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${tenantToken}`,
    },
    body: JSON.stringify({
      categoryId: category.id,
      premisesId: null,
      subject: "Назначенная задача инженера",
      description: "Необходимо проверить электричество.",
      contactName: "Контакт",
      phone: "123",
      email: "management@example.test",
    }),
  });
  expect(response.ok).toBeTruthy();
  requestId = (await response.json()).id;
});
test("staff works on assigned requests, private notes stay private and admin endpoints deny access", async ({
  page,
}) => {
  await login(page, "ui-engineer", password);
  await expect(
    page.getByRole("link", { name: "Пользователи и роли", exact: true }),
  ).toHaveCount(0);
  await section(page, "Обращения");
  await expect(text(page, "Назначенная задача инженера")).toBeVisible();
  await page
    .getByRole("button", { name: "Открыть обращение", exact: true })
    .click();
  await fill(page, "Текст ответа", "Публичный ответ инженера");
  await page
    .getByRole("button", { name: "Отправить ответ", exact: true })
    .click();
  await expect(
    page.getByRole("textbox", { name: "Текст ответа", exact: true }),
  ).toHaveValue("");
  await expect(text(page, "Публичный ответ инженера")).toBeVisible();
  await fill(page, "Текст ответа", "PRIVATE MANAGEMENT NOTE");
  await choose(page, "Вид сообщения", "Внутренняя заметка сотрудникам");
  await page
    .getByRole("button", { name: "Отправить ответ", exact: true })
    .click();
  await expect(
    page.getByRole("textbox", { name: "Текст ответа", exact: true }),
  ).toHaveValue("");
  await expect(text(page, "PRIVATE MANAGEMENT NOTE")).toBeVisible();
  await page
    .getByRole("button", { name: "Изменить статус", exact: true })
    .click();
  await choose(page, "Новый статус", "Принято в работу");
  await fill(page, "Причина изменения", "Принято инженером");
  await save(page);
  await expect(text(page, "Принято в работу").first()).toBeVisible();
  await page
    .getByRole("button", { name: "Закрыть карточку", exact: true })
    .click();
  await section(page, "Показания");
  await expect(page.getByText(/ADMIN-METER.*0 → 123/).filter({visible:true})).toBeVisible();
  const staffToken = (
    await call("/auth/login", { loginName: "ui-engineer", password })
  ).accessToken;
  expect(
    (
      await fetch(base + "/admin/users", {
        headers: { Authorization: `Bearer ${staffToken}` },
      })
    ).status,
  ).toBe(403);
  const detail = await fetch(base + `/requests/${requestId}`, {
    headers: { Authorization: `Bearer ${tenantToken}` },
  });
  const body = await detail.text();
  expect(body).toContain("Публичный ответ инженера");
  expect(body).not.toContain("PRIVATE MANAGEMENT NOTE");
  await page.goto("/manage/users");
  await expect(page).toHaveURL(/login/);
});
test("administrator mobile navigation and direct unauthenticated routes", async ({
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await login(page, "frontend-admin", process.env.CUBE_E2E_ADMIN!);
  await page
    .getByRole("button", { name: "Меню разделов", exact: true })
    .click();
  await section(page, "Арендаторы");
  await expect(
    page.getByRole("button", { name: "Добавить", exact: true }),
  ).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth + 1,
    ),
  ).toBeTruthy();
  await page.screenshot({
    path: "test-results/admin-mobile.png",
    fullPage: true,
  });
  await page
    .getByRole("button", { name: "Меню разделов", exact: true })
    .click();
  await section(page, "Показания");
  await page
    .getByRole("button", { name: "Рассмотреть показание", exact: true })
    .click();
  await fill(page, "Комментарий решения", "Показание проверено");
  await save(page);
  await expect(text(page, "Показание проверено")).toBeVisible();
  await page.reload();
  await expect(page).toHaveURL(/login/);
});
