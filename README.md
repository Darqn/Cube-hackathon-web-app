# Cube backend

ASP.NET Core 10, EF Core/Npgsql and PostgreSQL 17 backend for the commercial-property tenant application. The original Russian brief and five mockups remain in the repository. The [implementation plan](docs/backend-plan.md) was written before application code.

## Run locally

### Windows

Run `start-cube.bat` from the repository root (or pass the checkout directory as its first argument). The launcher checks Node.js/npm, the pinned .NET SDK, Docker Desktop/Compose and project dependencies. Every installation or dependency download requires console confirmation; declining stops startup. Docker Desktop requires Linux containers and may need WSL2 setup and a reboot after its first installation.

The launcher preserves an existing `.env`, starts PostgreSQL, builds the backend, applies migrations and starts API/Expo in separate windows. Stop both servers with Ctrl+C in their windows, then run `docker compose stop` from the repository root to stop PostgreSQL while retaining its data. No administrator is created automatically. On the first run or after dependency changes, `npm ci` recreates `node_modules` using the lockfile and available cache. The Windows launcher has not yet been executed on Windows CI.

Prerequisites: the SDK pinned in `global.json`, Python 3, Docker Engine and Docker Compose. Development data uses a named PostgreSQL volume; uploaded files use ignored `.data/files`. Nothing creates a default administrator or seeds real tenant data.

```bash
scripts/dev.sh setup
# Supply Bootstrap__Login, Bootstrap__Email and Bootstrap__Password securely in your environment.
scripts/dev.sh bootstrap
scripts/dev.sh run
```

`setup` generates an ignored, private `.env` if absent, starts PostgreSQL, restores locked dependencies, builds Release, applies migrations and idempotently seeds the six request categories. Existing `.env` files are preserved. The bootstrap command refuses to run if an administrator exists. Passwords require at least 12 characters plus upper/lowercase letters, a digit and a symbol. Unset bootstrap variables after use.

The API binds to loopback port 5080 in development. Readiness: `GET /health/ready`; development OpenAPI: `GET /openapi/v1.json`. Use that document for complete HTTP routes and body schemas. No Swagger UI dependency is required.

```bash
scripts/dev.sh test
```

Tests create and drop uniquely named PostgreSQL databases. Use an isolated development/CI database role with `CREATEDB`; never point `CUBE_TEST_DATABASE` at production. Tests apply actual migrations and use the real Npgsql provider. Unit tests and HTTP/database tests are in `tests/Cube.Tests`.

## Authentication and first workflow

1. Bootstrap the administrator, then `POST /api/v1/auth/login` with `loginName` and `password`. Send the returned access token in `Authorization: Bearer ...`.
2. A tenant submits `POST /api/v1/auth/register` with login/password, email, fullName, organization, INN and phone. Access remains pending.
3. The admin lists `/api/v1/admin/registration-applications` and approves an application. Existing organizations require an explicit matching `existingTenantId`; an INN match alone never grants access. Use `/admin/users/{id}/blocked` to reject/block an application or user.
4. The admin creates premises and a lease, uploads documents, creates meters and assigns request-category routing to a staff account. Tenants can then use the portal APIs.

Tokens are opaque random values, hashed in PostgreSQL, expire after eight hours and are revocable on logout, password change/reset, user block and tenant block. Login again after expiry; refresh-token rotation is not implemented. Keep browser bearer tokens in memory or use a trusted backend-for-frontend; do not put them in URLs. Staff can access assigned requests; broad billing, document and meter administration requires Admin.

## Included workflows

- Registration approval, login/logout, password changes, recovery/reset endpoints, user profile/preferences, account blocking and staff creation.
- Tenant organizations, premises, exclusive active leases, contract information and tenant dashboard with outstanding/overdue totals.
- Six request categories, configurable routing, assigned staff access, public conversation, private staff comments, state transitions with optimistic concurrency, attachment authorization and status history.
- Water/electricity meters, monotonic submission watermark, photo references, review history and explicit administrative baseline corrections.
- Manual administrative invoice entry, invoice/PDF views, financial balances, payment history and a transaction-safe settlement service reserved for future verified provider callbacks.
- Private document upload/download, tenant statements, templates, document kinds, archive/review and tenant-consistent links.
- Filtered available/reserved listings, protected photos and repeatable rental inquiries that create requests without reserving premises.
- News drafts/scheduling/expiry/archive, location or tenant targeting, importance, protected attachments, independent read/ack receipts.
- In-app notifications and optional hosted reminder scheduler; audit events; health checks; OpenAPI; migrations; CI configuration.

News recipients are evaluated against current active leases when reading. This intentionally avoids exposing location alerts after a tenant leaves. Scheduled in-app news/reminders run once a minute when `Notifications__WorkerEnabled=true`; duplicate workers use database uniqueness/upserts. Reminder defaults are three days before invoice due date, overdue once, and monthly meter reminders from UTC day 25. These are development defaults to confirm with operations. Scheduling does not control access: future/expired news is filtered directly in queries.

## Deliberately disabled integrations

Company instruction excludes live 1C, Bitrix and payment integrations because their contracts are confidential/unavailable. Interfaces and pending outbox handoffs are in [Integrations.cs](src/Cube.Api/Integrations.cs); extension instructions are in [docs/integrations.md](docs/integrations.md).

Payment checkout returns **503 `payment_provider_not_configured`**. No client endpoint can mark a payment successful. Requests, readings and statements remain stored locally with `AwaitingConfiguration` handoffs. There is no automatic remote-delivery worker, fake checkout URL or fabricated successful synchronization. Email is also unconfigured: password recovery queues an intent, but does not deliver a message. Administrators can inspect `/api/v1/admin/integrations` and `/api/v1/admin/integration-handoffs`.

The written brief takes precedence: **Internet/telephone support is not seeded**. Its seventh category appears only in mockup 2. The category/routing model can accommodate it later without changing request contracts.

## Production prerequisites and limits

This is a tested backend baseline, not a claim of production certification. The administration web interface and staff workspace are in `tenant-app-frontend`; native mobile release testing remains outside this baseline. Optional favorites, native push/SMS, electronic signatures, telephony, tenant-controlled contact lists, document version comparison and refunds are not implemented. Leases currently cover one premises per record and assume exclusive occupancy; multi-premises contracts can share a contract number, but there is no contract aggregate. Tenant notification history is separate per user.

Production uploads remain **quarantined** until a scanner/storage workflow is connected. Only Development with explicit `Files__AllowUnscannedDevelopmentUploads=true` permits structurally validated files without malware scanning. Extension/signature checks are not malware detection. Do not enable development mode in production. In a future scanner adapter, promote verified file metadata to Ready only after a clean scan; no HTTP endpoint permits bypassing quarantine.

Before deployment configure TLS termination/trusted proxy handling, CORS origins, database least privilege, durable private file storage, shared encrypted ASP.NET Data Protection keys, recovery email and secrets. Configure backup/restore, monitoring, business timezone/retention and load targets. The API deliberately ignores forwarded headers unless a deployment-specific trusted proxy setup is added. HTTP rate limits use the direct peer IP and are process-local; multi-instance production needs an edge/distributed limiter. OpenAPI is exposed only in Development/Testing.

Review migrations before production use. Explicitly run `dotnet run --project src/Cube.Api --configuration Release -- --migrate` during deployment; startup does not auto-migrate. Keep the migration role separate from runtime. Test backups of PostgreSQL **and** file storage together in an isolated restoration environment before production. A restore drill, production load test and real-provider acceptance have not been performed by this task.

## Configuration

| Variable | Purpose |
| --- | --- |
| `ConnectionStrings__Cube` | Required Npgsql connection string; supply securely |
| `ASPNETCORE_ENVIRONMENT` | Development locally, Production on deployment |
| `ASPNETCORE_URLS` | Bind addresses; local helper uses loopback |
| `Files__Root` | Durable private storage directory outside any static web root |
| `Files__AllowUnscannedDevelopmentUploads` | Explicit Development-only opt-in |
| `Notifications__WorkerEnabled` | Start local in-app scheduler; default false |
| `Cors__Origins__0` (and further indices) | Explicit allowed client origins |
| `Bootstrap__Login`, `Bootstrap__Email`, `Bootstrap__Password` | One-time administrator creation inputs |
| `CUBE_TEST_DATABASE` | Isolated test server connection, with database creation rights |

No secrets belong in source, logs, committed settings or chat. `.env` is development-only and ignored. Its sourced contents must be trusted local configuration.

## Веб-кабинет арендатора

Фронтенд находится в `tenant-app-frontend`. [Инструкции запуска и описание изменений на русском](tenant-app-frontend/README.md). Для API с разрешёнными локальными веб-источниками используйте `bash scripts/dev-web.sh`.

[Панель администратора и рабочий кабинет сотрудника: вход, возможности и ограничения](docs/admin-interface.md).

[Отдельный кабинет сотрудника: вход, очередь задач, уведомления, файлы и показания](docs/employee-interface.md).
