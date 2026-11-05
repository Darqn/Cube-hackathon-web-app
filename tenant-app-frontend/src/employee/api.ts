import { management, type Row } from "@/management/api";
export type Dashboard = {
  assigned: number;
  active: number;
  new: number;
  waitingForTenant: number;
  overdue: number;
  dueToday: number;
  unreadNotifications: number;
  pendingReadings: number;
  directions: Row[];
};
export const employee = {
  dashboard: () => management.get<Dashboard>("/staff/dashboard"),
  requests: () => management.list("/requests"),
  notifications: () => management.list("/staff/notifications"),
  meters: () => management.list("/staff/meters"),
  readings: () => management.list("/staff/readings"),
  read: (id: string) => management.save(`/staff/notifications/${id}/read`, {}),
  readAll: () => management.save("/staff/notifications/read-all", {}),
};
