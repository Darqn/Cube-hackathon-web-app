import { str, type Row } from "@/management/api";
export type QueueFilters = {
  search: string;
  status: string;
  category: string;
  tenant: string;
  premise: string;
  view: string;
  sort: string;
};
export const emptyFilters: QueueFilters = {
  search: "",
  status: "",
  category: "",
  tenant: "",
  premise: "",
  view: "all",
  sort: "deadline",
};
export const terminal = (r: Row) =>
  ["Completed", "Closed", "Rejected"].includes(str(r, "status"));
export function filterQueue(
  rows: Row[],
  f: QueueFilters,
  unread: Set<string>,
  now = new Date(),
): Row[] {
  const start = new Date(now);
  start.setHours(0, 0, 0, 0);
  const end = new Date(start);
  end.setDate(end.getDate() + 1);
  return rows
    .filter((r) => {
      const due = r.dueAt ? Date.parse(str(r, "dueAt")) : null;
      return (
        (!f.search ||
          [
            r.subject,
            r.description,
            r.tenantName,
            r.premisesName,
            r.contactName,
            r.phone,
            r.email,
            r.id,
          ]
            .join(" ")
            .toLowerCase()
            .includes(f.search.toLowerCase())) &&
        (!f.status || r.status === f.status) &&
        (!f.category || r.categoryId === f.category) &&
        (!f.tenant || r.tenantId === f.tenant) &&
        (!f.premise || r.premisesId === f.premise) &&
        (f.view === "all" ||
          (f.view === "active" && !terminal(r)) ||
          (f.view === "new" && r.status === "New") ||
          (f.view === "waiting" && r.status === "Clarification") ||
          (f.view === "unread" && unread.has(r.id)) ||
          (f.view === "overdue" &&
            !terminal(r) &&
            due !== null &&
            due < now.getTime()) ||
          (f.view === "today" &&
            !terminal(r) &&
            due !== null &&
            due >= start.getTime() &&
            due < end.getTime()))
      );
    })
    .sort((a, b) => {
      if (f.sort === "unread" && unread.has(a.id) !== unread.has(b.id))
        return unread.has(a.id) ? -1 : 1;
      if (f.sort === "oldest")
        return (
          str(a, "createdAt").localeCompare(str(b, "createdAt")) ||
          a.id.localeCompare(b.id)
        );
      if (f.sort === "newest")
        return (
          str(b, "createdAt").localeCompare(str(a, "createdAt")) ||
          a.id.localeCompare(b.id)
        );
      if (f.sort === "deadline") {
        const aa = a.dueAt ? Date.parse(str(a, "dueAt")) : Infinity;
        const bb = b.dueAt ? Date.parse(str(b, "dueAt")) : Infinity;
        if (aa !== bb) return aa < bb ? -1 : 1;
      }
      return (
        str(b, "createdAt").localeCompare(str(a, "createdAt")) ||
        a.id.localeCompare(b.id)
      );
    });
}
export function notificationTarget(
  n: Row,
): { section: "requests" | "readings"; id: string } | null {
  if (!n.resourceId) return null;
  if (str(n, "kind").startsWith("StaffRequest"))
    return { section: "requests", id: String(n.resourceId) };
  if (n.kind === "StaffReadingSubmitted")
    return { section: "readings", id: String(n.resourceId) };
  return null;
}
export const noticeNames: Record<string, string> = {
  StaffRequestAssigned: "Назначено обращение",
  StaffRequestDeadline: "Изменён срок",
  StaffRequestMessage: "Новое сообщение",
  StaffRequestAttachment: "Новое вложение",
  StaffRequestInternalComment: "Внутренняя заметка",
  StaffRequestStatus: "Изменён статус",
  StaffReadingSubmitted: "Новые показания",
};
