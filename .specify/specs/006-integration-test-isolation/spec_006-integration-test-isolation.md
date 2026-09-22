# Feature Specification: Integration Test Isolation and Role Alignment

**Feature Branch**: _(not yet created)_
**Created**: 2026-09-22
**Status**: Draft — Not Scheduled
**Constitution Reference**: NovaLeave — Constitution v4.0.0
**Origin**: Discovered during spec_005 implementation (GAP-005-5)

---

## Overview

`NovaLeave.Presentation.Tests` fails 24–26 of its 49 tests, non-deterministically, on a clean `main`. This specification records the diagnosis so it is not rediscovered, and defines what "fixed" means. It was deliberately **not** fixed inside spec_005, which is a containerization feature; mixing the two would have violated Constitution §16.1 (one feature per branch).

This matters beyond tidiness: while this suite is red, Constitution §9.3's CI gate cannot pass, and the project's primary defense against regressions in authentication and vacation-request flows is inert.

**Not in scope**: adding new test coverage, or changing any application behavior. This is a repair of existing tests, and it must not alter `src/`.

---

## Evidence

Measured 2026-09-22 inside `mcr.microsoft.com/dotnet/sdk:10.0`, three consecutive runs per tree:

| Tree | Failures across runs |
|------|----------------------|
| `main` @ `ec34e0a` | 26, 25, 25 |
| `docker-implementation` (spec_005 Phase 1) | 24, 26, 26 |

The failure count varies between identical runs of identical code, so the suite is both **failing** and **order-dependent**. `NovaLeave.Domain.Tests` (63) and `NovaLeave.Application.Tests` (41) pass cleanly and are unaffected.

---

## Root Cause 1 — A Single Shared In-Memory Database

`src/NovaLeave.Presentation.Web/Program.cs` configures the test provider with a fixed database name:

```csharp
options.UseInMemoryDatabase("NovaLeaveTestDb")
```

EF Core's InMemory provider resolves a given name against a **process-wide static root** by default. `AuthTestFixture` and `VacationTestFixture` are separate `WebApplicationFactory` instances, but they resolve to the *same* store. Each seeds its own data into it, and each guards seeding with an `if (await db.Employees.AnyAsync()) return;` check that the other fixture can satisfy first.

Observed symptoms: `Sequence contains more than one element`, `Failed to create test admin`, `Assert.NotNull() Failure: Value is null`.

`AuthTestFixture.ConfigureWebHost` compounds this by calling `services.BuildServiceProvider()` to seed, which constructs a second container whose `DbContext` is not the one the application under test resolves. It appears to work only because the InMemory root is shared — the very thing that breaks isolation.

## Root Cause 2 — Fixtures Use Roles That Do Not Exist

Constitution §4 is unambiguous: *"The MVP defines exactly two application roles — `User` and `Approver`"*, with no HR or administrative role.

| Source | Roles referenced |
|--------|------------------|
| `src/` | `User` (20), `Approver` (16) |
| `tests/NovaLeave.Presentation.Tests/` | `Employee` (9), `Approver` (3), `Admin` (3) |

The fixtures seed an `Employee` role the application never grants and an `Admin` role the Constitution forbids. Observed symptom: `Role EMPLOYEE does not exist.`

This is a test-side defect. The application and the Constitution agree with each other; only the fixtures disagree with both.

---

## Requirements

- **FR-001**: Each test fixture MUST use an isolated database instance; no store may be shared between fixtures or between test classes that mutate data.
- **FR-002**: Isolation MUST NOT depend on test execution order.
- **FR-003**: Fixtures MUST seed only the roles the Constitution defines — `User` and `Approver`. References to `Employee` and `Admin` roles MUST be removed.
- **FR-004**: Fixtures MUST seed through the application's own service provider, not a separately built one.
- **FR-005**: No file under `src/` may change in order to satisfy this specification, except the InMemory database naming in `Program.cs` if isolation requires it.
- **FR-006**: Tests that assert on authorization MUST exercise the same role names the application grants, so a role rename cannot pass silently.

## Success Criteria

- **SC-001**: `NovaLeave.Presentation.Tests` passes 49/49.
- **SC-002**: Ten consecutive runs produce identical results, confirming order-independence.
- **SC-003**: No application behavior changes; `Domain.Tests` and `Application.Tests` remain green.
- **SC-004**: Constitution §9.3's CI gate becomes achievable for the first time.

---

## Open Questions for the Product Owner

1. Were these fixtures written against an earlier role model (`Employee`/`Admin`) that the Constitution later narrowed to `User`/`Approver`? If so, are there other artifacts still carrying the old model?
2. Should the 3 currently-skipped tests be repaired, removed, or left skipped with a documented reason?
3. Should this be scheduled before any further feature work, given that it gates CI?
