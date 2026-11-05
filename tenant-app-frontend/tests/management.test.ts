import { test } from "node:test";
import assert from "node:assert/strict";
import {
  schemas,
  payload,
  initialValues,
  transitions,
  actions,
} from "../src/management/forms";
import { list, canOpen, roleHome } from "../src/management/api";
test("roles have separate homes and restricted management sections", () => {
  assert.equal(roleHome("Admin"), "/manage/overview");
  assert.equal(roleHome("Staff"), "/manage/overview");
  assert.equal(roleHome("Tenant"), "/");
  for (const s of [
    "tenants",
    "users",
    "documents",
    "invoices",
    "audit",
    "applications",
    "news",
  ])
    assert.equal(canOpen("Staff", s), false);
  assert.equal(canOpen("Tenant", "requests"), false);
  assert.equal(canOpen("Staff", "readings"), true);
  assert.equal(canOpen("Admin", "unknown"), false);
});
test("schemas strip role escalation and validate invoice precision", () => {
  const p = payload(schemas.users, {
    login: "engineer",
    email: "e@example.test",
    password: "Strong-Password123!",
    fullName: "Engineer",
    role: "Admin",
    approved: true,
  });
  assert.equal(p.role, undefined);
  assert.equal(p.approved, undefined);
  for (const amount of [-1, 10.123])
    assert.throws(() =>
      payload(schemas.invoices, {
        ...initialValues(schemas.invoices),
        tenantId: "t",
        number: "1",
        purpose: "Rent",
        amount,
      }),
    );
});
test("news drafts, explicit audiences, schedules and expiry", () => {
  const b = {
    ...initialValues(schemas.news),
    title: "News",
    summary: "Summary",
    body: "Body",
  };
  assert.equal(payload(schemas.news, b).publishAt, null);
  assert.throws(
    () => payload(schemas.news, { ...b, allTenants: false }),
    /получателей/,
  );
  const s = payload(schemas.news, {
    ...b,
    publishAt: "2030-01-01T10:00:00Z",
    expiresAt: "2030-01-02T10:00:00Z",
    allTenants: false,
    tenantIds: ["t"],
  });
  assert.deepEqual(s.tenantIds, ["t"]);
  assert.equal(s.publishAt, "2030-01-01T10:00:00.000Z");
  assert.throws(() =>
    payload(schemas.news, {
      ...b,
      publishAt: "2030-01-02",
      expiresAt: "2030-01-01",
    }),
  );
});
test("approval requires explicit association; status transitions follow backend", () => {
  assert.deepEqual(payload(actions.approve("id"), { existingTenantId: "" }), {
    existingTenantId: null,
  });
  assert.deepEqual(
    transitions("New").map((s) => s.value),
    ["Accepted", "Rejected"],
  );
  assert.deepEqual(transitions("Closed"), []);
});
test("calendar validity and lease date order", () => {
  const b = {
    tenantId: "t",
    premisesId: "p",
    number: "1",
    signedOn: "2026-01-01",
    startsOn: "2026-01-02",
    endsOn: "2026-02-01",
  };
  assert.equal(payload(schemas.leases, b).number, "1");
  assert.throws(() => payload(schemas.leases, { ...b, endsOn: "2026-02-31" }));
  assert.throws(() => payload(schemas.leases, { ...b, endsOn: "2025-01-01" }));
});

test("optional invoice PDF is JSON null, never an invalid empty GUID", () => {
  const data = payload(schemas.invoices, {
    ...initialValues(schemas.invoices),
    tenantId: "tenant",
    number: "I-1",
    purpose: "Rent",
    amount: 100,
  });
  assert.equal(data.fileId, null);
});

test("unpaginated read receipts are fetched exactly once even above 100 records", async () => {
  const previous = globalThis.fetch;
  let calls = 0;
  globalThis.fetch = (async () => {
    calls++;
    return new Response(
      JSON.stringify(
        Array.from({ length: 101 }, (_, i) => ({ id: String(i) })),
      ),
      { headers: { "Content-Type": "application/json" } },
    );
  }) as typeof fetch;
  try {
    assert.equal((await list("/admin/news/n/receipts")).length, 101);
    assert.equal(calls, 1);
  } finally {
    globalThis.fetch = previous;
  }
});
