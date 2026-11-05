# Validation record

Validated in the managed cloud workspace on 2026-11-04. This records observed checks, not a production readiness certification.

- SDK 10.0.403 installed from official Microsoft release metadata with SHA-512 artifact verification preserved.
- PostgreSQL 17.7 runs through Docker Compose with a loopback port and generated development-only password in ignored `.env`.
- Locked NuGet restore and Release build succeeded with zero compiler warnings/errors.
- `scripts/dev.sh test`: **37 passed, 0 failed, 0 skipped** against real, uniquely named PostgreSQL databases. Test databases were dropped after execution.
- Tests include tenant/staff authorization, internal-comment isolation, registration/approval, immediate account blocking, token logout, private/quarantined files, signatures, meter monotonicity/concurrency, request transitions/stale versions, financial replay/overpayment, repeatable inquiries, news time/audience/acknowledgement, notification deduplication and repeated message/payment notifications.
- Real Kestrel HTTP smoke check on a temporary database passed: migrations; first administrator bootstrap; refusal to bootstrap another administrator; readiness; login; six categories; disabled integration status; logout and revoked-session denial. The temporary process and database were removed; no default administrator was left in the development database.
- The documented setup was executed against an empty database and repeated successfully while preserving `.env` and existing data. Migrations and category seeding are explicit and repeatable.

Current-run test report: ignored `tests/Cube.Tests/TestResults/backend.trx` (37 total). GitHub Actions is configured to perform locked restore, Release build and the same PostgreSQL-backed tests; a remote Actions run has not been observed.

Not executed or enabled: real 1C/Bitrix/payment/email exchange, production malware scanning, production load/security review, production deployment, backup restoration, mobile/frontend clients. Live integrations were explicitly excluded by company instruction. Disabled gateways never claim successful remote delivery or payment.

Internet/telephone support remains a future category. Only the six categories in the written company brief are seeded.

## Administration frontend validation — 2026-11-05

The role-aware administration interface and staff workspace were validated against real ASP.NET Core and PostgreSQL. Release build passed; 40 backend tests passed without skips, including new administrative catalog permissions, responsibility-scoped meter history and assigned-request list projections. TypeScript and 16 frontend tests passed. Five production-browser scenarios passed (three administration/staff/mobile scenarios plus two tenant regressions). Test suites restart their isolated API between suites to avoid sharing production rate counters; assertions and production rate-limit defaults remain intact. Temporary databases were removed. See `docs/admin-interface.md` for supported workflows and backend limitations. No live external-provider or physical-device validation was performed.
