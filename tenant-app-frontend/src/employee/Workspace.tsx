import { useEffect, useState, type ReactNode } from "react";
import {
  Modal,
  Pressable,
  ScrollView,
  View,
  useWindowDimensions,
} from "react-native";
import { Redirect, useLocalSearchParams, useRouter } from "expo-router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/auth/AuthProvider";
import { AppText, Badge, Button, Card, Icon } from "@/ui/primitives";
import { TextField, SelectField } from "@/ui/form";
import { Async, EmptyState, useToast } from "@/ui/feedback";
import { PrivateImage } from "@/ui/PrivateImage";
import { colors } from "@/theme";
import { fmtDateTime } from "@/lib/format";
import { downloadFile } from "@/lib/files";
import { Detail, Profile } from "@/management/Workspace";
import { Editor, type EditorConfig, type Catalog } from "@/management/Editor";
import { actions, statuses, type Schema } from "@/management/forms";
import { management, roleHome, str, type Row } from "@/management/api";
import { employee } from "./api";
import {
  filterQueue,
  emptyFilters,
  terminal,
  notificationTarget,
  noticeNames,
  type QueueFilters,
} from "./models";
const sections = [
  ["overview", "Обзор", "grid-outline"],
  ["requests", "Обращения", "chatbubbles-outline"],
  ["notifications", "Уведомления", "notifications-outline"],
  ["meters", "Счётчики", "speedometer-outline"],
  ["readings", "Показания", "analytics-outline"],
  ["profile", "Мой профиль", "person-circle-outline"],
];
const viewOptions = [
  { value: "all", label: "Все обращения" },
  { value: "active", label: "В работе" },
  { value: "new", label: "Новые" },
  { value: "overdue", label: "Просроченные" },
  { value: "today", label: "Срок сегодня" },
  { value: "waiting", label: "Ожидают уточнения" },
  { value: "unread", label: "С новыми событиями" },
];
const stateNames: Record<string, string> = {
  ...statuses,
  Submitted: "Ожидает проверки",
  Accepted: "Принято",
  Rejected: "Отклонено",
  Correction: "Коррекция",
};
function Group({ children }: { children: ReactNode }) {
  return (
    <View
      style={{
        flexDirection: "row",
        flexWrap: "wrap",
        gap: 12,
        alignItems: "flex-start",
      }}
    >
      {children}
    </View>
  );
}
function Value({ label, value }: { label: string; value: unknown }) {
  return (
    <View style={{ gap: 3, minWidth: 130, flexGrow: 1 }}>
      <AppText variant="caption">{label}</AppText>
      <AppText>{value == null || value === "" ? "—" : String(value)}</AppText>
    </View>
  );
}
export default function EmployeeRoute() {
  const { user, isAuthed } = useAuth();
  const { section = "overview" } = useLocalSearchParams<{ section: string }>();
  if (!isAuthed) return <Redirect href="/login" />;
  if (user?.role !== "Staff")
    return <Redirect href={roleHome(user?.role) as never} />;
  if (!sections.some((s) => s[0] === section))
    return <Redirect href="/staff/overview" />;
  return <Workspace key={section} section={section} />;
}
function Workspace({ section }: { section: string }) {
  const { user, signOut } = useAuth();
  const params = useLocalSearchParams<{ view?: string }>();
  const router = useRouter();
  const qc = useQueryClient();
  const toast = useToast();
  const wide = useWindowDimensions().width >= 1000;
  const [menu, setMenu] = useState(false);
  const [filters, setFilters] = useState<QueueFilters>({
    ...emptyFilters,
    view:
      params.view && viewOptions.some((v) => v.value === params.view)
        ? params.view
        : "all",
  });
  const [page, setPage] = useState(1);
  const [inboxView, setInboxView] = useState("unread");
  const [readingStatus, setReadingStatus] = useState("Submitted");
  const [meterId, setMeterId] = useState("");
  const [search, setSearch] = useState("");
  const [selected, setSelected] = useState<{
    section: "requests" | "reading" | "meter";
    row: Row;
  }>();
  const [editor, setEditor] = useState<EditorConfig>();
  const [confirmLogout, setConfirmLogout] = useState(false);
  const dashboard = useQuery({
    queryKey: ["employee", "dashboard"],
    queryFn: employee.dashboard,
    refetchInterval: 30000,
  });
  const requests = useQuery({
    queryKey: ["employee", "requests"],
    queryFn: employee.requests,
    refetchInterval: 20000,
  });
  const notifications = useQuery({
    queryKey: ["employee", "notifications"],
    queryFn: employee.notifications,
    refetchInterval: 20000,
  });
  const meters = useQuery({
    queryKey: ["employee", "meters"],
    queryFn: employee.meters,
    refetchInterval: 45000,
  });
  const categories = useQuery({
    queryKey: ["employee", "categories"],
    queryFn: () => management.list("/request-categories"),
    staleTime: 60000,
  });
  const readings = useQuery({
    queryKey: ["employee", "readings"],
    queryFn: employee.readings,
    refetchInterval: 30000,
  });
  const refresh = () => {
    void qc.invalidateQueries({ queryKey: ["employee"] });
    void qc.invalidateQueries({ queryKey: ["management"] });
  };
  const mutation = useMutation({
    mutationFn: (fn: () => Promise<unknown>) => fn(),
    onSuccess: refresh,
    onError: (e: Error) => toast(e.message, "error"),
  });
  const run = (fn: () => Promise<unknown>) => mutation.mutate(fn);
  const unread = (notifications.data ?? []).filter((n) => !n.read);
  const unreadRequests = new Set(
    unread
      .filter((n) => str(n, "kind").startsWith("StaffRequest"))
      .map((n) => str(n, "resourceId")),
  );
  const catalog: Catalog = {
    meters: meters.data ?? [],
    routing: categories.data ?? [],
    tenants: [],
    premises: [],
    users: [],
    leases: [],
  };
  const openEditor = (
    schema: Schema,
    row?: Row,
    method?: string,
    extra?: Record<string, unknown>,
  ) => setEditor({ schema, row, method, extra });
  const go = (key: string) => {
    setMenu(false);
    router.replace(`/staff/${key}` as never);
  };
  const change = (key: keyof QueueFilters, value: string) =>
    setFilters((f) => ({ ...f, [key]: value }));
  useEffect(() => setPage(1), [filters]);
  const openRequest = (r: Row) => {
    setSelected({ section: "requests", row: r });
    const events = unread.filter(
      (n) => n.resourceId === r.id && str(n, "kind").startsWith("StaffRequest"),
    );
    if (events.length)
      run(() => Promise.all(events.map((n) => employee.read(n.id))));
  };
  const openNotice = (n: Row) => {
    const target = notificationTarget(n);
    const row =
      target?.section === "requests"
        ? requests.data?.find((r) => r.id === target.id)
        : readings.data?.find((r) => r.id === target?.id);
    if (!row) {
      toast("Запись больше недоступна. Обновите данные.", "error");
      refresh();
      return;
    }
    setSelected({
      section: target?.section === "requests" ? "requests" : "reading",
      row,
    });
    if (!n.read) run(() => employee.read(n.id));
  };
  const requestCard = (r: Row) => {
    const overdue =
      !terminal(r) && r.dueAt && Date.parse(str(r, "dueAt")) < Date.now();
    return (
      <Card
        key={r.id}
        style={{
          gap: 14,
          borderColor: overdue ? colors.danger : colors.border,
        }}
      >
        <Group>
          <View style={{ flex: 1, minWidth: 170, gap: 4 }}>
            <AppText variant="h3">{str(r, "subject")}</AppText>
            <AppText variant="caption">
              №{r.id.slice(0, 8)} · {fmtDateTime(str(r, "createdAt"))}
            </AppText>
          </View>
          <Badge
            label={
              statuses[str(r, "status") as keyof typeof statuses] ??
              str(r, "status")
            }
            tone={terminal(r) ? "success" : "info"}
          />
          {unreadRequests.has(r.id) ? (
            <Badge label="Новое событие" tone="warning" />
          ) : null}
        </Group>
        <Group>
          <Value label="Арендатор" value={r.tenantName} />
          <Value label="Помещение" value={r.premisesName} />
          <Value
            label="Категория"
            value={
              categories.data?.find((c) => c.id === r.categoryId)?.name ??
              "Назначенное обращение"
            }
          />
          <Value
            label={overdue ? "Срок просрочен" : "Срок выполнения"}
            value={r.dueAt ? fmtDateTime(str(r, "dueAt")) : "Не установлен"}
          />
        </Group>
        <AppText variant="small" numberOfLines={2}>
          {str(r, "description")}
        </AppText>
        <Button
          title="Открыть обращение"
          small
          onPress={() => openRequest(r)}
        />
      </Card>
    );
  };
  const meterName = (id: unknown) => meters.data?.find((m) => m.id === id);
  const readingCard = (r: Row) => {
    const m = meterName(r.meterId);
    return (
      <Card key={r.id} style={{ gap: 12 }}>
        <Group>
          <View style={{ flex: 1, minWidth: 170 }}>
            <AppText variant="h3">
              {m ? str(m, "serialNumber") : "Счётчик"} ·{" "}
              {r.previousValue as number} → {r.value as number}
            </AppText>
            <AppText variant="small">
              {m ? `${str(m, "tenantName")} · ${str(m, "premisesName")}` : ""}
            </AppText>
          </View>
          <Badge
            label={stateNames[str(r, "status")] ?? str(r, "status")}
            tone={
              r.status === "Rejected"
                ? "danger"
                : r.status === "Submitted"
                  ? "warning"
                  : "success"
            }
          />
        </Group>
        <AppText variant="caption">
          {fmtDateTime(str(r, "createdAt"))} · {m ? str(m, "unit") : ""}
        </AppText>
        {r.reviewReason ? <AppText>{str(r, "reviewReason")}</AppText> : null}
        <Button
          title="Открыть показание"
          small
          variant="secondary"
          onPress={() => setSelected({ section: "reading", row: r })}
        />
      </Card>
    );
  };
  const queue = filterQueue(requests.data ?? [], filters, unreadRequests);
  const totalPages = Math.max(1, Math.ceil(queue.length / 20));
  const currentPage = Math.min(page, totalPages);
  const choices = (key: string, name: string) =>
    Array.from(
      new Map(
        (requests.data ?? [])
          .filter((r) => r[key])
          .map((r) => [str(r, key), str(r, name) || str(r, key).slice(0, 8)]),
      ).entries(),
    ).map(([value, label]) => ({ value, label }));
  const nav = (
    <View style={{ padding: 12, gap: 4 }}>
      {sections.map(([key, title, icon]) => (
        <Pressable
          key={key}
          accessibilityRole="link"
          accessibilityLabel={title}
          accessibilityState={{ selected: section === key }}
          onPress={() => go(key)}
          style={{
            padding: 12,
            borderRadius: 10,
            backgroundColor:
              section === key ? colors.accentSoft : "transparent",
            flexDirection: "row",
            alignItems: "center",
            gap: 10,
          }}
        >
          <Icon name={icon as never} />
          <AppText
            style={{ flex: 1, fontWeight: section === key ? "700" : "400" }}
          >
            {title}
          </AppText>
          {key === "notifications" && unread.length ? (
            <Badge label={String(unread.length)} tone="danger" />
          ) : null}
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
        {wide ? (
          <Icon name="briefcase-outline" color="#FFFFFF" size={28} />
        ) : (
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Меню сотрудника"
            onPress={() => setMenu(true)}
            style={{
              minWidth: 44,
              minHeight: 44,
              alignItems: "center",
              justifyContent: "center",
              borderWidth: 1,
              borderColor: "#C6DADC",
              borderRadius: 10,
            }}
          >
            <Icon name="menu-outline" color="#FFFFFF" size={26} />
          </Pressable>
        )}
        <View style={{ flex: 1 }}>
          <AppText color="#FFFFFF" variant="h3">
            КУБ · Рабочий кабинет
          </AppText>
          <AppText color="#C6DADC" variant="small" numberOfLines={2}>
            {user?.fullName} · Сотрудник компании
          </AppText>
        </View>
        <Button
          title="Выйти"
          small
          variant="secondary"
          onPress={() => setConfirmLogout(true)}
        />
      </View>
      <View style={{ flex: 1, flexDirection: "row" }}>
        {wide ? (
          <ScrollView
            style={{ width: 250, flexGrow: 0, backgroundColor: colors.card }}
          >
            {nav}
          </ScrollView>
        ) : null}
        <ScrollView
          style={{ flex: 1 }}
          contentContainerStyle={{
            padding: wide ? 28 : 16,
            paddingBottom: 48,
            gap: 20,
            width: "100%",
            maxWidth: 1350,
            alignSelf: "center",
          }}
        >
          <Group>
            <View style={{ flex: 1, minWidth: 170, gap: 4 }}>
              <AppText variant="title">
                {section === "requests"
                  ? "Мои обращения"
                  : sections.find((s) => s[0] === section)?.[1]}
              </AppText>
              <AppText variant="small">
                {section === "overview"
                  ? "Назначенные задачи, сроки и новые события"
                  : "Доступны только ваши обращения и ответственное направление"}
              </AppText>
            </View>
            <Button
              title="Обновить"
              small
              variant="secondary"
              onPress={refresh}
            />
          </Group>
          {section === "overview" ? (
            <>
              <Async query={dashboard}>
                {(d) => (
                  <>
                    <Group>
                      {[
                        ["Новые обращения", d.new, "new"],
                        ["В работе", d.active, "active"],
                        ["Просрочено", d.overdue, "overdue"],
                        ["Ожидают уточнения", d.waitingForTenant, "waiting"],
                      ].map(([title, value, view]) => (
                        <Card
                          key={String(view)}
                          onPress={() => {
                            setFilters((f) => ({ ...f, view: String(view) }));
                            router.replace(
                              `/staff/requests?view=${view}` as never,
                            );
                          }}
                          style={{ flexGrow: 1, minWidth: 145, gap: 12 }}
                        >
                          <AppText variant="small">{title}</AppText>
                          <AppText
                            variant="title"
                            color={
                              view === "overdue" && Number(value) > 0
                                ? colors.danger
                                : colors.heading
                            }
                          >
                            {value}
                          </AppText>
                        </Card>
                      ))}
                    </Group>
                    <Card style={{ gap: 12 }}>
                      <AppText variant="h2">Ваши направления</AppText>
                      {d.directions.length ? (
                        d.directions.map((c) => (
                          <View key={c.id} style={{ gap: 3 }}>
                            <AppText variant="h3">{str(c, "name")}</AppText>
                            <AppText variant="small">
                              {str(c, "department")}
                            </AppText>
                          </View>
                        ))
                      ) : (
                        <AppText variant="small">
                          Направления не назначены. С конкретными обращениями
                          можно работать после назначения ответственным.
                        </AppText>
                      )}
                      <AppText variant="caption">
                        Распределение обращений и направлений выполняет
                        администратор. Интеграция с Битрикс пока не подключена.
                      </AppText>
                    </Card>
                    <Group>
                      <Button
                        title={`Уведомления: ${d.unreadNotifications}`}
                        variant="secondary"
                        onPress={() => go("notifications")}
                      />
                      <Button
                        title={`Показания на проверку: ${d.pendingReadings}`}
                        variant="secondary"
                        onPress={() => go("readings")}
                      />
                    </Group>
                  </>
                )}
              </Async>
              <AppText variant="h2">Ближайшие задачи</AppText>
              <Async query={requests}>
                {(rows) => {
                  const active = filterQueue(
                    rows,
                    { ...emptyFilters, view: "active" },
                    unreadRequests,
                  ).slice(0, 5);
                  return active.length ? (
                    <View style={{ gap: 16 }}>{active.map(requestCard)}</View>
                  ) : (
                    <EmptyState
                      title="Активных задач нет"
                      text="Новые назначения появятся здесь и в уведомлениях."
                    />
                  );
                }}
              </Async>
            </>
          ) : null}
          {section === "requests" ? (
            <>
              <Group>
                <View style={{ flexGrow: 1, minWidth: 180 }}>
                  <TextField
                    label="Поиск обращений"
                    value={filters.search}
                    onChangeText={(v) => change("search", v)}
                    placeholder="Тема, организация, помещение, контакт…"
                  />
                </View>
                <View style={{ flexGrow: 1, minWidth: 170 }}>
                  <SelectField
                    label="Очередь"
                    value={filters.view}
                    onChange={(v) => change("view", v)}
                    options={viewOptions}
                  />
                </View>
              </Group>
              <Group>
                <View style={{ flexGrow: 1, minWidth: 165 }}>
                  <SelectField
                    label="Статус обращения"
                    value={filters.status}
                    onChange={(v) => change("status", v)}
                    options={[
                      { value: "", label: "Все статусы" },
                      ...Object.entries(statuses).map(([value, label]) => ({
                        value,
                        label,
                      })),
                    ]}
                  />
                </View>
                <View style={{ flexGrow: 1, minWidth: 165 }}>
                  <SelectField
                    label="Категория обращения"
                    value={filters.category}
                    onChange={(v) => change("category", v)}
                    options={[
                      { value: "", label: "Все категории" },
                      ...(categories.data ?? []).map((c) => ({
                        value: c.id,
                        label: str(c, "name"),
                      })),
                    ]}
                  />
                </View>
                <View style={{ flexGrow: 1, minWidth: 165 }}>
                  <SelectField
                    label="Арендатор обращения"
                    value={filters.tenant}
                    onChange={(v) => change("tenant", v)}
                    options={[
                      { value: "", label: "Все арендаторы" },
                      ...choices("tenantId", "tenantName"),
                    ]}
                  />
                </View>
                <View style={{ flexGrow: 1, minWidth: 165 }}>
                  <SelectField
                    label="Помещение обращения"
                    value={filters.premise}
                    onChange={(v) => change("premise", v)}
                    options={[
                      { value: "", label: "Все помещения" },
                      ...choices("premisesId", "premisesName"),
                    ]}
                  />
                </View>
                <View style={{ flexGrow: 1, minWidth: 165 }}>
                  <SelectField
                    label="Сортировка"
                    value={filters.sort}
                    onChange={(v) => change("sort", v)}
                    options={[
                      { value: "deadline", label: "По сроку выполнения" },
                      { value: "newest", label: "Сначала новые" },
                      { value: "oldest", label: "Сначала старые" },
                      { value: "unread", label: "Сначала новые события" },
                    ]}
                  />
                </View>
              </Group>
              <Button
                title="Сбросить фильтры"
                small
                variant="ghost"
                onPress={() => setFilters(emptyFilters)}
              />
              <Async query={requests}>
                {() =>
                  queue.length ? (
                    <>
                      <AppText variant="small">Найдено: {queue.length}</AppText>
                      {queue
                        .slice((currentPage - 1) * 20, currentPage * 20)
                        .map(requestCard)}
                      <Group>
                        <Button
                          title="Предыдущая страница"
                          small
                          variant="secondary"
                          disabled={currentPage <= 1}
                          onPress={() => setPage((p) => p - 1)}
                        />
                        <AppText>
                          {currentPage} / {totalPages}
                        </AppText>
                        <Button
                          title="Следующая страница"
                          small
                          variant="secondary"
                          disabled={currentPage >= totalPages}
                          onPress={() => setPage((p) => p + 1)}
                        />
                      </Group>
                    </>
                  ) : (
                    <EmptyState
                      title="Обращений не найдено"
                      text="Сбросьте фильтры или дождитесь назначения от администратора."
                    />
                  )
                }
              </Async>
            </>
          ) : null}
          {section === "notifications" ? (
            <>
              <Group>
                <View style={{ flexGrow: 1, minWidth: 170 }}>
                  <SelectField
                    label="Показать уведомления"
                    value={inboxView}
                    onChange={setInboxView}
                    options={[
                      { value: "unread", label: "Непрочитанные" },
                      { value: "all", label: "Все уведомления" },
                      { value: "requests", label: "По обращениям" },
                      { value: "readings", label: "По показаниям" },
                    ]}
                  />
                </View>
                <Button
                  title="Прочитать все"
                  variant="secondary"
                  disabled={!unread.length || mutation.isPending}
                  onPress={() => run(employee.readAll)}
                />
              </Group>
              <Async query={notifications}>
                {(rows) => {
                  const list = rows.filter(
                    (n) =>
                      inboxView === "all" ||
                      (inboxView === "unread" && !n.read) ||
                      inboxView === notificationTarget(n)?.section,
                  );
                  return list.length ? (
                    list.map((n) => (
                      <Card
                        key={n.id}
                        style={{
                          gap: 12,
                          backgroundColor: n.read
                            ? colors.card
                            : colors.accentSoft,
                        }}
                      >
                        <Group>
                          <View style={{ flex: 1, minWidth: 180 }}>
                            <AppText variant="h3">
                              {noticeNames[str(n, "kind")] ?? "Рабочее событие"}
                            </AppText>
                            <AppText variant="caption">
                              {fmtDateTime(str(n, "createdAt"))}
                            </AppText>
                          </View>
                          <Badge
                            label={n.read ? "Прочитано" : "Новое"}
                            tone={n.read ? "neutral" : "warning"}
                          />
                        </Group>
                        <AppText>{str(n, "text")}</AppText>
                        <Group>
                          <Button
                            title="Открыть событие"
                            small
                            onPress={() => openNotice(n)}
                          />
                          {!n.read ? (
                            <Button
                              title="Отметить прочитанным"
                              small
                              variant="secondary"
                              onPress={() => run(() => employee.read(n.id))}
                            />
                          ) : null}
                        </Group>
                      </Card>
                    ))
                  ) : (
                    <EmptyState
                      title="Новых уведомлений нет"
                      text="Здесь будут назначения, ответы арендаторов, изменения сроков и показания по вашему направлению."
                    />
                  );
                }}
              </Async>
            </>
          ) : null}
          {["meters", "readings"].includes(section) ? (
            <>
              <Card>
                <AppText variant="small">
                  Доступ определяется ответственностью за электричество или
                  водоснабжение/отопление. Показания можно принять или отклонить
                  с комментарием. Изменять базу счётчика может только
                  администратор.
                </AppText>
              </Card>
              <Group>
                <View style={{ minWidth: 180, flexGrow: 1 }}>
                  <TextField
                    label="Поиск счётчиков"
                    value={search}
                    onChangeText={setSearch}
                    placeholder="Номер, организация, помещение"
                  />
                </View>
                {section === "readings" ? (
                  <>
                    <View style={{ minWidth: 170, flexGrow: 1 }}>
                      <SelectField
                        label="Счётчик показаний"
                        value={meterId}
                        onChange={setMeterId}
                        options={[
                          { value: "", label: "Все счётчики" },
                          ...(meters.data ?? []).map((m) => ({
                            value: m.id,
                            label: str(m, "serialNumber"),
                            hint: str(m, "premisesName"),
                          })),
                        ]}
                      />
                    </View>
                    <View style={{ minWidth: 170, flexGrow: 1 }}>
                      <SelectField
                        label="Состояние проверки"
                        value={readingStatus}
                        onChange={setReadingStatus}
                        options={[
                          { value: "", label: "Вся история" },
                          { value: "Submitted", label: "Ожидает проверки" },
                          { value: "Accepted", label: "Принято" },
                          { value: "Rejected", label: "Отклонено" },
                          { value: "Correction", label: "Коррекция" },
                        ]}
                      />
                    </View>
                  </>
                ) : null}
              </Group>
              <Async query={meters}>
                {(ms) =>
                  section === "meters" ? (
                    ms.filter((m) =>
                      [m.serialNumber, m.tenantName, m.premisesName]
                        .join(" ")
                        .toLowerCase()
                        .includes(search.toLowerCase()),
                    ).length ? (
                      ms
                        .filter((m) =>
                          [m.serialNumber, m.tenantName, m.premisesName]
                            .join(" ")
                            .toLowerCase()
                            .includes(search.toLowerCase()),
                        )
                        .map((m) => (
                          <Card key={m.id} style={{ gap: 12 }}>
                            <Group>
                              <AppText variant="h3">
                                {str(m, "serialNumber")}
                              </AppText>
                              <Badge
                                label={
                                  m.type === "Water" ? "Вода" : "Электричество"
                                }
                              />
                              <Badge
                                label={m.active ? "Активен" : "Отключён"}
                              />
                            </Group>
                            <Group>
                              <Value label="Арендатор" value={m.tenantName} />
                              <Value label="Помещение" value={m.premisesName} />
                              <Value
                                label="Текущее значение"
                                value={`${m.lastValue} ${m.unit}`}
                              />
                            </Group>
                            <Button
                              title="История счётчика"
                              small
                              variant="secondary"
                              onPress={() =>
                                setSelected({ section: "meter", row: m })
                              }
                            />
                          </Card>
                        ))
                    ) : (
                      <EmptyState
                        title="Доступных счётчиков нет"
                        text="Проверьте назначенное направление или измените поиск."
                      />
                    )
                  ) : (
                    <Async query={readings}>
                      {(rs) => {
                        const filtered = rs.filter((r) => {
                          const m = meterName(r.meterId);
                          return (
                            (!readingStatus || r.status === readingStatus) &&
                            (!meterId || r.meterId === meterId) &&
                            [m?.serialNumber, m?.tenantName, m?.premisesName]
                              .join(" ")
                              .toLowerCase()
                              .includes(search.toLowerCase())
                          );
                        });
                        return filtered.length ? (
                          filtered.map(readingCard)
                        ) : (
                          <EmptyState
                            title="Показаний не найдено"
                            text="Выберите «Вся история» или другой счётчик."
                          />
                        );
                      }}
                    </Async>
                  )
                }
              </Async>
            </>
          ) : null}
          {section === "profile" ? (
            <Profile open={openEditor} signOut={signOut} />
          ) : null}
        </ScrollView>
      </View>
      {!wide && menu ? (
        <Modal visible transparent onRequestClose={() => setMenu(false)}>
          <View style={{ flex: 1, backgroundColor: "#102B3B99" }}>
            <ScrollView
              style={{
                width: "90%",
                maxWidth: 330,
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
          </View>
        </Modal>
      ) : null}
      {selected?.section === "requests" ? (
        <Detail
          section="requests"
          selected={selected.row}
          catalog={catalog}
          isAdmin={false}
          onClose={() => setSelected(undefined)}
          open={openEditor}
          run={run}
          refresh={refresh}
        />
      ) : null}
      {selected?.section === "meter" ? (
        <Detail
          section="meters"
          selected={selected.row}
          catalog={catalog}
          isAdmin={false}
          onClose={() => setSelected(undefined)}
          open={openEditor}
          run={run}
          refresh={refresh}
        />
      ) : null}
      {selected?.section === "reading" ? (
        <ReadingDetail
          row={
            readings.data?.find((r) => r.id === selected.row.id) ?? selected.row
          }
          meter={meterName(selected.row.meterId)}
          onClose={() => setSelected(undefined)}
          open={openEditor}
          run={run}
        />
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
      {confirmLogout ? (
        <Modal
          visible
          transparent
          onRequestClose={() => setConfirmLogout(false)}
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
            <Card style={{ width: "100%", maxWidth: 450, gap: 16 }}>
              <AppText variant="h2">Выйти из рабочего кабинета?</AppText>
              <Button
                title="Подтвердить выход"
                onPress={() => {
                  void signOut().catch(() =>
                    toast(
                      "Вы вышли локально. Сервер не подтвердил отзыв сессии.",
                      "error",
                    ),
                  );
                }}
              />
              <Button
                title="Отмена"
                variant="secondary"
                onPress={() => setConfirmLogout(false)}
              />
            </Card>
          </View>
        </Modal>
      ) : null}
    </View>
  );
}
function ReadingDetail({
  row,
  meter,
  onClose,
  open,
  run,
}: {
  row: Row;
  meter?: Row;
  onClose: () => void;
  open: (s: Schema, r?: Row, m?: string) => void;
  run: (fn: () => Promise<unknown>) => void;
}) {
  return (
    <Modal visible transparent onRequestClose={onClose}>
      <View
        style={{
          flex: 1,
          alignItems: "center",
          justifyContent: "center",
          padding: 12,
          backgroundColor: "#102B3BCC",
        }}
      >
        <Card
          style={{ width: "100%", maxWidth: 780, maxHeight: "94%", padding: 0 }}
        >
          <View
            style={{
              padding: 16,
              flexDirection: "row",
              alignItems: "center",
              gap: 12,
            }}
          >
            <AppText variant="h2" style={{ flex: 1 }}>
              Показание {meter ? str(meter, "serialNumber") : ""}
            </AppText>
            <Button
              title="Закрыть показание"
              small
              variant="secondary"
              onPress={onClose}
            />
          </View>
          <ScrollView contentContainerStyle={{ padding: 20, gap: 16 }}>
            <Group>
              <Value label="Организация" value={meter?.tenantName} />
              <Value label="Помещение" value={meter?.premisesName} />
              <Value
                label="Дата передачи"
                value={fmtDateTime(str(row, "createdAt"))}
              />
            </Group>
            <Group>
              <Value label="Предыдущее" value={row.previousValue} />
              <Value label="Новое" value={row.value} />
              <Value
                label="Расход"
                value={`${Number(row.value) - Number(row.previousValue)} ${meter?.unit ?? ""}`}
              />
            </Group>
            <Badge
              label={stateNames[str(row, "status")] ?? str(row, "status")}
              tone={row.status === "Rejected" ? "danger" : "info"}
            />
            {row.reviewReason ? (
              <AppText>{str(row, "reviewReason")}</AppText>
            ) : null}
            {row.photoId ? (
              <>
                <PrivateImage
                  uri={`/api/v1/staff/readings/${row.id}/photo`}
                  style={{ width: "100%", height: 300 }}
                  resizeMode="contain"
                />
                <Button
                  title="Скачать фото показания"
                  variant="secondary"
                  onPress={() =>
                    run(() =>
                      downloadFile(`/api/v1/staff/readings/${row.id}/photo`),
                    )
                  }
                />
              </>
            ) : (
              <AppText variant="small">Фото не приложено.</AppText>
            )}
            {row.status === "Submitted" ? (
              <Button
                title="Рассмотреть показание"
                onPress={() =>
                  open(
                    actions.review(`/staff/readings/${row.id}/review`),
                    undefined,
                    "PUT",
                  )
                }
              />
            ) : (
              <AppText variant="small">
                Проверка завершена. Повторное рассмотрение недоступно.
              </AppText>
            )}
          </ScrollView>
        </Card>
      </View>
    </Modal>
  );
}
