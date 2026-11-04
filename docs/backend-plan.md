# Cube backend implementation plan

Status: implementation plan recorded before coding; implementation now authorized.

Company decisions received after initial planning: external integration details are unavailable under company policy. Live 1C, Bitrix and payment integrations are excluded from this delivery; implement disabled extension points and durable pending handoffs instead. Do not claim delivery or settlement. Seed only the six written support categories; leave Internet/telephone support extensible and mention it in the handoff. These decisions supersede provider-dependent release gates below.

Implementation sizing decision: begin with one modular API project containing a hosted outbox worker and one test project; split deployment/projects when independent worker scaling is needed. Preserve domain, persistence, endpoint and adapter boundaries in separate files. Use revocable opaque database-backed bearer sessions rather than introducing signing-key infrastructure; tokens are hashed at rest and checked against current user/tenant state on every request.

## 1. Source and scope

Reviewed baseline: `407c8e94e84f28f16e9e790ed88c3186497f06b9` on `main`.
Requirements: `Техническое задание приложение.docx`, sections 1–23, and all five `Ст.jpg` screen mockups.
The repository otherwise contains no implementation, build instructions, CI, or agent instructions.

Deliver an ASP.NET Core HTTP API and background processing for the tenant application and a separate administrative frontend. Frontend implementation, mobile applications, and production infrastructure provisioning are outside this backend scope. The API must support both web and mobile clients without assuming which frontend will be built first.

The written MVP in section 20 includes authentication, tenant dashboard, leases, invoices and payment, readings, requests with Bitrix delivery, documents, listings, and administrative functions. Section 23 adds mandatory news and alerts; include these in the first backend release. Do not silently defer external integrations and call the MVP complete.

### Differences and unresolved requirements

| Item | Evidence | Proposed treatment |
| --- | --- | --- |
| Request categories | Text specifies six; screen 2 adds Internet/telephone → system administrator | Configurable categories and routing; confirm whether to seed seven |
| Staff permissions | Section 12 has a heading without a permission matrix | Default to assigned requests and explicitly permitted domains; administrators have company-wide access |
| Documents | Screen 3 additionally shows acts, invoices/UPD, other documents, and tenant upload | Include these document kinds; tenants upload statements only, never contracts/invoices |
| Listing favorites | Screen 4 shows favorite icons without written behavior | Optional subsequent feature; confirm before adding to release scope |
| News importance | Section 23 requests levels without defining them | Propose Normal, Important, Urgent; require explicit read acknowledgement separately |
| Password storage | Brief says encrypted passwords | Use salted adaptive password hashing through ASP.NET Core Identity; never reversible encryption |
| Recovery | Email or telephone allowed | Start with email; SMS recovery needs provider selection |
| Payments | Provider, fiscalization, refund behavior, and bank contract unspecified | No invented gateway protocol or client-controlled successful payments |
| Time and money | Deployment timezone/currency unspecified | Store instants in UTC, contract/invoice dates as dates; propose RUB and configurable business timezone |
| Historical access | Access after lease termination unspecified | Preserve organization-owned history; deny new readings on inactive leases; confirm retention/access policy |

## 2. Architecture and toolchain

Use a modular monolith. Deploy one API and one worker, sharing application/domain libraries and a PostgreSQL database. This keeps financial changes transactional and avoids introducing distributed services before they are needed.

Proposed stack: .NET 10 LTS / ASP.NET Core 10, EF Core 10, Npgsql EF provider matching EF's major version, PostgreSQL 17. Select current supported patches during setup, pin the SDK in `global.json`, pin NuGet dependencies, and commit lockfiles. Verify provider compatibility before scaffolding. Do not use floating versions. Use Microsoft.Extensions.TimeProvider.Testing for deterministic scheduling tests where appropriate.

Proposed structure:

```text
src/Cube.Api/                 HTTP endpoints, authentication, authorization, OpenAPI
src/Cube.Application/         Use cases, DTOs, validation, interfaces, authorization rules
src/Cube.Domain/              Entities, invariants, state transitions
src/Cube.Infrastructure/      EF mappings/migrations, storage, provider adapters, outbox
src/Cube.Worker/              Outbox delivery, scheduled notifications, reconciliation
tests/Cube.UnitTests/
tests/Cube.IntegrationTests/  Real PostgreSQL, HTTP host, storage and provider simulators
tests/Cube.ContractTests/     Provider contracts and signed callback fixtures
docs/                        Contracts, decisions, setup and operations
```

Avoid a generic repository over EF Core and unnecessary service layers. Use explicit application handlers with narrow capabilities and database transactions around invariants. Keep HTTP DTOs separate from persistence entities to prevent overposting and accidental disclosure.

Development prerequisites observed: x86_64 Debian 13, Docker executable available; .NET SDK and psql are absent from PATH. Docker daemon availability is not yet validated. Before setup choose native local dependencies or verified container support; do not assume a Docker executable means Testcontainers will work.

## 3. Identity and authorization

Model the tenant as an organization, not a user. A user can have membership in an organization; every organization-owned record carries a required `TenantId`. Start with one active organization context per session, validated against membership. Staff scopes are separate from tenant membership.

Use ASP.NET Core Identity for password policy, hashes, lockout, reset tokens and security stamps. Registration creates a pending application (name, email, organization, INN); it never grants access. Validate INN length/checksum for legal entities or individual entrepreneurs. Admin approval must associate an existing organization explicitly or create one; matching an INN alone must never grant access to existing tenant data. Send an invitation after approval. Disallow public role assignment and shared default administrator passwords.

Expose login, logout, session refresh, current profile, change-password, forgot/reset-password, registration application and administrative approval/rejection. Use short-lived bearer access tokens with fixed issuer/audience/signature validation and rotating opaque refresh tokens stored hashed in the database. Reuse detection revokes the refresh family. Blocking a user/organization or changing permissions must invalidate access promptly, including already-issued access tokens through security-version checks. For browser clients, put refresh credentials in Secure HttpOnly cookies with CSRF protection; choose a documented mobile token transport separately. Never store browser tokens in local storage as a prescribed integration pattern.

Require policies for tenant reads/writes, staff assignment, domain capabilities, and administration. Determine tenant identity from authenticated server context, never a tenant-supplied request body. Admin cross-tenant endpoints are distinct and audited. EF query filters provide defense in depth; explicit ownership checks and tenant-consistent foreign keys remain necessary. Ensure queries that disable filters are confined to reviewed administrative/integration paths.

Default-deny staff access to billing, tenant documents and other staff conversations unless their capability permits it. Internal comments have a dedicated representation and query path; tenant response DTOs never contain them. Return a consistent not-found result for inaccessible resource identifiers to avoid revealing other tenants' records.

## 4. Domain and persistence design

Use UUID primary identifiers, UTC timestamps, stable string enums in HTTP contracts, optimistic concurrency versions on mutable aggregates, and database constraints as well as request validation. Use `numeric`/C# decimal for money, area and readings; never binary floating point. Separate identity/contact PII from operational logs.

| Module | Main entities | Critical rules |
| --- | --- | --- |
| Access | User, TenantOrganization, Membership, StaffCapability, RegistrationApplication, RefreshSession | Unique normalized login/email; disabled users cannot authenticate; approval and invitation audited |
| Property | Building, BuildingSection, Premises, Lease, LeasePremises | Premises type, address, floor, area, price and availability; contract number, signed/start/end dates and status; support multiple premises per lease |
| Billing | Invoice, PaymentAttempt, PaymentEvent, PaymentAllocation, ExternalReference | Invoice external identity unique per 1C source; currency consistent; immutable provider references; posted payment events and allocations cannot be counted twice |
| Metering | Meter, MeterInstallation, MeterReading | Meter type/serial/unit, effective installation and lease context; baseline and monotonic accepted readings; explicit correction/replacement path |
| Requests | RequestCategory, RoutingRule, ServiceRequest, RequestMessage, RequestStatusEvent, Assignment | Private conversation scoped by tenant and staff assignment; all status/assignment changes retained |
| Files | StoredFile, Attachment, TenantDocument, DocumentVersion, StatementReview, DocumentTemplate | Private storage; owner/resource authorization; document links to tenant-consistent lease and premises; archive without exposing old files |
| Listings | Listing, ListingPhoto, ListingInquiry | Publish/hide/archive separately from premises status; inquiries create rental requests, not automatic reservations |
| News | NewsItem, NewsAudience, NewsRecipient, NewsReadReceipt, NewsAcknowledgement | Scheduled publication, expiry, tenant/building/section/floor/premises/group targeting, archive and receipt tracking |
| Notifications | UserNotification, NotificationPreference, DeliveryAttempt | In-app baseline; configurable email/push delivery; deduplicate reminders and delivery retries |
| Reliability | OutboxMessage, InboxReceipt, IntegrationMapping, AuditEvent | Transactional enqueue, deduplication, bounded retries, operator-visible failures and append-only audit |

Add composite tenant/resource constraints where a tenant-owned entity references another tenant-owned entity. Create indexes for tenant/date and tenant/status listing queries, request assignee/status, invoice external IDs, meter/time, news publication/expiry, outbox due time and provider event IDs.

Use migrations from the first schema version. Development can run explicit migration commands; production uses a reviewed migration deployment step rather than uncontrolled API startup migration. Test migrations on an empty database and upgrade scenarios. Seed static dictionaries idempotently; bootstrap the initial admin using a one-time operational command with secure inputs.

### State and transaction rules

- Lease: proposed/draft → active → expired or terminated. Preserve history; prevent incompatible overlapping active exclusive leases with a concurrency-safe database constraint/transaction. Confirm whether shared premises leasing exists before enforcing exclusivity universally.
- Request: new → accepted → in-progress → clarification/completed/rejected; clarification → in-progress; completed → closed or in-progress; closed/rejected reopening requires an authorized, audited action. Reject unsupported transitions with 409. Capture actor, timestamp and reason. External status mapping must not overwrite newer local events silently.
- Reading: submitted → accepted/rejected; tenant input is tied to an active meter/lease at submission time. Serialize updates per meter or use concurrency checks so simultaneous submissions cannot invalidate the baseline. Replacements/resets/corrections require authorized explicit records, never silently decreasing values.
- Invoice: payment state is unpaid/partial/paid from reconciled amounts; overdue is a derived condition on the outstanding balance and due date. Retain the raw 1C status separately; agree reconciliation precedence with accounting before implementing synchronization. Show an effective overdue status without losing partial-payment information.
- Payment: created → pending → succeeded/failed/cancelled; callbacks may arrive duplicated or out of order. Success is accepted only after verified provider evidence. A browser redirect never marks an invoice paid. Lock the invoice/payment attempt during allocation, reject wrong currencies and amounts, and make event processing atomic. Refunds/reversals are separate ledger events. Confirm partial payments, overpayment handling, expiry and refund scope with accounting.
- News: draft → scheduled/published → archived. Visibility requires publication time reached, expiry not reached and recipient eligibility. Background job delays must not expose expired or future news. Distinguish opening, marking read and explicitly acknowledging an alert.

## 5. HTTP API contract

Version endpoints under `/api/v1`. Publish OpenAPI including auth requirements, validation, examples and error responses. Return RFC 9457 Problem Details with stable application error codes and trace ID. Use bounded pagination, allowlisted sorting and validated filters on all collections. Treat resource IDs as opaque. Use `Idempotency-Key` on retry-sensitive creation operations and reject reuse with a different payload. Bind keys to actor, route and payload hash; persist original result atomically.

| Area | Tenant endpoints / behavior | Staff and admin endpoints / behavior |
| --- | --- | --- |
| Identity | `/auth/registration-applications`, `/auth/login`, `/auth/refresh`, `/auth/logout`, `/auth/forgot-password`, `/auth/reset-password`, `/me`, `/me/password`, `/me/notification-preferences` | Registration approval/rejection, tenant create/update/block/unblock, membership and staff permissions |
| Dashboard | `/dashboard`: active leases/premises, unpaid amounts, overdue balance, upcoming payment, urgent alerts and unread counts | Tenant-specific administrative view with explicit permission |
| Premises | `/me/premises`, `/me/leases`, detail | Buildings/sections/premises CRUD, lease lifecycle and tenant assignment |
| Invoices | `/invoices`, detail, `/invoices/{id}/file`, `/invoices/{id}/payment-attempts`, `/payments` | Invoice search, synchronization status, payment reconciliation; no arbitrary tenant edit of imported amounts |
| Meters | `/meters`, `/meters/{id}/readings`, create reading and read status/history | Meter installation/baselines, review/correction with reason, tenant/premises/date filters |
| Requests | `/request-categories`, `/requests`, detail, messages and attachments | Assignment, transition, deadline, internal comments, full history and filters |
| Documents | `/documents`, detail, authorized file download, statements upload/review history, templates | Document upload/version/archive, tenant/lease/premises assignment and statement review |
| Listings | `/listings`, filters/detail, `/listings/{id}/inquiries`, own inquiries | Listing CRUD/publication/photos, availability and rental inquiry routing |
| News | `/news`, detail, read/read-all, acknowledge, allowed archive | Draft/edit/schedule/publish/archive, targeting, delivery/read/ack reports |
| Notifications | `/notifications`, unread counts, read/read-all | Delivery failure visibility and authorized retries |
| Operations | No tenant access | Audit search, integration health, dead-letter inspection/replay, routing configuration |

Include all fields specified in the document (contract dates, invoice purpose/due date, request contacts, meter previous value/photo, document type/version, listing utility conditions, news summary/priority/expiry). Use stable category codes with Russian display names; do not couple enum behavior to translated text. Scope administrative list endpoints by explicit tenant/property filters and permissions.

Webhook endpoints live under `/api/v1/integrations/{provider}` and use dedicated provider authentication, payload validation and replay controls; they are not tenant bearer endpoints. Publish concrete routes and schemas only once the provider contract is known.

File operations are two steps: authorized upload creates a pending object; validation/scanning promotes it to usable; an authorized use case attaches it to a resource. Do not permit arbitrary object IDs to attach another tenant's upload. Ensure thumbnails/previews inherit authorization. Large downloads stream with cancellation rather than buffering the entire object in memory.

## 6. Files, privacy and auditing

Accept only PDF, DOC/DOCX, XLS/XLSX, JPEG and PNG with a proposed 20 MiB per-file limit from section 16. Also bound request count and total upload size. Validate extension, declared type and file signatures/container structure; normalize filenames, generate random storage keys, reject traversal and malformed archives, quarantine and scan before download. Do not serve uploads from a public web root. Serve office/PDF files as attachments with nosniff; safely generated image previews may be inline. Select production scanner/storage services before deploying upload functionality.

Use a private local-volume storage adapter for development and a private object-storage adapter for production. Store metadata and authorization links in PostgreSQL. Never expose raw storage keys or permanent public URLs. Coordinate metadata/file failure recovery with pending-object cleanup, retention policy and backups. News attachments are protected by recipient eligibility too; shared templates require a separate authenticated-access policy.

Audit administrator and sensitive staff actions, login/security events, approval, permission changes, exports, financial reconciliation and integration replay. Record actor, target, action, time, result and safe changed-field summaries. Never log passwords, tokens, reset URLs, provider secrets, file bodies or unrestricted financial/PII payloads. Define retention, deletion/export, deployment jurisdiction and audit retention with the company before production.

## 7. External integrations and background processing

Use provider-specific adapters behind application interfaces, not fabricated universal APIs. Independent local work can proceed with explicit contract simulators; simulators must be restricted to development/test and must never report real delivery/payment success in production.

### 1C

Confirm configuration/version, API or exchange format, authentication, tenant identifiers, invoice/PDF schema, pagination/change cursor, update/delete semantics, currency, payment reconciliation ownership and synchronization frequency. Keep external IDs separate from local IDs. Import idempotently; persist cursor only after the batch commits. Do not silently create tenant access based on an external contact match. Export verified payments via outbox and reconcile delivery versus accounting acceptance. Unknown tenant mappings go into a review queue.

### Bitrix

Confirm cloud versus self-hosted edition, task/CRM/smart-process entity, authorization, custom fields, category-to-user IDs, attachments mechanism, rate limits and inbound message/status capability. Persist request ID ↔ Bitrix ID mappings. Route all six written categories, readings to the chief energy engineer, listing inquiries to rental management, and Internet/telephone if confirmed. Specify whether Bitrix is the primary staff workspace and implement two-way synchronization if so; outgoing task creation alone does not satisfy conversation/status synchronization.

### Payments

Obtain provider and merchant sandbox documentation, callback verification method, provider idempotency rules, permitted return URLs, payment methods, receipts/fiscalization responsibilities and refund policy. Keep card details outside this backend. The backend creates checkout for the authorized invoice balance, obtains the provider URL, and validates webhook signature/replay/amount/currency/reference before recording payment. Deduplicate callback event IDs and transaction IDs across restarts. Reconcile pending transactions with the provider to recover lost callbacks. Never trust client-provided settlement state.

### Notifications and scheduling

Implement in-app notifications first; email is required for recovery/invitations and needs a configured sender. Provider failures remain visible and retryable. Push requires mobile client/APNs/FCM decisions; SMS and telephony are optional unless separately selected. Schedule upcoming/overdue invoice reminders, meter reminders, news publication/expiry, outbox delivery and integration reconciliation using database-backed jobs/leases. Multiple worker instances must not duplicate sends. Select reminder timing and business timezone with the company.

### Delivery reliability

Persist business changes and outbox messages in the same database transaction. Workers claim batches using database locks/leases, retry transient failures with bounded exponential backoff and jitter, honor Retry-After, and dead-letter exhausted items. Treat delivery as at-least-once, never promise exactly-once remote side effects. Use provider idempotency or reconcile by external reference after an ambiguous timeout; do not blindly repeat create operations. Inbox processing deduplicates and commits side effects atomically before acknowledging callbacks. Log correlation IDs and safe failure summaries; give administrators controlled replay operations.

## 8. Security and operations

- Enforce HTTPS in deployment, explicit trusted reverse-proxy configuration, strict CORS allowlist, body/upload limits, validation and rate limits on authentication/reset/upload/payment operations. A browser API cookie flow additionally needs CSRF controls.
- Keep secrets outside source control in deployment secret storage; development examples contain placeholders only. Use least-privilege runtime and migration database accounts. Persist ASP.NET Data Protection keys securely across restarts/replicas; define signing-key rotation and revocation.
- Provide liveness and readiness separately. Readiness tests database access and required local dependencies without claiming third-party payment health. Provider outages should queue delivery rather than prevent tenant read access. Restrict diagnostic endpoints and sanitize errors.
- Structured logs, trace IDs, metrics for latency/error rates, outbox age/failures, callback verification errors, sync lag and job backlog. Alert on stalled integrations and repeated payment discrepancies.
- Automated encrypted PostgreSQL and file backups with aligned retention; verify a restore, not just backup command success. Define RPO/RTO, ownership, geography and retention with operations. Include migration rollout/rollback and provider outage recovery runbooks.
- Production release requires security review, representative load/concurrency tests, dependency scanning, tenant-isolation tests, restored backups and completed sandbox integration checks. Do not claim absence of defects from a passing build.

## 9. Implementation sequence and exit criteria

Each stage includes implementation, meaningful tests, OpenAPI updates and relevant setup/runbook updates. Do not label adapters awaiting contracts as delivered integrations.

| Stage | Work | Exit evidence |
| --- | --- | --- |
| 0. Requirements/design | This plan, conflict register, provider contracts, permission/source-of-truth decisions | Required decisions recorded; independent core scope separated from provider-dependent work |
| 1. Foundation | Pinned SDK/packages, solution, PostgreSQL, initial migrations, validation, errors, health, CI | Clean restore/build; migrations apply on empty DB; real DB integration suite executes; startup readiness verified |
| 2. Access/property | Identity, registration approval, memberships, tenant isolation, staff scopes, admin bootstrap, premises/leases, audit | Tenant A cannot read/write tenant B; blocking/revocation works; no public privilege escalation; lease concurrency verified |
| 3. Requests/metering | Routing, messages/internal comments, histories, meter installation/readings, outbox | Assigned staff boundaries and internal-comment privacy verified; concurrent readings tested; durable events survive restart |
| 4. Documents/listings | Protected storage pipeline, documents/versions/templates/statements, listing filters/photos/inquiries | Cross-tenant files denied; file validation/scanning enforced; inquiry creates one routed request under retry |
| 5. Billing/payments | 1C mappings/import, invoice views, ledger/payment attempts, chosen gateway, reconciliation/export | Repeated imports/callbacks do not duplicate money; signature/amount mismatch rejected; lost callback recovery and sandbox checkout verified |
| 6. Bitrix | Concrete outbound/inbound adapters and employee routing | Sandbox request/readings/inquiry delivered to correct employee; messages/statuses synchronize; timeout/retry does not duplicate remote requests |
| 7. News/notifications | Targeting, scheduling, expiry, archive, read/ack, recovery/invitation email, dashboard | Audience isolation and scheduling tested with controlled clock; old/future news hidden; read does not imply acknowledged; emails verified in test mailbox |
| 8. Release | Full acceptance suite, OpenAPI examples, operations/security review, restore/load tests | Reproducible setup and CI; end-to-end journeys pass against PostgreSQL; external acceptance and remaining limitations documented |

Stages 5 and 6 depend on external contracts; other core stages can proceed without live credentials. Provider delivery acceptance cannot proceed without sandbox access. Build core services before adding optional favorites, SMS, electronic signature, telephony, document generation or native push. Two-way request conversation, financial correctness and tenant isolation are required scope.

## 10. Verification strategy

Unit tests cover state transitions, INN validation, permissions, recipient predicates, date boundaries, decimal math and meter invariants. Integration tests run migrations and the actual Npgsql provider against PostgreSQL; EF InMemory or SQLite cannot validate PostgreSQL constraints, transaction behavior or concurrency.

Required HTTP/database scenarios:

1. Pending applicant cannot log in; admin approval/invitation enables only the intended membership. Wrong credentials, lockout, reset expiry, refresh rotation/reuse and account blocking behave correctly.
2. Tenant A cannot enumerate, fetch, mutate, attach or download tenant B's leases, invoices, documents, readings, requests, messages, files or targeted news, including guessed IDs and nested child endpoints. Staff sees only authorized scopes. Internal comments never leak through detail, lists, notifications or integration payloads intended for tenants.
3. Admin creates tenant/premises/lease; tenant sees correct contract fields and cannot alter imported financial values. Concurrent lease assignments cannot bypass exclusivity rules.
4. Repeated/out-of-order 1C batches converge; unknown tenant mappings are quarantined; partial payment, due date and overdue calculations use controlled dates and correct currency precision.
5. Duplicate/concurrent signed payment callbacks allocate once; forged/mismatched callbacks fail; browser return cannot settle an invoice; a provider timeout after checkout creation recovers without double charge. Export retry cannot lose or duplicate accounting notification.
6. Concurrent readings validate against the correct prior value; unauthorized/inactive lease, decrease, duplicate submission, meter replacement and authorized correction are covered.
7. Each request category routes correctly; assignment/status history persists; staff/tenant messages synchronize; unauthorized transitions fail; retries/restarts and Bitrix rate limits preserve history without loops.
8. Upload size/type/spoofed signature/traversal/foreign attachment tests fail safely; pending/quarantined files cannot download; private previews and archived versions remain authorized; storage failure cleanup is exercised.
9. Listing filters/pagination are stable; hidden/unavailable listings follow the defined policy; repeated inquiry creates one rental request and never a lease or reservation automatically.
10. News tests cover all targeting dimensions, multiple lease memberships, recipient changes, publication/expiry boundaries, archive rules, urgency ordering, read-all versus acknowledgement and audience report authorization. Define whether published audiences are snapshotted: propose snapshot recipients at publication to preserve delivery/read accounting, with explicit updates for later audience changes.
11. Outbox workers recover after crash/lease expiry, handle poison messages and retry-after, avoid unbounded retry and expose dead letters. Test ambiguous remote success separately from explicit rejection.
12. Restore database plus private files to an isolated instance and exercise authenticated document download and billing history. Verify migrations and application startup using the documented commands.

CI gates: locked dependency restore, Release build with compiler/analyzer checks, unit tests, PostgreSQL integration tests and provider contract simulators. Run actual provider sandbox checks separately when access exists; report skipped/unrun checks explicitly. Produce current-run test reports and preserve command exit statuses. Determine load targets from anticipated tenants/concurrency/document sizes before defining performance acceptance thresholds.

## 11. Decisions needed from the company

The first three items block concrete external adapters, not independent implementation of tenant/property/request/document APIs:

1. 1C version/configuration, exchange/API documentation, source identifiers and reconciliation owner.
2. Bitrix edition, destination entity/API, employee/category mapping and whether staff work in Bitrix or the administrative interface.
3. Payment provider/acquirer, sandbox contract, fiscalization responsibility, partial payment/refund behavior.
4. Seventh support category, exact staff permission boundaries and confirmation that a tenant organization may have multiple users.
5. Email sender/provider and recovery channel; mobile push scope and devices if applicable. Supply credentials through environment settings, never chat or repository files.
6. Production hosting/private storage/scanner, business timezone, currency, retention/jurisdiction and backup RPO/RTO.
7. Historical data after termination, shared/exclusive premises leasing, document version retention and news audience changes after publication.

Record answers as design decisions before implementing the affected behavior. Do not create fake integration successes or choose a bank, employee ID, retention period or legal policy on the company's behalf.

## 12. Development handoff

Use the existing checkout `/workspace/Cube`; this cloud task is already isolated and does not need a Git worktree. Once implementation begins, save tested install/start instructions with exact working directories, tool versions, database initialization and readiness checks. Future tasks must restart processes explicitly rather than assume they survive a snapshot. Do not record unexecuted setup commands as tested.

Completion means the agreed backend scope is implemented, documented and validated, including actual sandbox external integration flows; a scaffold, passing compile or simulator-only payment test is not equivalent to a production-ready backend.

## 13. Delivery record

See [README](../README.md), [validation evidence](validation.md) and [integration handoff](integrations.md) for the implemented scope and verified commands. The initial plan above is retained as design history; its proposed architecture and release targets are not a statement that every proposed capability shipped.

Implemented sizing decisions: one modular API project, an optional in-process in-app notification worker, one test project, revocable eight-hour bearer sessions without refresh, explicit authorization queries and tenant-consistent child foreign keys. Leases contain one premises per record. News uses live audience eligibility rather than frozen recipients. No provider-delivery worker is active. Current storage is private local disk with quarantine enforced outside explicit development opt-in. Optional frontend/native clients, favorites, SMS/push, contact lists, document-version comparison and refunds remain outside this baseline. Live providers are excluded as authorized.

Backend implementation and 37 passing tests are complete for this documented baseline. Production acceptance remains dependent on the explicitly documented operational prerequisites.
