import { useEffect, useState, type ReactNode } from "react";
import {
  View,
  ScrollView,
  Pressable,
  useWindowDimensions,
  Modal,
} from "react-native";
import { Redirect, useLocalSearchParams, useRouter } from "expo-router";
import {
  useMutation,
  useQueries,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import { useAuth } from "@/auth/AuthProvider";
import { AppText, Badge, Button, Card, Icon } from "@/ui/primitives";
import { TextField, SelectField } from "@/ui/form";
import { Async, EmptyState, useToast } from "@/ui/feedback";
import { colors, space } from "@/theme";
import { fmtDate, fmtDateTime, fmtMoney } from "@/lib/format";
import { downloadFile, pickDocuments } from "@/lib/files";
import { PrivateImage } from "@/ui/PrivateImage";
import {
  management,
  canOpen,
  roleHome,
  str,
  sectionPaths,
  staffSections,
  type Row,
} from "./api";
import { Editor, type EditorConfig, type Catalog } from "./Editor";
import {
  actions,
  schemas,
  statuses,
  premiseStatuses,
  newsCategories,
  transitions,
  type Schema,
} from "./forms";

const sections: [string, string, string][] = [
  ["overview", "Обзор", "grid-outline"],
  ["applications", "Заявки на регистрацию", "person-add-outline"],
  ["tenants", "Арендаторы", "people-outline"],
  ["premises", "Помещения", "business-outline"],
  ["leases", "Договоры аренды", "document-text-outline"],
  ["documents", "Документы", "folder-open-outline"],
  ["invoices", "Счета", "receipt-outline"],
  ["requests", "Обращения", "chatbubbles-outline"],
  ["meters", "Счётчики", "speedometer-outline"],
  ["readings", "Показания", "analytics-outline"],
  ["listings", "Объявления", "storefront-outline"],
  ["news", "Новости и оповещения", "megaphone-outline"],
  ["users", "Пользователи и роли", "key-outline"],
  ["routing", "Маршрутизация", "git-branch-outline"],
  ["audit", "Журнал действий", "time-outline"],
  ["handoffs", "Интеграции", "cloud-outline"],
  ["profile", "Мой профиль", "person-circle-outline"],
];
const translations: Record<string, string> = {
  ...statuses,
  ...premiseStatuses,
  Paid: "Оплачен",
  Unpaid: "Не оплачен",
  PartiallyPaid: "Частично оплачен",
  Overdue: "Просрочен",
  Admin: "Администратор",
  Staff: "Сотрудник",
  Tenant: "Арендатор",
  Active: "Действует",
  Expired: "Истёк",
  Terminated: "Расторгнут",
  Submitted: "Передано",
  Correction: "Коррекция",
  UnderReview: "На рассмотрении",
  Published: "Опубликовано",
  Contract: "Договор",
  Addendum: "Доп. соглашение",
  Statement: "Заявление",
  Act: "Акт",
  Invoice: "Счёт-фактура",
  UPD: "УПД",
  Template: "Шаблон",
  Other: "Прочее",
  Rent: "Аренда",
  Utilities: "Коммунальные платежи",
  VehicleAccess: "Проезды автотранспорта",
  Electricity: "Электричество",
  Water: "Вода",
  NotConfigured: "Не подключено",
  AwaitingConfiguration: "Ожидает подключения",
  Office: "Офис",
  Warehouse: "Склад",
  Retail: "Торговая площадь",
  Normal: "Обычная",
  Important: "Важная",
  Urgent: "Срочная",
  ApproveRegistration: "Подтверждение регистрации",
  BlockUser: "Блокировка пользователя",
  UnblockUser: "Разблокировка пользователя",
  CreateStaff: "Создание сотрудника",
};
const label = (v: unknown) => translations[String(v)] ?? String(v ?? "—");
const tone = (status: string) =>
  ["Rejected", "Unavailable", "Terminated"].includes(status)
    ? ("danger" as const)
    : ["Completed", "Closed", "Accepted", "Active", "Available"].includes(
          status,
        )
      ? ("success" as const)
      : ("info" as const);
function RowText({ label: caption, value }: { label: string; value: unknown }) {
  return (
    <View style={{ gap: 3, minWidth: 130, flexGrow: 1 }}>
      <AppText variant="caption">{caption}</AppText>
      <AppText>{value == null || value === "" ? "—" : String(value)}</AppText>
    </View>
  );
}
function Group({ children }: { children: ReactNode }) {
  return (
    <View
      style={{
        flexDirection: "row",
        flexWrap: "wrap",
        gap: 16,
        alignItems: "flex-start",
      }}
    >
      {children}
    </View>
  );
}
function State({ value }: { value: string }) {
  return <Badge label={label(value)} tone={tone(value)} />;
}

export default function ManagementRoute() {
  const { user, isAuthed } = useAuth();
  const { section = "overview" } = useLocalSearchParams<{ section: string }>();
  if (!isAuthed) return <Redirect href="/login" />;
  if (!canOpen(user?.role, section))
    return <Redirect href={roleHome(user?.role) as never} />;
  return <Workspace key={section} section={section} />;
}
function Workspace({ section }: { section: string }) {
  const { user, signOut } = useAuth();
  const isAdmin = user?.role === "Admin";
  const router = useRouter();
  const { width } = useWindowDimensions();
  const wide = width >= 1000;
  const toast = useToast();
  const qc = useQueryClient();
  const [menu, setMenu] = useState(false);
  const [search, setSearch] = useState("");
  const [tenant, setTenant] = useState("");
  const [status, setStatus] = useState("");
  const [premise, setPremise] = useState("");
  const [assignee, setAssignee] = useState("");
  const [category, setCategory] = useState("");
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<Row>();
  const [editor, setEditor] = useState<EditorConfig>();
  const [confirm, setConfirm] = useState<{
    title: string;
    action: () => Promise<unknown>;
  }>();
  const catalogKeys = [
    "tenants",
    "premises",
    "users",
    "leases",
    "meters",
    "routing",
    "listings",
  ];
  const catalogQueries = useQueries({
    queries: catalogKeys.map((key) => ({
      queryKey: ["management", "catalog", key],
      queryFn: () => management.list(sectionPaths[key]),
      enabled: isAdmin,
    })),
  });
  const staffCats = useQuery({
    queryKey: ["management", "categories"],
    queryFn: () => management.list("/request-categories"),
    enabled: !isAdmin,
  });
  const catalog: Catalog = Object.fromEntries(
    catalogKeys.map((key, i) => [key, catalogQueries[i].data ?? []]),
  );
  if (!isAdmin) catalog.routing = staffCats.data ?? [];
  const name = (key: string, id: unknown) => {
    const r = catalog[key]?.find((r) => r.id === id);
    return r
      ? str(r, "name") ||
          str(r, "fullName") ||
          str(r, "userName") ||
          str(r, "serialNumber") ||
          str(r, "number")
      : String(id ?? "—").slice(0, 8);
  };
  const staffMeters = useQuery({
    queryKey: ["management", "staff-meter-catalog"],
    queryFn: () => management.list("/staff/meters"),
    enabled: !isAdmin,
    refetchInterval: 30000,
  });
  if (!isAdmin) catalog.meters = staffMeters.data ?? [];
  const path =
    !isAdmin && ["meters", "readings"].includes(section)
      ? `/staff/${section}`
      : sectionPaths[section];
  const query = useQuery({
    queryKey: ["management", "list", section, isAdmin],
    queryFn: () => management.list(path),
    enabled: !!path,
    refetchInterval: ["requests", "readings", "applications"].includes(section)
      ? 30000
      : false,
  });
  const overviewRequests = useQuery({
    queryKey: ["management", "overview-requests"],
    queryFn: () => management.list("/requests"),
    enabled: section === "overview",
    refetchInterval: 30000,
  });
  const overviewApps = useQuery({
    queryKey: ["management", "overview-apps"],
    queryFn: () => management.list("/admin/registration-applications"),
    enabled: section === "overview" && isAdmin,
    refetchInterval: 30000,
  });
  const refresh = () => {
    void qc.invalidateQueries({ queryKey: ["management"] });
    void qc.invalidateQueries({ queryKey: ["requests"] });
  };
  const action = useMutation({
    mutationFn: async (fn: () => Promise<unknown>) => fn(),
    onSuccess: () => {
      refresh();
      toast("Изменения сохранены");
      setConfirm(undefined);
    },
    onError: (e: Error) => toast(e.message, "error"),
  });
  const run = (fn: () => Promise<unknown>) => action.mutate(fn);
  const open = (
    schema: Schema,
    row?: Row,
    method?: string,
    extra?: Record<string, unknown>,
  ) => setEditor({ schema, row, method, extra });
  const edit = (row: Row) =>
    open(
      { ...schemas[section], path: `${schemas[section].path}/${row.id}` },
      row,
      "PUT",
    );
  const go = (key: string) => {
    setMenu(false);
    router.replace(`/manage/${key}` as never);
  };
  const title = sections.find((s) => s[0] === section)?.[1] ?? "Управление";
  const blocked = (resource: string, row: Row) =>
    setConfirm({
      title: `${row.blocked ? "Восстановить доступ" : "Заблокировать доступ"}: ${str(row, "name") || str(row, "userName")}?`,
      action: () =>
        management.save(
          `/admin/${resource}/${row.id}/blocked`,
          { blocked: !row.blocked },
          "PUT",
        ),
    });
  const assets = (row: Row) =>
    setSelected({ ...row, assetMode: true, assetKind: section });
  const renderActions = (row: Row) => (
    <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 8 }}>
      {["tenants", "premises", "listings", "news"].includes(section) &&
      isAdmin &&
      !row.archived ? (
        <Button
          title="Редактировать"
          small
          variant="secondary"
          onPress={() => edit(row)}
        />
      ) : null}
      {["tenants", "users"].includes(section) && row.id !== user?.id ? (
        <Button
          title={row.blocked ? "Разблокировать" : "Заблокировать"}
          small
          variant={row.blocked ? "secondary" : "danger"}
          onPress={() => blocked(section, row)}
        />
      ) : null}
      {section === "premises" ? (
        <Button
          title="Фотографии помещения"
          small
          variant="secondary"
          onPress={() =>
            run(async () => {
              let listing = catalog.listings.find(
                (l) => l.premisesId === row.id && !l.archived,
              );
              if (!listing)
                listing = await management.save("/admin/listings", {
                  premisesId: row.id,
                  title: row.name,
                  description: row.description,
                  published: false,
                });
              setSelected({
                ...listing,
                assetMode: true,
                assetKind: "listings",
              });
            })
          }
        />
      ) : null}
      {section === "listings" && !row.archived ? (
        <Button
          title="Цена и статус помещения"
          small
          variant="secondary"
          onPress={() => {
            const p = catalog.premises.find((p) => p.id === row.premisesId);
            if (p)
              open(
                { ...schemas.premises, path: `/admin/premises/${p.id}` },
                p,
                "PUT",
              );
          }}
        />
      ) : null}
      {section === "applications" ? (
        <>
          <Button
            title="Подтвердить доступ"
            small
            onPress={() => open(actions.approve(row.id))}
          />
          <Button
            title={row.blocked ? "Снять блокировку" : "Отклонить заявку"}
            small
            variant="danger"
            onPress={() => blocked("users", { ...row, blocked: !!row.blocked })}
          />
        </>
      ) : null}
      {["tenants", "requests", "news", "meters"].includes(section) ? (
        <Button
          title={section === "news" ? "Прочтения" : "Подробнее"}
          small
          variant="secondary"
          onPress={() => setSelected(row)}
        />
      ) : null}
      {section === "leases" && row.status === "Active" ? (
        <>
          <Button
            title="Завершить договор"
            small
            variant="danger"
            onPress={() =>
              setConfirm({
                title: "Завершить договор и освободить помещение?",
                action: () =>
                  management.save(
                    `/admin/leases/${row.id}/status`,
                    { status: "Expired" },
                    "PUT",
                  ),
              })
            }
          />
          <Button
            title="Расторгнуть"
            small
            variant="danger"
            onPress={() =>
              setConfirm({
                title: "Расторгнуть договор и освободить помещение?",
                action: () =>
                  management.save(
                    `/admin/leases/${row.id}/status`,
                    { status: "Terminated" },
                    "PUT",
                  ),
              })
            }
          />
        </>
      ) : null}
      {section === "documents" ? (
        <>
          <Button
            title="Скачать документ"
            small
            variant="secondary"
            onPress={() =>
              run(() => downloadFile(`/api/v1/documents/${row.id}/file`))
            }
          />
          <Button
            title={
              row.archived ? "Восстановить документ" : "Архивировать документ"
            }
            small
            variant="secondary"
            onPress={() =>
              setConfirm({
                title: row.archived
                  ? "Восстановить доступ к документу?"
                  : "Архивировать документ? Арендатор перестанет его видеть.",
                action: () =>
                  management.save(
                    `/admin/documents/${row.id}/archive`,
                    { archived: !row.archived },
                    "PUT",
                  ),
              })
            }
          />
          {row.kind === "Statement" ? (
            <Button
              title="Рассмотреть заявление"
              small
              onPress={() =>
                open(
                  actions.review(`/admin/statements/${row.id}/review`),
                  undefined,
                  "PUT",
                )
              }
            />
          ) : null}
        </>
      ) : null}
      {section === "invoices" && row.fileId ? (
        <Button
          title="Скачать PDF"
          small
          variant="secondary"
          onPress={() =>
            run(() => downloadFile(`/api/v1/invoices/${row.id}/file`))
          }
        />
      ) : null}
      {section === "meters" && isAdmin ? (
        <Button
          title="Скорректировать базу"
          small
          variant="secondary"
          onPress={() =>
            open(actions.correction(row.id), undefined, "PUT", {
              version: row.version,
            })
          }
        />
      ) : null}
      {section === "readings" ? (
        <>
          {isAdmin && row.photoId ? (
            <Button
              title="Фото счётчика"
              small
              variant="secondary"
              onPress={() =>
                run(() => downloadFile(`/api/v1/files/${row.photoId}`))
              }
            />
          ) : null}
          {isAdmin && row.status === "Submitted" ? (
            <Button
              title="Рассмотреть показание"
              small
              onPress={() =>
                open(
                  actions.review(`/admin/readings/${row.id}/review`),
                  undefined,
                  "PUT",
                )
              }
            />
          ) : null}
        </>
      ) : null}
      {section === "routing" ? (
        <Button
          title="Назначить сотрудника"
          small
          variant="secondary"
          onPress={() => open(actions.routing(row.id), row, "PUT")}
        />
      ) : null}
      {["listings", "news"].includes(section) && !row.archived ? (
        <>
          <Button
            title={section === "listings" ? "Фотографии" : "Вложения"}
            small
            variant="secondary"
            onPress={() => assets(row)}
          />
          <Button
            title="В архив"
            small
            variant="danger"
            onPress={() =>
              setConfirm({
                title:
                  "Архивировать публикацию? Она будет скрыта от арендаторов.",
                action: () => management.remove(`/admin/${section}/${row.id}`),
              })
            }
          />
        </>
      ) : null}
    </View>
  );
  function summary(row: Row) {
    const primary =
      str(row, "name") ||
      str(row, "subject") ||
      str(row, "title") ||
      str(row, "fullName") ||
      str(row, "serialNumber") ||
      str(row, "number") ||
      (section === "readings"
        ? `Показание ${row.value}`
        : section === "audit"
          ? label(row.action)
          : row.id.slice(0, 8));
    return (
      <Card key={row.id} style={{ gap: 14 }}>
        <Group>
          <View style={{ flex: 1, minWidth: 180, gap: 4 }}>
            <AppText variant="h3">{primary}</AppText>
            <AppText variant="caption">
              {section === "applications"
                ? `${str(row, "requestedOrganization")} · ИНН ${str(row, "requestedInn")}`
                : section === "routing"
                  ? str(row, "department")
                  : section === "users"
                    ? str(row, "userName")
                    : section === "premises"
                      ? `${str(row, "address")} · ${str(row, "building")}`
                      : str(row, "createdAt")
                        ? fmtDateTime(str(row, "createdAt"))
                        : ""}
            </AppText>
          </View>
          {row.status ? <State value={str(row, "status")} /> : null}
          {row.blocked !== undefined ? (
            <Badge
              label={row.blocked ? "Доступ заблокирован" : "Доступ открыт"}
              tone={row.blocked ? "danger" : "success"}
            />
          ) : null}
          {row.role ? <Badge label={label(row.role)} /> : null}
          {row.archived ? <Badge label="В архиве" /> : null}
          {section === "news" ? (
            <Badge
              label={
                row.archived
                  ? "В архиве"
                  : !row.publishAt
                    ? "Черновик"
                    : Date.parse(str(row, "publishAt")) > Date.now()
                      ? "Запланировано"
                      : row.expiresAt &&
                          Date.parse(str(row, "expiresAt")) < Date.now()
                        ? "Срок истёк"
                        : "Опубликовано"
              }
              tone={row.importance === "Urgent" ? "danger" : "info"}
            />
          ) : null}
          {section === "listings" ? (
            <Badge
              label={
                row.archived
                  ? "В архиве"
                  : row.published
                    ? "Опубликовано"
                    : "Скрыто"
              }
            />
          ) : null}
        </Group>
        <Group>
          {row.tenantId ? (
            <RowText
              label="Арендатор"
              value={
                row.tenantName ??
                (isAdmin
                  ? name("tenants", row.tenantId)
                  : String(row.tenantId).slice(0, 8))
              }
            />
          ) : null}
          {row.premisesId ? (
            <RowText
              label="Помещение"
              value={row.premisesName ?? name("premises", row.premisesId)}
            />
          ) : null}
          {section === "tenants" ? (
            <>
              <RowText label="ИНН" value={row.inn} />
              <RowText label="Контакт" value={row.contactName} />
              <RowText
                label="Email / телефон"
                value={`${row.email} · ${row.phone}`}
              />
            </>
          ) : null}
          {section === "premises" ? (
            <>
              <RowText label="Тип" value={label(row.type)} />
              <RowText label="Площадь" value={`${row.area} м²`} />
              <RowText
                label="Аренда в месяц"
                value={fmtMoney(Number(row.monthlyRent))}
              />
              <RowText label="Этаж" value={row.floor} />
            </>
          ) : null}
          {section === "leases" ? (
            <>
              <RowText label="Договор" value={row.number} />
              <RowText label="Заключён" value={fmtDate(str(row, "signedOn"))} />
              <RowText
                label="Период аренды"
                value={`${fmtDate(str(row, "startsOn"))} — ${fmtDate(str(row, "endsOn"))}`}
              />
            </>
          ) : null}
          {section === "documents" ? (
            <>
              <RowText label="Вид" value={label(row.kind)} />
              <RowText
                label="Номер и дата"
                value={`${row.number || "Без номера"} · ${fmtDate(str(row, "documentDate"))}`}
              />
              {row.leaseId ? (
                <RowText label="Договор" value={name("leases", row.leaseId)} />
              ) : null}
            </>
          ) : null}
          {section === "invoices" ? (
            <>
              <RowText label="Назначение" value={row.purpose} />
              <RowText label="Сумма" value={fmtMoney(Number(row.amount))} />
              <RowText label="Оплачено" value={fmtMoney(Number(row.paid))} />
              <RowText
                label="Остаток"
                value={fmtMoney(Number(row.amount) - Number(row.paid))}
              />
              <RowText label="Срок оплаты" value={fmtDate(str(row, "dueOn"))} />
            </>
          ) : null}
          {section === "requests" ? (
            <>
              <RowText
                label="Категория"
                value={name("routing", row.categoryId)}
              />
              <RowText
                label="Ответственный"
                value={
                  row.assigneeId
                    ? isAdmin
                      ? name("users", row.assigneeId)
                      : user?.fullName
                    : "Не назначен"
                }
              />
              <RowText
                label="Срок выполнения"
                value={
                  row.dueAt ? fmtDateTime(str(row, "dueAt")) : "Не установлен"
                }
              />
              <RowText label="Номер обращения" value={row.id.slice(0, 8)} />
            </>
          ) : null}
          {section === "meters" ? (
            <>
              <RowText label="Тип" value={label(row.type)} />
              <RowText
                label="Текущее значение"
                value={`${row.lastValue} ${row.unit}`}
              />
              <RowText
                label="Состояние"
                value={row.active ? "Активен" : "Отключён"}
              />
            </>
          ) : null}
          {section === "readings" ? (
            <>
              <RowText label="Счётчик" value={name("meters", row.meterId)} />
              <RowText
                label="Предыдущее → новое"
                value={`${row.previousValue} → ${row.value}`}
              />
              <RowText label="Комментарий проверки" value={row.reviewReason} />
            </>
          ) : null}
          {section === "listings" ? (
            <RowText label="Описание" value={row.description} />
          ) : null}
          {section === "news" ? (
            <>
              <RowText
                label="Категория"
                value={
                  newsCategories[
                    str(row, "category") as keyof typeof newsCategories
                  ]
                }
              />
              <RowText label="Важность" value={label(row.importance)} />
              <RowText
                label="Публикация"
                value={
                  row.publishAt
                    ? fmtDateTime(str(row, "publishAt"))
                    : "Черновик"
                }
              />
              <RowText
                label="Подтверждение"
                value={
                  row.requiresAcknowledgement ? "Требуется" : "Не требуется"
                }
              />
            </>
          ) : null}
          {section === "premises" ? (
            <Button
              title="Фотографии помещения"
              small
              variant="secondary"
              onPress={() =>
                run(async () => {
                  let listing = catalog.listings.find(
                    (l) => l.premisesId === row.id && !l.archived,
                  );
                  if (!listing)
                    listing = await management.save("/admin/listings", {
                      premisesId: row.id,
                      title: row.name,
                      description: row.description,
                      published: false,
                    });
                  setSelected({
                    ...listing,
                    assetMode: true,
                    assetKind: "listings",
                  });
                })
              }
            />
          ) : null}
          {section === "listings" && !row.archived ? (
            <Button
              title="Цена и статус помещения"
              small
              variant="secondary"
              onPress={() => {
                const p = catalog.premises.find((p) => p.id === row.premisesId);
                if (p)
                  open(
                    { ...schemas.premises, path: `/admin/premises/${p.id}` },
                    p,
                    "PUT",
                  );
              }}
            />
          ) : null}
          {section === "applications" ? (
            <>
              <RowText label="Логин" value={row.userName} />
              <RowText label="Email" value={row.email} />
              <RowText label="Телефон" value={row.phoneNumber} />
            </>
          ) : null}
          {section === "routing" ? (
            <RowText
              label="Ответственный"
              value={
                row.responsibleUserId
                  ? name("users", row.responsibleUserId)
                  : "Не назначен"
              }
            />
          ) : null}
          {section === "audit" ? (
            <>
              <RowText
                label="Пользователь"
                value={name("users", row.actorId)}
              />
              <RowText label="Запись" value={row.resourceId} />
            </>
          ) : null}
          {section === "handoffs" ? (
            <>
              <RowText label="Система" value={row.provider} />
              <RowText label="Событие" value={row.kind} />
              <RowText label="Попытки" value={row.attempts} />
              <RowText label="Последняя ошибка" value={row.lastError} />
            </>
          ) : null}
        </Group>
        {renderActions(row)}
      </Card>
    );
  }
  const rows = (query.data ?? [])
    .map((row) =>
      section === "invoices"
        ? {
            ...row,
            status:
              Number(row.paid) >= Number(row.amount)
                ? "Paid"
                : str(row, "dueOn") < new Date().toISOString().slice(0, 10)
                  ? "Overdue"
                  : Number(row.paid) > 0
                    ? "PartiallyPaid"
                    : "Unpaid",
          }
        : row,
    )
    .filter((row) => {
      const matches =
        !search ||
        [
          ...Object.values(row),
          name("tenants", row.tenantId),
          name("premises", row.premisesId),
        ]
          .join(" ")
          .toLowerCase()
          .includes(search.toLowerCase());
      return (
        matches &&
        (!tenant || row.tenantId === tenant) &&
        (!status ||
          row.status === status ||
          (status === "Archived" && row.archived)) &&
        (!premise || row.premisesId === premise) &&
        (!assignee || row.assigneeId === assignee) &&
        (!category || row.categoryId === category)
      );
    });
  useEffect(
    () => setPage(1),
    [search, tenant, status, premise, assignee, category],
  );
  const nav = (
    <View style={{ gap: 4, padding: 12 }}>
      {sections
        .filter((s) => isAdmin || staffSections.includes(s[0]))
        .map(([key, title, icon]) => (
          <Pressable
            key={key}
            accessibilityRole="link"
            accessibilityLabel={title}
            accessibilityState={{ selected: key === section }}
            onPress={() => go(key)}
            style={{
              padding: 12,
              borderRadius: 10,
              flexDirection: "row",
              alignItems: "center",
              gap: 12,
              backgroundColor:
                key === section ? colors.accentSoft : "transparent",
            }}
          >
            <Icon name={icon as never} size={20} />
            <AppText
              color={key === section ? colors.accentDark : colors.heading}
              style={{
                fontWeight: key === section ? "700" : "400",
                flexShrink: 1,
              }}
            >
              {title}
            </AppText>
          </Pressable>
        ))}
    </View>
  );
  return (
    <View style={{ flex: 1, backgroundColor: colors.bg }}>
      <View
        style={{
          padding: 16,
          backgroundColor: colors.heading,
          flexDirection: "row",
          alignItems: "center",
          gap: 12,
        }}
      >
        {!wide ? (
          <Button
            title="Меню разделов"
            small
            variant="secondary"
            onPress={() => setMenu(!menu)}
          />
        ) : (
          <Icon name="cube-outline" color="#FFFFFF" size={30} />
        )}
        <View style={{ flex: 1 }}>
          <AppText color="#FFFFFF" variant="h3">
            КУБ · {isAdmin ? "Панель управления" : "Рабочий кабинет"}
          </AppText>
          <AppText color="#C6DADC" variant="small">
            {user?.fullName} · {label(user?.role)}
          </AppText>
        </View>
        <Button
          title="Выйти"
          small
          variant="secondary"
          onPress={() =>
            setConfirm({
              title: "Выйти из кабинета?",
              action: async () => {
                await signOut().catch(() => {});
              },
            })
          }
        />
      </View>
      <View style={{ flex: 1, flexDirection: "row" }}>
        {wide ? (
          <ScrollView
            style={{
              width: 250,
              flexGrow: 0,
              backgroundColor: colors.card,
              borderRightWidth: 1,
              borderColor: colors.border,
            }}
          >
            {nav}
          </ScrollView>
        ) : null}
        <ScrollView
          contentContainerStyle={{
            padding: wide ? 28 : 16,
            gap: 20,
            paddingBottom: 48,
            width: "100%",
            maxWidth: 1450,
            alignSelf: "center",
          }}
          style={{ flex: 1 }}
        >
          <Group>
            <View style={{ flex: 1, minWidth: 180, gap: 4 }}>
              <AppText variant="title">{title}</AppText>
              <AppText variant="small">
                {isAdmin
                  ? "Управление недвижимостью и работой с арендаторами"
                  : "Ваши обращения и рабочие задачи"}
              </AppText>
            </View>
            {schemas[section] && isAdmin ? (
              <Button
                title={
                  section === "users"
                    ? "Создать сотрудника"
                    : section === "documents"
                      ? "Загрузить документ"
                      : section === "leases"
                        ? "Привязать помещение"
                        : "Добавить"
                }
                icon="add"
                onPress={() => open(schemas[section])}
              />
            ) : null}
            <Button
              title="Обновить"
              variant="secondary"
              small
              onPress={refresh}
            />
          </Group>
          {catalogQueries.some((q) => q.isError) ? (
            <Card>
              <AppText color={colors.danger}>
                Не удалось загрузить справочники. Нажмите «Обновить» перед
                редактированием.
              </AppText>
            </Card>
          ) : null}
          {section === "overview" ? (
            <>
              <Async query={overviewRequests}>
                {(requests) => (
                  <Group>
                    {[
                      [
                        isAdmin ? "Все обращения" : "Назначенные обращения",
                        requests.length,
                      ],
                      [
                        "В работе",
                        requests.filter(
                          (r) =>
                            !["Closed", "Rejected", "Completed"].includes(
                              str(r, "status"),
                            ),
                        ).length,
                      ],
                      [
                        "Просроченные задачи",
                        requests.filter(
                          (r) =>
                            r.dueAt &&
                            Date.parse(str(r, "dueAt")) < Date.now() &&
                            !["Closed", "Rejected", "Completed"].includes(
                              str(r, "status"),
                            ),
                        ).length,
                      ],
                      ...(isAdmin
                        ? [
                            ["Арендаторы", catalog.tenants.length],
                            [
                              "Свободные помещения",
                              catalog.premises.filter(
                                (p) => p.status === "Available",
                              ).length,
                            ],
                          ]
                        : []),
                    ].map(([caption, value]) => (
                      <Card
                        key={String(caption)}
                        style={{ flexGrow: 1, minWidth: 145, gap: 10 }}
                      >
                        <AppText variant="small">{caption}</AppText>
                        <AppText variant="title">{value}</AppText>
                      </Card>
                    ))}
                  </Group>
                )}
              </Async>
              {isAdmin ? (
                <Async query={overviewApps}>
                  {(apps) => (
                    <Card style={{ gap: 12 }}>
                      <AppText variant="h2">
                        Заявки на регистрацию: {apps.length}
                      </AppText>
                      <AppText variant="small">
                        Подтвердите данные организации и предоставьте доступ
                        арендаторам.
                      </AppText>
                      <Button
                        title="Открыть заявки"
                        variant="secondary"
                        onPress={() => go("applications")}
                      />
                    </Card>
                  )}
                </Async>
              ) : (
                <Card>
                  <AppText>
                    Вам доступны назначенные обращения. История счётчиков
                    доступна по направлениям, за которые вы назначены
                    ответственным.
                  </AppText>
                </Card>
              )}
              <Card style={{ gap: 8 }}>
                <AppText variant="h2">Рабочие разделы</AppText>
                <AppText>
                  Все действия сохраняются на сервере. Для работы с обращением
                  откройте его карточку: там находятся переписка, внутренние
                  заметки, сроки и история статусов.
                </AppText>
              </Card>
            </>
          ) : null}
          {section === "profile" ? (
            <Profile open={open} signOut={signOut} />
          ) : null}
          {section === "handoffs" ? <IntegrationStatus /> : null}
          {section === "invoices" ? (
            <Card>
              <AppText variant="small">
                Счета вводятся вручную. 1С и платёжный провайдер не подключены.
                Отметить оплату вручную через кабинет нельзя.
              </AppText>
            </Card>
          ) : null}
          {section === "users" ? (
            <Card>
              <AppText variant="small">
                Роли: администратор управляет приложением, сотрудник работает с
                назначенными обращениями, арендатор видит только свои данные.
                Права сотрудника определяются назначением обращений и
                маршрутизацией категорий. Создание дополнительных
                администраторов и смена роли существующего пользователя в этой
                версии недоступны.
              </AppText>
            </Card>
          ) : null}
          {section === "routing" ? (
            <Card>
              <AppText variant="small">
                Назначенный сотрудник получает новые обращения категории внутри
                приложения. Битрикс пока не подключён; внешняя доставка не
                выполняется. Ответственный за электричество или водоснабжение
                получает доступ к истории соответствующих счётчиков.
              </AppText>
            </Card>
          ) : null}
          {path ? (
            <>
              <Group>
                <View style={{ flexGrow: 1, minWidth: 180 }}>
                  <TextField
                    label="Поиск"
                    value={search}
                    onChangeText={setSearch}
                    placeholder="Название, номер, контакт…"
                  />
                </View>
                {isAdmin &&
                [
                  "documents",
                  "invoices",
                  "requests",
                  "leases",
                  "meters",
                  "readings",
                ].includes(section) ? (
                  <View style={{ minWidth: 180, flexGrow: 1 }}>
                    <SelectField
                      label="Фильтр по арендатору"
                      value={tenant}
                      onChange={setTenant}
                      options={[
                        { value: "", label: "Все арендаторы" },
                        ...catalog.tenants.map((r) => ({
                          value: r.id,
                          label: str(r, "name"),
                        })),
                      ]}
                    />
                  </View>
                ) : null}
                {section === "requests" ? (
                  <>
                    <View style={{ minWidth: 165, flexGrow: 1 }}>
                      <SelectField
                        label="Фильтр статуса"
                        value={status}
                        onChange={setStatus}
                        options={[
                          { value: "", label: "Все статусы" },
                          ...Object.entries(statuses).map(([value, label]) => ({
                            value,
                            label,
                          })),
                        ]}
                      />
                    </View>
                    <View style={{ minWidth: 165, flexGrow: 1 }}>
                      <SelectField
                        label="Фильтр категории"
                        value={category}
                        onChange={setCategory}
                        options={[
                          { value: "", label: "Все категории" },
                          ...catalog.routing.map((r) => ({
                            value: r.id,
                            label: str(r, "name"),
                          })),
                        ]}
                      />
                    </View>
                    {isAdmin ? (
                      <>
                        <View style={{ minWidth: 165, flexGrow: 1 }}>
                          <SelectField
                            label="Фильтр помещения"
                            value={premise}
                            onChange={setPremise}
                            options={[
                              { value: "", label: "Все помещения" },
                              ...catalog.premises.map((r) => ({
                                value: r.id,
                                label: str(r, "name"),
                              })),
                            ]}
                          />
                        </View>
                        <View style={{ minWidth: 165, flexGrow: 1 }}>
                          <SelectField
                            label="Фильтр ответственного"
                            value={assignee}
                            onChange={setAssignee}
                            options={[
                              { value: "", label: "Все сотрудники" },
                              ...catalog.users
                                .filter((r) => r.role !== "Tenant")
                                .map((r) => ({
                                  value: r.id,
                                  label: str(r, "fullName"),
                                })),
                            ]}
                          />
                        </View>
                      </>
                    ) : null}
                  </>
                ) : null}
              </Group>
              <Async query={query}>
                {() =>
                  rows.length ? (
                    <>
                      <AppText variant="small">Найдено: {rows.length}</AppText>
                      {rows.slice((page - 1) * 20, page * 20).map(summary)}
                      <Group>
                        <Button
                          title="Предыдущая страница"
                          small
                          variant="secondary"
                          disabled={page <= 1}
                          onPress={() => setPage((p) => p - 1)}
                        />
                        <AppText>
                          Страница {page} из{" "}
                          {Math.max(1, Math.ceil(rows.length / 20))}
                        </AppText>
                        <Button
                          title="Следующая страница"
                          small
                          variant="secondary"
                          disabled={page * 20 >= rows.length}
                          onPress={() => setPage((p) => p + 1)}
                        />
                      </Group>
                    </>
                  ) : (
                    <EmptyState
                      title="Записей нет"
                      text="Измените фильтры или добавьте запись."
                    />
                  )
                }
              </Async>
            </>
          ) : null}
          {selected ? (
            <Detail
              section={section}
              selected={selected}
              catalog={catalog}
              isAdmin={isAdmin}
              onClose={() => setSelected(undefined)}
              open={open}
              run={run}
              refresh={refresh}
            />
          ) : null}
        </ScrollView>
      </View>
      {!wide && menu ? (
        <Modal transparent visible onRequestClose={() => setMenu(false)}>
          <Pressable
            accessibilityLabel="Закрыть меню"
            onPress={() => setMenu(false)}
            style={{ flex: 1, backgroundColor: "#102B3B99" }}
          >
            <ScrollView
              style={{
                width: "90%",
                maxWidth: 320,
                backgroundColor: colors.card,
              }}
            >
              <Button
                title="Закрыть меню"
                variant="ghost"
                onPress={() => setMenu(false)}
              />
              {nav}
            </ScrollView>
          </Pressable>
        </Modal>
      ) : null}
      {editor ? (
        <Editor
          key={editor.schema.path}
          config={editor}
          catalog={catalog}
          onClose={() => setEditor(undefined)}
          onSaved={refresh}
        />
      ) : null}
      {confirm ? (
        <Modal
          transparent
          visible
          onRequestClose={() => {
            if (!action.isPending) setConfirm(undefined);
          }}
        >
          <View
            style={{
              flex: 1,
              justifyContent: "center",
              alignItems: "center",
              padding: 20,
              backgroundColor: "#102B3BCC",
            }}
          >
            <Card style={{ width: "100%", maxWidth: 480, gap: 20 }}>
              <AppText variant="h2">{confirm.title}</AppText>
              <Button
                title="Подтвердить"
                loading={action.isPending}
                onPress={() => run(confirm.action)}
              />
              <Button
                title="Отмена"
                variant="secondary"
                disabled={action.isPending}
                onPress={() => setConfirm(undefined)}
              />
            </Card>
          </View>
        </Modal>
      ) : null}
    </View>
  );
}
function Detail({
  section,
  selected,
  catalog,
  isAdmin,
  onClose,
  open,
  run,
  refresh,
}: {
  section: string;
  selected: Row;
  catalog: Catalog;
  isAdmin: boolean;
  onClose: () => void;
  open: (s: Schema, r?: Row, m?: string, e?: Record<string, unknown>) => void;
  run: (fn: () => Promise<unknown>) => void;
  refresh: () => void;
}) {
  const { user } = useAuth();
  const [text, setText] = useState("");
  const [internal, setInternal] = useState(false);
  const toast = useToast();
  const asset = !!selected.assetMode;
  const assetKind = str(selected, "assetKind") || section;
  const assetPath =
    assetKind === "listings"
      ? `/listings/${selected.id}/photos`
      : `/news/${selected.id}/attachments`;
  const query = useQuery({
    queryKey: ["management", "detail", section, selected.id, asset],
    queryFn: async () => {
      if (asset) return { assets: await management.list(assetPath) };
      if (section === "tenants") {
        const [detail, invoices, documents, requests] = await Promise.all([
          management.get<{ tenant: Row; leases: Row[] }>(
            `/admin/tenants/${selected.id}`,
          ),
          management.list(`/admin/invoices?tenantId=${selected.id}`),
          management.list(`/admin/documents?tenantId=${selected.id}`),
          management.list(`/requests?tenantId=${selected.id}`),
        ]);
        return { ...detail, invoices, documents, requests };
      }
      if (section === "requests")
        return management.get<{
          request: Row;
          messages: Row[];
          history: Row[];
          attachments: Row[];
        }>(`/requests/${selected.id}`);
      if (section === "news")
        return {
          receipts: await management.list(
            `/admin/news/${selected.id}/receipts`,
          ),
        };
      return {
        readings: (
          await management.list(isAdmin ? "/admin/readings" : "/staff/readings")
        ).filter((r) => r.meterId === selected.id),
      };
    },
    refetchInterval: section === "requests" ? 10000 : false,
  });
  const send = useMutation({
    mutationFn: () =>
      management.save(`/requests/${selected.id}/messages`, {
        text: text.trim(),
        internal,
      }),
    onSuccess: () => {
      setText("");
      refresh();
      toast(internal ? "Внутренняя заметка сохранена" : "Сообщение отправлено");
    },
    onError: (e: Error) => toast(e.message, "error"),
  });
  const author = (id: unknown) =>
    catalog.users?.find((r) => r.id === id)?.fullName ??
    (id === user?.id ? "Вы" : String(id).slice(0, 8));
  const lookup = (key: string, id: unknown) => {
    const r = catalog[key]?.find((r) => r.id === id);
    return r
      ? str(r, "name") || str(r, "number") || str(r, "fullName")
      : String(id ?? "—").slice(0, 8);
  };
  // The response shape is selected by this component's section, not by a user-supplied URL.
  return (
    <Modal visible transparent onRequestClose={onClose}>
      <View
        style={{
          flex: 1,
          backgroundColor: "#102B3BCC",
          alignItems: "center",
          justifyContent: "center",
          padding: 12,
        }}
      >
        <Card
          style={{
            width: "100%",
            maxWidth: 1000,
            maxHeight: "95%",
            padding: 0,
          }}
        >
          <View
            style={{
              padding: 16,
              flexDirection: "row",
              alignItems: "center",
              gap: 12,
              borderBottomWidth: 1,
              borderColor: colors.border,
            }}
          >
            <AppText variant="h2" style={{ flex: 1 }}>
              {asset
                ? "Файлы публикации"
                : section === "requests"
                  ? `Обращение №${selected.id.slice(0, 8)}`
                  : str(selected, "name") ||
                    str(selected, "title") ||
                    str(selected, "serialNumber")}
            </AppText>
            <Button
              title="Закрыть карточку"
              small
              variant="secondary"
              onPress={onClose}
            />
          </View>
          <ScrollView
            keyboardShouldPersistTaps="handled"
            contentContainerStyle={{ padding: 20, gap: 20 }}
          >
            <Async query={query}>
              {(data) => {
                if ("assets" in data) {
                  const rows = data.assets as Row[];
                  return (
                    <>
                      <AppText variant="small">
                        Файлы доступны арендаторам только после проверки.
                        Фотографии относятся к объявлению помещения.
                      </AppText>
                      <Button
                        title="Добавить файл публикации"
                        variant="secondary"
                        onPress={() =>
                          run(async () => {
                            const [file] = await pickDocuments(false);
                            if (!file) return;
                            const saved = await management.asset(
                              `/admin${assetPath}`,
                              file,
                            );
                            if (saved.state === "Quarantined")
                              toast("Файл ожидает проверки.");
                          })
                        }
                      />
                      {!rows.length ? (
                        <EmptyState title="Файлы пока не опубликованы" />
                      ) : (
                        rows.map((r) => (
                          <Card key={r.id} style={{ gap: 12 }}>
                            <AppText>{str(r, "name")}</AppText>
                            {assetKind === "listings" ? (
                              <PrivateImage
                                uri={str(r, "url")}
                                style={{ width: "100%", height: 180 }}
                                resizeMode="contain"
                              />
                            ) : null}
                            <Button
                              title="Скачать вложение"
                              small
                              variant="secondary"
                              onPress={() =>
                                run(() => downloadFile(str(r, "url")))
                              }
                            />
                          </Card>
                        ))
                      )}
                    </>
                  );
                }
                if ("request" in data) {
                  const d = data as {
                    request: Row;
                    messages: Row[];
                    history: Row[];
                    attachments: Row[];
                  };
                  const r = { ...selected, ...d.request };
                  const closed = ["Closed", "Rejected"].includes(
                    str(r, "status"),
                  );
                  return (
                    <>
                      <Group>
                        <RowText label="Тема" value={r.subject} />
                        <RowText
                          label="Категория"
                          value={lookup("routing", r.categoryId)}
                        />
                        <RowText
                          label="Арендатор"
                          value={r.tenantName ?? lookup("tenants", r.tenantId)}
                        />
                        <RowText
                          label="Помещение"
                          value={
                            r.premisesName ?? lookup("premises", r.premisesId)
                          }
                        />
                        <RowText
                          label="Ответственный"
                          value={lookup("users", r.assigneeId)}
                        />
                        <RowText
                          label="Срок"
                          value={fmtDateTime(str(r, "dueAt"))}
                        />
                      </Group>
                      <State value={str(r, "status")} />
                      <AppText>{str(r, "description")}</AppText>
                      <RowText
                        label="Контактное лицо"
                        value={`${r.contactName} · ${r.phone} · ${r.email}`}
                      />
                      <Group>
                        {isAdmin ? (
                          <Button
                            title="Назначить ответственного"
                            small
                            variant="secondary"
                            onPress={() =>
                              open(actions.assign(r.id), r, "PUT", {
                                version: r.version,
                              })
                            }
                          />
                        ) : null}
                        {transitions(str(r, "status")).length ? (
                          <Button
                            title="Изменить статус"
                            small
                            variant="secondary"
                            onPress={() =>
                              open(
                                actions.transition(
                                  r.id,
                                  transitions(str(r, "status")),
                                ),
                                undefined,
                                "PUT",
                                { version: r.version },
                              )
                            }
                          />
                        ) : null}
                      </Group>
                      <AppText variant="h2">Вложения</AppText>
                      {d.attachments.length ? (
                        d.attachments.map((f) => (
                          <Button
                            key={f.id}
                            title={`Скачать файл ${str(f, "fileId").slice(0, 8)}`}
                            small
                            variant="secondary"
                            onPress={() =>
                              run(() =>
                                downloadFile(`/api/v1/files/${f.fileId}`),
                              )
                            }
                          />
                        ))
                      ) : (
                        <AppText variant="small">Вложений нет.</AppText>
                      )}
                      <AppText variant="h2">Переписка</AppText>
                      {!d.messages.length ? (
                        <AppText variant="small">Сообщений пока нет.</AppText>
                      ) : (
                        d.messages.map((m) => (
                          <Card
                            key={m.id}
                            style={{
                              gap: 8,
                              backgroundColor: m.internal
                                ? colors.warningBg
                                : colors.bg,
                            }}
                          >
                            <Group>
                              <AppText variant="h3">
                                {str(m, "authorName") ||
                                  String(author(m.authorId))}
                              </AppText>
                              {m.internal ? (
                                <Badge
                                  label="Внутренняя заметка"
                                  tone="warning"
                                />
                              ) : (
                                <Badge label="Сообщение арендатору" />
                              )}
                              <AppText variant="caption">
                                {fmtDateTime(str(m, "createdAt"))}
                              </AppText>
                            </Group>
                            <AppText>{str(m, "text")}</AppText>
                          </Card>
                        ))
                      )}
                      {!closed ? (
                        <View style={{ gap: 12 }}>
                          <TextField
                            label="Текст ответа"
                            editable={!send.isPending}
                            value={text}
                            onChangeText={setText}
                            multiline
                          />
                          <SelectField
                            label="Вид сообщения"
                            value={internal ? "internal" : "public"}
                            onChange={(v) => setInternal(v === "internal")}
                            options={[
                              {
                                value: "public",
                                label: "Публичный ответ арендатору",
                              },
                              {
                                value: "internal",
                                label: "Внутренняя заметка сотрудникам",
                              },
                            ]}
                          />
                          <AppText variant="caption">
                            {internal
                              ? "Внутренняя заметка недоступна арендатору."
                              : "Этот ответ будет виден арендатору."}
                          </AppText>
                          <Button
                            title="Отправить ответ"
                            disabled={!text.trim()}
                            loading={send.isPending}
                            onPress={() => send.mutate()}
                          />
                        </View>
                      ) : (
                        <AppText variant="small">
                          Обращение закрыто для переписки.
                        </AppText>
                      )}
                      <AppText variant="h2">История статусов</AppText>
                      {d.history.map((h) => (
                        <Card key={h.id} style={{ gap: 6 }}>
                          <AppText>
                            {label(h.fromStatus)} → {label(h.toStatus)}
                          </AppText>
                          <AppText>{str(h, "reason")}</AppText>
                          <AppText variant="caption">
                            {fmtDateTime(str(h, "createdAt"))} ·{" "}
                            {String(author(h.actorId))}
                          </AppText>
                        </Card>
                      ))}
                    </>
                  );
                }
                if ("tenant" in data) {
                  const d = data as {
                    tenant: Row;
                    leases: Row[];
                    invoices: Row[];
                    documents: Row[];
                    requests: Row[];
                  };
                  return (
                    <>
                      <Group>
                        <RowText label="ИНН" value={d.tenant.inn} />
                        <RowText
                          label="Контактное лицо"
                          value={d.tenant.contactName}
                        />
                        <RowText label="Телефон" value={d.tenant.phone} />
                        <RowText label="Email" value={d.tenant.email} />
                      </Group>
                      <AppText variant="h2">Пользователи организации</AppText>
                      {catalog.users
                        .filter((u) => u.tenantId === selected.id)
                        .map((u) => (
                          <AppText key={u.id}>
                            {str(u, "userName")} · {str(u, "fullName")} ·{" "}
                            {u.blocked ? "Заблокирован" : "Доступ открыт"}
                          </AppText>
                        ))}
                      <Button
                        title="Привязать помещение к арендатору"
                        variant="secondary"
                        onPress={() =>
                          open(schemas.leases, {
                            id: "",
                            tenantId: selected.id,
                          })
                        }
                      />
                      <AppText variant="h2">Помещения и договоры</AppText>
                      {!d.leases.length ? (
                        <AppText variant="small">Договоров нет.</AppText>
                      ) : (
                        d.leases.map((l) => (
                          <Card key={l.id} style={{ gap: 8 }}>
                            <AppText variant="h3">
                              {lookup("premises", l.premisesId)} · №
                              {str(l, "number")}
                            </AppText>
                            <AppText>
                              {fmtDate(str(l, "signedOn"))} ·{" "}
                              {fmtDate(str(l, "startsOn"))} —{" "}
                              {fmtDate(str(l, "endsOn"))}
                            </AppText>
                            <State value={str(l, "status")} />
                          </Card>
                        ))
                      )}
                      <AppText variant="h2">
                        Счета · {d.invoices.length}
                      </AppText>
                      {d.invoices.map((i) => (
                        <AppText key={i.id}>
                          №{str(i, "number")} · {fmtMoney(Number(i.amount))} ·
                          оплачено {fmtMoney(Number(i.paid))}
                        </AppText>
                      ))}
                      <AppText variant="h2">
                        Документы · {d.documents.length}
                      </AppText>
                      {d.documents.map((doc) => (
                        <Button
                          key={doc.id}
                          title={
                            str(doc, "title") + (doc.archived ? " · Архив" : "")
                          }
                          variant="secondary"
                          onPress={() =>
                            run(() =>
                              downloadFile(`/api/v1/documents/${doc.id}/file`),
                            )
                          }
                        />
                      ))}
                      <AppText variant="h2">
                        Обращения · {d.requests.length}
                      </AppText>
                      {d.requests.map((r) => (
                        <Card key={r.id} style={{ gap: 6 }}>
                          <AppText variant="h3">{str(r, "subject")}</AppText>
                          <State value={str(r, "status")} />
                          <AppText variant="caption">
                            {fmtDateTime(str(r, "createdAt"))}
                          </AppText>
                        </Card>
                      ))}
                    </>
                  );
                }
                if ("receipts" in data) {
                  const receipts = data.receipts as Row[];
                  const today = new Date().toISOString().slice(0, 10);
                  const active = catalog.leases.filter(
                    (l) =>
                      l.status === "Active" &&
                      str(l, "startsOn") <= today &&
                      str(l, "endsOn") >= today,
                  );
                  const tenants = catalog.tenants.filter(
                    (t) =>
                      !t.blocked &&
                      (selected.allTenants ||
                        ((selected.tenantIds as string[]) ?? []).includes(
                          t.id,
                        ) ||
                        active.some((l) => {
                          if (l.tenantId !== t.id) return false;
                          const p = catalog.premises.find(
                            (p) => p.id === l.premisesId,
                          );
                          return (
                            p &&
                            ((
                              (selected.premisesIds as string[]) ?? []
                            ).includes(p.id) ||
                              ((selected.building ||
                                selected.section ||
                                selected.floor != null) &&
                                (!selected.building ||
                                  selected.building === p.building) &&
                                (!selected.section ||
                                  selected.section === p.section) &&
                                (selected.floor == null ||
                                  selected.floor === p.floor)))
                          );
                        })),
                  );
                  const recipients = catalog.users.filter(
                    (u) =>
                      u.role === "Tenant" &&
                      u.approved &&
                      !u.blocked &&
                      tenants.some((t) => t.id === u.tenantId),
                  );
                  return (
                    <>
                      <AppText variant="h2">{str(selected, "title")}</AppText>
                      <AppText>{str(selected, "body")}</AppText>
                      <AppText variant="small">
                        Получатели рассчитаны по текущим правилам и действующим
                        договорам. Доставка email, SMS и push не отслеживается.
                        Сервер возвращает до 1000 отметок прочтения.
                      </AppText>
                      <Group>
                        <RowText
                          label="Получатели по правилам"
                          value={recipients.length}
                        />
                        <RowText
                          label="Открыли"
                          value={receipts.filter((r) => r.readAt).length}
                        />
                        <RowText
                          label="Подтвердили"
                          value={
                            receipts.filter((r) => r.acknowledgedAt).length
                          }
                        />
                      </Group>
                      {recipients.map((u) => {
                        const receipt = receipts.find((r) => r.userId === u.id);
                        return (
                          <Card key={u.id} style={{ gap: 8 }}>
                            <AppText variant="h3">
                              {str(u, "fullName")} · {str(u, "userName")}
                            </AppText>
                            <AppText variant="small">
                              {lookup("tenants", u.tenantId)}
                            </AppText>
                            <AppText>
                              Открыто:{" "}
                              {receipt?.readAt
                                ? fmtDateTime(str(receipt, "readAt"))
                                : "Не открыто"}
                            </AppText>
                            <AppText>
                              Подтверждено:{" "}
                              {receipt?.acknowledgedAt
                                ? fmtDateTime(str(receipt, "acknowledgedAt"))
                                : "Не подтверждено"}
                            </AppText>
                          </Card>
                        );
                      })}
                    </>
                  );
                }
                const readings = (
                  "readings" in data ? data.readings : []
                ) as Row[];
                return (
                  <>
                    <AppText>
                      Счётчик: {str(selected, "serialNumber")} ·{" "}
                      {label(selected.type)}
                    </AppText>
                    <AppText variant="h2">История показаний</AppText>
                    {!readings.length ? (
                      <AppText variant="small">Показаний пока нет.</AppText>
                    ) : (
                      readings.map((r) => (
                        <Card key={r.id} style={{ gap: 8 }}>
                          <AppText>
                            {r.previousValue as number} → {r.value as number}{" "}
                            {str(selected, "unit")}
                          </AppText>
                          <State value={str(r, "status")} />
                          <AppText variant="caption">
                            {fmtDateTime(str(r, "createdAt"))}
                          </AppText>
                          <AppText>{str(r, "reviewReason")}</AppText>
                        </Card>
                      ))
                    )}
                  </>
                );
              }}
            </Async>
          </ScrollView>
        </Card>
      </View>
    </Modal>
  );
}
function IntegrationStatus() {
  const query = useQuery({
    queryKey: ["management", "integrations"],
    queryFn: () =>
      management.get<Record<string, unknown>>("/admin/integrations"),
  });
  return (
    <Async query={query}>
      {(d) => (
        <Card style={{ gap: 12 }}>
          <AppText variant="h2">Состояние внешних систем</AppText>
          {[
            ["accounting", "1С"],
            ["bitrix", "Битрикс"],
            ["payments", "Онлайн-оплата"],
            ["email", "Почта"],
          ].map(([key, title]) => (
            <Group key={key}>
              <AppText>{title}</AppText>
              <Badge label={label(d[key])} tone="warning" />
            </Group>
          ))}
          <AppText variant="small">
            Автоматическая внешняя доставка отключена. Ниже показаны сохранённые
            события, ожидающие настройки интеграций.
          </AppText>
        </Card>
      )}
    </Async>
  );
}
function Profile({
  open,
  signOut,
}: {
  open: (s: Schema, r?: Row, m?: string) => void;
  signOut: () => Promise<void>;
}) {
  const query = useQuery({
    queryKey: ["management", "me"],
    queryFn: management.identity,
  });
  return (
    <Async query={query}>
      {(u) => (
        <>
          <Card style={{ gap: 12 }}>
            <AppText variant="h2">{u.fullName}</AppText>
            <RowText label="Логин" value={u.userName} />
            <RowText label="Email" value={u.email} />
            <RowText label="Роль" value={label(u.role)} />
            <Button
              title="Изменить профиль"
              variant="secondary"
              onPress={() =>
                open(
                  {
                    title: "Мой профиль",
                    path: "/auth/profile",
                    fields: [
                      { key: "fullName", label: "ФИО", required: true },
                      { key: "phone", label: "Телефон", required: true },
                    ],
                  },
                  { id: u.id, fullName: u.fullName, phone: u.phoneNumber },
                  "PUT",
                )
              }
            />
            <PasswordForm signOut={signOut} />
          </Card>
        </>
      )}
    </Async>
  );
}
function PasswordForm({ signOut }: { signOut: () => Promise<void> }) {
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const toast = useToast();
  const m = useMutation({
    mutationFn: async () => {
      if (
        next.length < 12 ||
        !/[A-Z]/.test(next) ||
        !/[a-z]/.test(next) ||
        !/[0-9]/.test(next) ||
        !/[^a-zA-Z0-9]/.test(next)
      )
        throw new Error(
          "Новый пароль: от 12 символов, буквы обоих регистров, цифра, спецсимвол.",
        );
      await management.save("/auth/change-password", {
        currentPassword: current,
        newPassword: next,
      });
    },
    onSuccess: () => {
      void signOut().catch(() => {});
      toast("Пароль изменён. Войдите заново.");
    },
    onError: (e: Error) => toast(e.message, "error"),
  });
  return (
    <View style={{ gap: 12 }}>
      <AppText variant="h3">Смена пароля</AppText>
      <TextField
        label="Текущий пароль"
        value={current}
        onChangeText={setCurrent}
        secureTextEntry
      />
      <TextField
        label="Новый пароль"
        value={next}
        onChangeText={setNext}
        secureTextEntry
        hint="Не менее 12 символов: буквы обоих регистров, цифра, спецсимвол"
      />
      <Button
        title="Изменить пароль"
        disabled={!current || !next}
        loading={m.isPending}
        onPress={() => m.mutate()}
      />
    </View>
  );
}
