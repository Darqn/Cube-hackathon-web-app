import { request, fileForm, appendFile } from "@/api/http";
import type { LocalFile } from "@/api/types";
export type Role = "Admin" | "Staff" | "Tenant";
export type Identity = {
  id: string;
  userName: string;
  fullName: string;
  email: string;
  phoneNumber: string;
  role: Role;
  tenantId: string | null;
};
export type Row = { id: string; [key: string]: unknown };
export const str = (r: Row, key: string) =>
  r[key] == null ? "" : String(r[key]);
export async function list(path: string): Promise<Row[]> {
  // These endpoints return their full bounded collection and ignore page/size.
  if (
    path.split("?")[0] === "/request-categories" ||
    /\/(receipts|photos|attachments)$/.test(path.split("?")[0])
  )
    return request<Row[]>("GET", path);
  const rows: Row[] = [];
  for (let page = 1; ; page++) {
    const data = await request<Row[] | { items: Row[]; total: number }>(
      "GET",
      `${path}${path.includes("?") ? "&" : "?"}page=${page}&size=100`,
    );
    const items = Array.isArray(data) ? data : data.items;
    rows.push(...items);
    if (
      !items.length ||
      (Array.isArray(data) ? items.length < 100 : rows.length >= data.total)
    )
      return rows;
  }
}
export const management = {
  reply: async (
    id: string,
    text: string,
    internal: boolean,
    files: LocalFile[],
  ) => {
    const form = new FormData();
    form.append("text", text);
    form.append("internal", String(internal));
    for (const file of files) await appendFile(form, file);
    return request<{ id: string; pendingFiles: number }>(
      "POST",
      `/staff/requests/${id}/replies`,
      { form },
    );
  },
  identity: () => request<Identity>("GET", "/auth/me"),
  list,
  get: <T>(path: string) => request<T>("GET", path),
  save: (path: string, body: Record<string, unknown>, method = "POST") =>
    request<Row>("" + method, path, { body }),
  remove: (path: string) => request<void>("DELETE", path),
  upload: async (file: LocalFile, tenantId: string) =>
    request<{ id: string; state: string }>(
      "POST",
      `/files?tenantId=${encodeURIComponent(tenantId)}`,
      { form: await fileForm(file) },
    ),
  asset: async (path: string, file: LocalFile) =>
    request<Row>("POST", path, { form: await fileForm(file) }),
};
export const sectionPaths: Record<string, string> = {
  tenants: "/admin/tenants",
  applications: "/admin/registration-applications",
  premises: "/admin/premises",
  leases: "/admin/leases",
  documents: "/admin/documents",
  invoices: "/admin/invoices",
  requests: "/requests",
  meters: "/admin/meters",
  readings: "/admin/readings",
  listings: "/admin/listings",
  news: "/admin/news",
  users: "/admin/users",
  routing: "/request-categories",
  audit: "/admin/audit",
  handoffs: "/admin/integration-handoffs",
};
export const staffSections = [
  "overview",
  "requests",
  "meters",
  "readings",
  "profile",
];
export const roleHome = (role?: Role) =>
  role === "Admin"
    ? "/manage/overview"
    : role === "Staff"
      ? "/staff/overview"
      : "/";
export function canOpen(role: Role | undefined, section: string) {
  return (
    (role === "Admin" &&
      (section in sectionPaths || ["overview", "profile"].includes(section))) ||
    (role === "Staff" && staffSections.includes(section))
  );
}
