import { test } from "node:test";
import assert from "node:assert/strict";
import {
  emptyFilters,
  filterQueue,
  notificationTarget,
} from "../src/employee/models";
const rows = [
  {
    id: "a",
    status: "New",
    tenantId: "t1",
    premisesId: "p1",
    tenantName: "Альфа",
    subject: "Свет",
    categoryId: "electric",
    dueAt: "2026-11-05T08:00:00Z",
    createdAt: "2026-11-01",
  },
  {
    id: "b",
    status: "Closed",
    tenantId: "t2",
    subject: "Склад",
    dueAt: "2026-11-01T00:00:00Z",
    createdAt: "2026-11-02",
  },
  {
    id: "c",
    status: "Clarification",
    tenantId: "t1",
    subject: "Проверка",
    dueAt: null,
    createdAt: "2026-11-03",
  },
];
test("employee queue combines organization, status, text and unread filters", () => {
  assert.deepEqual(
    filterQueue(
      rows,
      { ...emptyFilters, search: "альфа", view: "unread" },
      new Set(["a"]),
    ).map((r) => r.id),
    ["a"],
  );
  assert.equal(
    filterQueue(rows, { ...emptyFilters, status: "Accepted" }, new Set())
      .length,
    0,
  );
  assert.equal(
    filterQueue(
      rows,
      { ...emptyFilters, tenant: "t1", premise: "p1" },
      new Set(),
    ).length,
    1,
  );
});
test("closed and completed tasks never count as overdue; absent deadline is last", () => {
  const now = new Date("2026-11-05T12:00:00Z");
  assert.deepEqual(
    filterQueue(rows, { ...emptyFilters, view: "overdue" }, new Set(), now).map(
      (r) => r.id,
    ),
    ["a"],
  );
  assert.deepEqual(
    filterQueue(rows, { ...emptyFilters, view: "active" }, new Set(), now).map(
      (r) => r.id,
    ),
    ["a", "c"],
  );
  assert.deepEqual(
    filterQueue(rows, { ...emptyFilters, view: "waiting" }, new Set(), now).map(
      (r) => r.id,
    ),
    ["c"],
  );
});
test("sorts queue without mutating cached data", () => {
  const before = rows.map((r) => r.id);
  assert.deepEqual(
    filterQueue(rows, { ...emptyFilters, sort: "newest" }, new Set()).map(
      (r) => r.id,
    ),
    ["c", "b", "a"],
  );
  assert.deepEqual(
    rows.map((r) => r.id),
    before,
  );
  assert.equal(
    filterQueue(rows, { ...emptyFilters, sort: "unread" }, new Set(["c"]))[0]
      .id,
    "c",
  );
});
test("notification links use only supported internal targets", () => {
  assert.deepEqual(
    notificationTarget({
      id: "n",
      kind: "StaffRequestAssigned",
      resourceId: "r",
    }),
    { section: "requests", id: "r" },
  );
  assert.deepEqual(
    notificationTarget({
      id: "n",
      kind: "StaffReadingSubmitted",
      resourceId: "v",
    }),
    { section: "readings", id: "v" },
  );
  assert.equal(
    notificationTarget({
      id: "n",
      kind: "https://external.test",
      resourceId: "x",
    }),
    null,
  );
  assert.equal(
    notificationTarget({ id: "n", kind: "StaffRequestMessage" }),
    null,
  );
});
