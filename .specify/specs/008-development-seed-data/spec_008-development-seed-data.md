# Feature Specification: Development Seed Data for Vacation Requests

**Feature Branch**: `prometheus-implementation` *(continued; no new branch created)*
**Created**: 2026-09-23
**Status**: Implemented
**Constitution Reference**: NovaLeave — Constitution v4.0.0
**Depends on**: `005-docker-containerization`, `007-prometheus-observability`

---

## Overview

`DevelopmentDataSeeder` created 5 employees, 5 users and 2 roles, and **zero vacation requests**. A freshly reset database therefore had no approver queue, no request history, and nothing for the spec_007 business dashboards to display.

This feature seeds **13 vacation requests across all six statuses**, with balances that reconcile and creation dates that are backdated so queue-age signals are meaningful.

**Out of scope**: seeding audit records, production data of any kind, and fixing the balance defect found during implementation (recorded, not fixed).

---

## Requirements

- **FR-001**: At least 10 vacation requests MUST be seeded in `Development`.
- **FR-002**: Every `RequestStatus` value (Pending, Approved, Rejected, Cancelled, Voided, Expired) MUST be represented.
- **FR-003**: Requests MUST be created through the domain's own constructor and state-transition methods, so no seeded row can violate an invariant.
- **FR-004**: `RequestedDays` MUST equal the working days in the request's date range, computed with the application's own `IWorkingDayCalculator`.
- **FR-005**: Employee balances MUST reconcile: `Balance + days held by Pending/Approved requests = initial balance`.
- **FR-006**: No employee balance may be negative at any point during seeding.
- **FR-007**: `CreatedAt` MUST be backdated so the oldest pending request has a non-trivial age.
- **FR-008**: Seeding MUST be idempotent — a restart must not create duplicates or drain balances.
- **FR-009**: Seeding MUST remain `Development`-only, inheriting the existing `DatabaseInitializer` guards.
- **FR-010**: The inactive employee MUST NOT receive requests, because `ReserveDays` rejects inactive accounts by design.

## Success Criteria

- **SC-001**: A reset database contains ≥10 requests spanning ≥4 distinct statuses. ✅ **13 requests, 6 statuses**
- **SC-002**: `Balance + held = initial` for every employee. ✅ **verified for all 5**
- **SC-003**: The approver queue and employee history render seeded data. ✅ **queue shows 4; history renders**
- **SC-004**: `novaleave_vacation_requests_pending` and `..._oldest_pending_age_seconds` report non-empty values. ✅ **4 pending, 12.0 days**
- **SC-005**: Restarting does not duplicate data. ✅ **13 before and after; seeder reported 0**
- **SC-006**: Existing test suites unchanged. ✅ **Domain 63/63, Application 49/49, Presentation at baseline**

---

## Seeded dataset

| Owner | Initial | Pending | Approved | Rejected | Cancelled | Expired | Voided | Final balance |
|-------|---------|---------|----------|----------|-----------|---------|--------|---------------|
| Juan Pérez (`user@`) | 15 | 2 (3d, 5d) | 1 (4d) | 1 (2d) | 1 (3d) | — | — | **3** |
| Pedro López (`employee@`) | 12 | 1 (2d) | 1 (5d) | — | — | 1 (3d) | 1 (4d) | **5** |
| Carlos Rodríguez (`manager@`) | 20 | 1 (5d) | 1 (3d) | 1 (4d) | 1 (2d) | — | — | **12** |
| María García (`approver@`) | 10 | — | — | — | — | — | — | **10** |
| Ana Martínez (`inactive@`) | 10 | — | — | — | — | — | — | **10** |

Pending ages: 1, 2, 5 and **12 days** — the 12-day request is what makes the oldest-pending-age gauge show a real number.

---

## Recorded Gaps

- **GAP-008-1** *(found during implementation — a defect in the application, not the seeder)*: **approving a request charges the employee twice.** `CreateRequestHandler` calls `employee.ReserveDays(n)` and `ApproveRequestHandler` then calls `employee.DeductDays(n)` with no intervening restore — and `ReserveDays` and `DeductDays` are **identical implementations**, both `Balance -= days`. A 15-day employee whose 3-day request is approved ends with 9 days, not 12. Seed data deliberately does **not** replicate this; see below.
- **GAP-008-2**: Audit records are still not seeded, so the (also unbuilt) audit history view has nothing to show. See `docs/Keep-in-mind.md` item 4.
- **GAP-008-3**: `VacationRequest.CreatedAt` is assigned `DateTimeOffset.UtcNow` directly (`VacationRequest.cs:43`), which `architecture.md` prohibits in the Domain (Constitution §2.VI). It is why backdating has to go through the EF entry rather than the constructor.

---

## Deliberate deviation: balance semantics

The seeder applies **one net charge per request that holds days**, and restores on Rejected / Cancelled / Expired / Voided. It does not reproduce the double deduction described in GAP-008-1.

Reasoning: fixture data is read by people as a statement of what correct data looks like. Baking in a known defect would make it look intentional, and would mean every balance in the database silently disagreed with its own request history. The seeded data is therefore internally consistent — and `Balance + held = initial` now holds, which is a property worth being able to assert.

Consequence: once GAP-008-1 is fixed, the seeded data needs no change. Until then, balances created *through the UI* will drift from balances created *by the seeder* on approval — which is itself a useful way to notice the bug.
