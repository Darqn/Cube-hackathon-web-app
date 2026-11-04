# Future integration adapters

Live integrations are excluded by company instruction. Current status is always NotConfigured; pending business events are durable but never marked delivered. Implement and validate real protocols when their contracts become available.

## Contracts and ownership

- `IBitrixGateway`: delivery of requests, messages, status events, attachments, statements and readings.
- `IAccountingGateway`: delivery of verified payment events. Add an invoice-source interface returning paged external invoice snapshots and a continuation cursor when the 1C exchange format is known; do not assume a universal REST API.
- `IPaymentGateway`: authorized invoice checkout creation. The current adapter throws 503. Keep card data outside Cube.
- `IEmailGateway`: approval and recovery delivery. Recovery events carry only a user ID; generate an expiring Identity password-reset token immediately before sending, never persist/log raw tokens.

`Outbox` rows identify the provider, event kind and local resource ID. Resolve payloads from the authoritative database under the provider's service scope. `RequestMessage.Internal` content must never enter tenant-visible notifications or outbound tenant messages. Request status events are immutable facts; avoid reconstructing an old event from the current request status.

## Implementation checklist

1. Add provider configuration/secret bindings and replace the specific interface registration. Require configured adapters explicitly in production; never turn a disabled result into success.
2. Introduce unique provider ↔ local mappings and inbox event receipts. Specify source-of-truth ownership, external status mapping, ordering and reconciliation rules in tests before accepting callbacks.
3. Add a worker that atomically claims outbox batches with leases, retries with bounded backoff/jitter, respects rate limits, and dead-letters failures. Current rows remain `AwaitingConfiguration` until the operator explicitly enables the provider. Do not silently replay old data without reviewing its relevance.
4. Deduplicate by stable external reference. At-least-once transport can yield ambiguous remote success: use provider idempotency or query/reconcile before retrying creation. Persist acknowledgement/mapping atomically with local delivery state where possible.
5. Authenticate callbacks using the documented provider mechanism; validate raw-body signatures, replay rules, currency, amount and local reference. No tenant bearer endpoint may call settlement. `PaymentLedger.ApplyVerifiedAsync` serializes invoice allocation and deduplicates transaction references, but does not verify provider authenticity itself.
6. A future checkout adapter must persist attempts/idempotency keys before calling the provider, recover ambiguous create timeouts and allow only configured return URLs. Implement reconciliation for callbacks lost during downtime. Refund/reversal handling needs separate ledger entries and agreed accounting rules before exposing it.
7. Import 1C invoices idempotently by source/external ID; map tenants through explicit trusted mappings, not contact emails or user-provided INN alone. Do not overwrite locally settled balances with stale source snapshots. Confirm payment authority and reconcile mismatches.
8. If employees work in Bitrix, implement inbound conversation, assignment and status synchronization as well as outbound creation. Prevent echo loops and prohibit tenant-visible internal comments.
9. Add sandbox/contract tests with real signed fixtures, duplicate/out-of-order delivery, timeouts, restart recovery, invalid tenant references and financial mismatches. Only then claim the provider is working.

The six written categories are seeded by stable codes. A future Internet/telephone category can be added idempotently with a stable code (for example `internet-telephone`), Russian display name, system-administrator department and explicit responsible-user mapping. Do not add it automatically without a requirements decision.
