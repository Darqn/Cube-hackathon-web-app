# Demonstration data

Run from the checkout on Windows after the first launcher setup:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/seed-demo.ps1
```

The helper starts the local database, builds the API, applies migrations, and seeds fictional Russian-language examples. It saves a generated password and the account list in ignored `.data/demo-accounts.json`. All five demo accounts share that local demonstration password. Existing accounts and passwords are preserved; rerunning the seed does not duplicate or reset the dataset. The database remains running for `start-cube.bat`.

| Login | Demonstration |
| --- | --- |
| `demo-admin` | Approve the pending registration; manage premises, leases, invoices, news and staff routing |
| `demo-staff` | 14 assigned requests with status history, public replies and internal notes |
| `demo-tenant` | Studio North: dashboard, outstanding invoices, sample payments, requests, meters and downloadable contract |
| `demo-tenant-2` | Workshop East: separate tenant data to demonstrate isolation |
| `demo-pending` | Registration awaiting approval; cannot log in until approved |

The dataset contains two organizations, five premises, two active leases, eight invoices (paid, overdue, partially paid and unpaid), four fictional payment records, fourteen requests covering all seven statuses, four meters with twelve readings, two downloadable sample PDF contracts, three rental listings, four news items (published, scheduled and draft), and tenant/staff notifications. Dates are relative to the first seed run. Names and identifiers are marked `ДЕМО`/`DEMO`; emails use `example.test`.

Suggested walkthrough: sign in as `demo-tenant`, view balances, download the contract, submit a new reading above 1300, open a request and acknowledge the important news. Switch to `demo-staff` to reply and advance an active request. Switch to `demo-admin` to approve `demo-pending` and publish the draft news. Unassigned category routes are assigned to demo staff; existing routing is preserved, so meter visibility may depend on existing staff assignments.

Payment records are explicitly seeded fixtures (`DemoFixture`), not gateway transactions. Payment, accounting, email and CRM integrations remain unconfigured. The seed runs only in Development with development file access enabled and uses one database transaction. For non-Windows environments, supply `Demo__Password` securely and run `dotnet run --project src/Cube.Api -- --seed-demo` after migrations, with the normal development database/file configuration.
