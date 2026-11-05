import type { Row } from "./api";
export type Choice = { value: string; label: string; hint?: string };
export type Field = {
  key: string;
  label: string;
  type?:
    | "number"
    | "date"
    | "datetime"
    | "password"
    | "text"
    | "long"
    | "select"
    | "bool"
    | "multi"
    | "file";
  required?: boolean;
  options?: Choice[];
  catalog?: string;
  default?: unknown;
  min?: number;
  nullable?: boolean;
  hint?: string;
};
export type Schema = { title: string; path: string; fields: Field[] };
const opts = (items: Record<string, string>): Choice[] =>
  Object.entries(items).map(([value, label]) => ({ value, label }));
export const statuses = {
  New: "Новое",
  Accepted: "Принято в работу",
  InProgress: "В процессе",
  Clarification: "Требуется уточнение",
  Completed: "Выполнено",
  Closed: "Закрыто",
  Rejected: "Отклонено",
};
export const premiseStatuses = {
  Available: "Свободно",
  Occupied: "Занято",
  Reserved: "Забронировано",
  Unavailable: "Недоступно",
};
export const newsCategories = {
  Emergency: "Авария",
  PlannedWork: "Плановые работы",
  Electricity: "Электричество",
  Water: "Вода",
  Heating: "Отопление",
  Construction: "Строительство",
  Accounting: "Бухгалтерия",
  Rent: "Аренда",
  General: "Общее объявление",
};
const tenant: Field = {
  key: "tenantId",
  label: "Арендатор",
  type: "select",
  catalog: "tenants",
  required: true,
};
const premise: Field = {
  key: "premisesId",
  label: "Помещение",
  type: "select",
  catalog: "premises",
  required: true,
};
const date = (key: string, label: string): Field => ({
  key,
  label,
  type: "date",
  required: true,
  default: new Date().toISOString().slice(0, 10),
});
const text = (key: string, label: string, required = true): Field => ({
  key,
  label,
  required,
});
export const schemas: Record<string, Schema> = {
  tenants: {
    title: "Арендатор",
    path: "/admin/tenants",
    fields: [
      text("name", "Название организации"),
      text("inn", "ИНН"),
      text("contactName", "Контактное лицо"),
      text("email", "Email"),
      text("phone", "Телефон"),
    ],
  },
  premises: {
    title: "Помещение",
    path: "/admin/premises",
    fields: [
      text("name", "Название помещения"),
      {
        key: "type",
        label: "Тип помещения",
        type: "select",
        options: opts({
          Office: "Офис",
          Warehouse: "Склад",
          Retail: "Торговая площадь",
        }),
        default: "Office",
        required: true,
      },
      text("address", "Адрес"),
      text("building", "Корпус"),
      text("section", "Секция", false),
      { key: "floor", label: "Этаж", type: "number", nullable: true },
      {
        key: "area",
        label: "Площадь, м²",
        type: "number",
        min: 0.01,
        required: true,
      },
      {
        key: "monthlyRent",
        label: "Аренда в месяц, ₽",
        type: "number",
        min: 0,
        required: true,
      },
      { key: "description", label: "Описание", type: "long", required: true },
      text("utilities", "Коммунальные условия", false),
      {
        key: "status",
        label: "Статус помещения",
        type: "select",
        options: opts(premiseStatuses),
        default: "Available",
        required: true,
      },
    ],
  },
  leases: {
    title: "Привязать помещение — договор",
    path: "/admin/leases",
    fields: [
      tenant,
      premise,
      text("number", "Номер договора"),
      date("signedOn", "Дата заключения"),
      date("startsOn", "Начало аренды"),
      date("endsOn", "Окончание аренды"),
    ],
  },
  documents: {
    title: "Загрузить документ",
    path: "/admin/documents",
    fields: [
      tenant,
      {
        key: "leaseId",
        label: "Договор",
        type: "select",
        catalog: "leases",
        nullable: true,
      },
      { ...premise, required: false, nullable: true },
      text("title", "Название документа"),
      {
        key: "kind",
        label: "Вид документа",
        type: "select",
        options: opts({
          Contract: "Договор",
          Addendum: "Дополнительное соглашение",
          Statement: "Заявление",
          Act: "Акт",
          Invoice: "Счёт-фактура",
          UPD: "УПД",
          Other: "Прочее",
          Template: "Шаблон",
        }),
        default: "Contract",
        required: true,
      },
      text("number", "Номер документа", false),
      date("documentDate", "Дата документа"),
      { key: "fileId", label: "Файл документа", type: "file", required: true },
    ],
  },
  invoices: {
    title: "Выставить счёт",
    path: "/admin/invoices",
    fields: [
      tenant,
      text("number", "Номер счёта"),
      {
        key: "kind",
        label: "Вид начисления",
        type: "select",
        default: "Rent",
        required: true,
        options: opts({
          Rent: "Аренда",
          Utilities: "Коммунальные платежи",
          VehicleAccess: "Проезды автотранспорта",
          Other: "Прочее",
        }),
      },
      date("issuedOn", "Дата счёта"),
      date("dueOn", "Срок оплаты"),
      text("purpose", "Назначение платежа"),
      {
        key: "amount",
        label: "Сумма, ₽",
        type: "number",
        min: 0.01,
        required: true,
      },
      {
        key: "fileId",
        label: "PDF счёта (необязательно)",
        type: "file",
        nullable: true,
      },
    ],
  },
  users: {
    title: "Новый сотрудник",
    path: "/admin/staff",
    fields: [
      text("login", "Логин сотрудника"),
      text("email", "Email сотрудника"),
      text("fullName", "ФИО сотрудника"),
      {
        key: "password",
        label: "Пароль сотрудника",
        type: "password",
        required: true,
        hint: "Не менее 12 символов: буквы обоих регистров, цифра, спецсимвол",
      },
    ],
  },
  meters: {
    title: "Добавить счётчик",
    path: "/admin/meters",
    fields: [
      tenant,
      premise,
      {
        key: "type",
        label: "Тип счётчика",
        type: "select",
        default: "Electricity",
        required: true,
        options: opts({ Electricity: "Электричество", Water: "Вода" }),
      },
      text("serialNumber", "Заводской номер"),
      { ...text("unit", "Единица измерения"), default: "кВт·ч" },
      {
        key: "baseline",
        label: "Начальное показание",
        type: "number",
        default: 0,
        min: 0,
        required: true,
      },
    ],
  },
  listings: {
    title: "Объявление",
    path: "/admin/listings",
    fields: [
      premise,
      text("title", "Заголовок объявления"),
      {
        key: "description",
        label: "Описание объявления",
        type: "long",
        required: true,
      },
      { key: "published", label: "Опубликовано", type: "bool", default: false },
    ],
  },
  news: {
    title: "Новость или оповещение",
    path: "/admin/news",
    fields: [
      text("title", "Заголовок новости"),
      {
        key: "category",
        label: "Категория новости",
        type: "select",
        default: "General",
        required: true,
        options: opts(newsCategories),
      },
      {
        key: "importance",
        label: "Важность",
        type: "select",
        default: "Normal",
        required: true,
        options: opts({
          Normal: "Обычная",
          Important: "Важная",
          Urgent: "Срочная",
        }),
      },
      text("summary", "Краткое описание"),
      { key: "body", label: "Полный текст", type: "long", required: true },
      {
        key: "allTenants",
        label: "Всем арендаторам",
        type: "bool",
        default: true,
      },
      {
        key: "tenantIds",
        label: "Выбранные арендаторы",
        type: "multi",
        catalog: "tenants",
        default: [],
      },
      {
        key: "premisesIds",
        label: "Выбранные помещения",
        type: "multi",
        catalog: "premises",
        default: [],
      },
      { ...text("building", "Корпус получателей", false), nullable: true },
      { ...text("section", "Секция получателей", false), nullable: true },
      {
        key: "floor",
        label: "Этаж получателей",
        type: "number",
        nullable: true,
      },
      {
        key: "publishAt",
        label: "Дата публикации",
        type: "datetime",
        nullable: true,
        hint: "Пустое поле — черновик. Для немедленной публикации нажмите «Сейчас».",
      },
      {
        key: "expiresAt",
        label: "Срок актуальности",
        type: "datetime",
        nullable: true,
        hint: "Пустое поле — бессрочно.",
      },
      {
        key: "requiresAcknowledgement",
        label: "Требовать подтверждение «Ознакомлен»",
        type: "bool",
        default: false,
      },
    ],
  },
};
export const actions = {
  approve: (id: string): Schema => ({
    title: "Подтвердить регистрацию",
    path: `/admin/registration-applications/${id}/approve`,
    fields: [
      {
        key: "existingTenantId",
        label: "Привязать к существующему арендатору",
        type: "select",
        catalog: "tenants",
        nullable: true,
        hint: "Выберите организацию с тем же ИНН. Пустое поле создаёт новую организацию.",
      },
    ],
  }),
  assign: (id: string): Schema => ({
    title: "Ответственный и срок",
    path: `/admin/requests/${id}/assignment`,
    fields: [
      {
        key: "assigneeId",
        label: "Ответственный сотрудник",
        type: "select",
        catalog: "staff",
        nullable: true,
      },
      {
        key: "dueAt",
        label: "Срок выполнения",
        type: "datetime",
        nullable: true,
      },
    ],
  }),
  transition: (id: string, options: Choice[]): Schema => ({
    title: "Изменить статус обращения",
    path: `/requests/${id}/status`,
    fields: [
      {
        key: "status",
        label: "Новый статус",
        type: "select",
        required: true,
        options,
      },
      {
        key: "reason",
        label: "Причина изменения",
        type: "long",
        required: true,
      },
    ],
  }),
  review: (path: string): Schema => ({
    title: "Рассмотреть запись",
    path,
    fields: [
      {
        key: "status",
        label: "Решение",
        type: "select",
        default: "Accepted",
        required: true,
        options: opts({ Accepted: "Принять", Rejected: "Отклонить" }),
      },
      {
        key: "reason",
        label: "Комментарий решения",
        type: "long",
        required: true,
      },
    ],
  }),
  correction: (id: string): Schema => ({
    title: "Коррекция базового показания",
    path: `/admin/meters/${id}/baseline`,
    fields: [
      {
        key: "value",
        label: "Новое базовое показание",
        type: "number",
        min: 0,
        required: true,
      },
      {
        key: "reason",
        label: "Обоснование коррекции",
        type: "long",
        required: true,
      },
    ],
  }),
  routing: (id: string): Schema => ({
    title: "Маршрутизация категории",
    path: `/admin/request-categories/${id}/routing`,
    fields: [
      {
        key: "responsibleUserId",
        label: "Ответственный по категории",
        type: "select",
        catalog: "staff",
        nullable: true,
      },
    ],
  }),
};
export function transitions(current: string): Choice[] {
  const next: Record<string, string[]> = {
    New: ["Accepted", "Rejected"],
    Accepted: ["InProgress", "Rejected"],
    InProgress: ["Clarification", "Completed", "Rejected"],
    Clarification: ["InProgress", "Rejected"],
    Completed: ["Closed", "InProgress"],
  };
  return (next[current] ?? []).map((value) => ({
    value,
    label: statuses[value as keyof typeof statuses],
  }));
}
export function initialValues(
  schema: Schema,
  row?: Row,
): Record<string, unknown> {
  return Object.fromEntries(
    schema.fields.map((f) => [
      f.key,
      row?.[f.key] ??
        f.default ??
        (f.type === "bool" ? false : f.type === "multi" ? [] : ""),
    ]),
  );
}
export function payload(
  schema: Schema,
  values: Record<string, unknown>,
): Record<string, unknown> {
  const data: Record<string, unknown> = {};
  for (const f of schema.fields) {
    let v = values[f.key];
    const empty = v == null || v === "";
    if (f.required && empty) throw new Error(`Заполните поле «${f.label}».`);
    if (f.type === "number") {
      v = empty && f.nullable ? null : Number(String(v).replace(",", "."));
      if (
        v !== null &&
        (!Number.isFinite(v) || (f.min !== undefined && Number(v) < f.min))
      )
        throw new Error(`Проверьте поле «${f.label}».`);
      if (
        (f.key === "floor" || f.key === "amount") &&
        v !== null &&
        (f.key === "floor"
          ? !Number.isInteger(v)
          : Math.abs(Number(v) * 100 - Math.round(Number(v) * 100)) > 0.000001)
      )
        throw new Error(`Проверьте точность поля «${f.label}».`);
    } else if (f.type === "date") {
      if (
        !/^\d{4}-\d{2}-\d{2}$/.test(String(v)) ||
        Number.isNaN(Date.parse(String(v))) ||
        new Date(String(v)).toISOString().slice(0, 10) !== v
      )
        throw new Error(`Дата «${f.label}»: используйте ГГГГ-ММ-ДД.`);
    } else if (f.type === "datetime") {
      if (empty) v = null;
      else {
        if (Number.isNaN(Date.parse(String(v))))
          throw new Error(`Проверьте дату «${f.label}».`);
        v = new Date(String(v)).toISOString();
      }
    } else if (f.type === "file" && empty && f.nullable) v = null;
    else if (f.type === "bool") v = !!v;
    else if (f.type === "multi") v = Array.isArray(v) ? v : [];
    else if (f.type !== "file") {
      v = empty && f.nullable ? null : String(v ?? "").trim();
      if (f.required && !v) throw new Error(`Заполните поле «${f.label}».`);
    }
    if (
      f.type === "select" &&
      v &&
      f.options &&
      !f.options.some((o) => o.value === v)
    )
      throw new Error(`Выберите значение «${f.label}».`);
    data[f.key] = v;
  }
  if (data.email && !/^\S+@\S+\.\S+$/.test(String(data.email)))
    throw new Error("Введите корректный email.");
  const password = data.password ?? data.newPassword;
  if (
    password &&
    (String(password).length < 12 ||
      !/[A-Z]/.test(String(password)) ||
      !/[a-z]/.test(String(password)) ||
      !/[0-9]/.test(String(password)) ||
      !/[^a-zA-Z0-9]/.test(String(password)))
  )
    throw new Error(
      "Пароль: от 12 символов, буквы обоих регистров, цифра и спецсимвол.",
    );
  if (
    data.endsOn &&
    (String(data.endsOn) < String(data.startsOn) ||
      String(data.endsOn) < String(data.signedOn))
  )
    throw new Error(
      "Окончание аренды должно быть после начала и даты договора.",
    );
  if (data.dueOn && String(data.dueOn) < String(data.issuedOn))
    throw new Error("Срок оплаты не может быть раньше даты счёта.");
  if (
    data.expiresAt &&
    (!data.publishAt || String(data.expiresAt) <= String(data.publishAt))
  )
    throw new Error("Срок актуальности должен быть позже публикации.");
  if (
    schema.path.includes("/news") &&
    !data.allTenants &&
    !(data.tenantIds as unknown[])?.length &&
    !(data.premisesIds as unknown[])?.length &&
    !data.building &&
    !data.section &&
    data.floor == null
  )
    throw new Error("Выберите получателей новости.");
  return data;
}
