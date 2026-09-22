# Quickstart: Vacation Request Management

**Feature Branch**: `001-vacation-request-core`  
**Date**: 2026-07-28  
**Plan Reference**: [plan.md](./plan.md)  
**Contracts Reference**: [contracts.md](./contracts.md)  
**Research Reference**: [research.md](./research.md)

---

## Prerequisites

- .NET 10 SDK installed
- SQL Server instance available
- Solution `NovaLeave.slnx` open in Visual Studio 2026
- **Feature 004 (Login/Authentication) implemented** — this feature depends on authenticated sessions
- Common artifacts reviewed: [architecture.md](../common/architecture.md), [data-model.md](../common/data-model.md), [security.md](../common/security.md)

---

## Step-by-Step Implementation Guide

### Step 1 — Create the feature branch

```bash
git checkout -b 001-vacation-request-core
```

---

### Step 2 — Create Domain Entities

#### 2a. DateRange Value Object

File: `src/NovaLeave.Domain/ValueObjects/DateRange.cs`

- Immutable record with `StartDate` and `EndDate` (`DateOnly`).
- Constructor validates `StartDate <= EndDate`.
- Method `OverlapsWith(DateRange other)` for overlap detection.

#### 2b. VacationRequest Entity

File: `src/NovaLeave.Domain/Entities/VacationRequest.cs`

- Properties: `Id`, `OwnerId`, `StartDate`, `EndDate`, `Reason`, `Status`, `RequestedDays`, `CreatedAt`, `ResolvedAt`, `ResolvedBy`, `RejectionReason`, `ExpiryDate`, `RowVersion`.
- State transition methods: `Approve()`, `Reject()`, `Cancel()`, `Void()`, `Expire()`, `Edit()`.
- All methods validate preconditions and throw explicit exceptions.
- See [contracts.md](./contracts.md) §6 for signatures and [research.md](./research.md) §5 for implementation.

#### 2c. Employee Balance Methods

File: `src/NovaLeave.Domain/Entities/Employee.cs`

Add to existing `Employee` entity:
- `ReservedDays` property
- `AvailableBalance` computed property (`Balance - ReservedDays`)
- Methods: `ReserveDays()`, `ReleaseReservation()`, `DeductDays()`, `RestoreDays()`
- See [research.md](./research.md) §6.

#### 2d. Domain Exceptions

File: `src/NovaLeave.Domain/Exceptions/`

- `InvalidStateTransitionException`
- `SelfApprovalException`
- `InsufficientBalanceException`
- `OverlappingRequestException`
- `RejectionReasonRequiredException`
- `VacationAlreadyStartedException`
- `NegativeBalanceException`
- `ConcurrencyConflictException`

---

### Step 3 — Create Domain Service

File: `src/NovaLeave.Domain/Services/OverlapChecker.cs`

```csharp
public static class OverlapChecker
{
	public static bool Overlaps(DateOnly start1, DateOnly end1, DateOnly start2, DateOnly end2)
		=> start1 <= end2 && end1 >= start2;
}
```

Used by Application handlers for pre-validation (the DB serializable transaction is the authoritative check).

---

### Step 4 — Create Application Contracts

File locations in `src/NovaLeave.Application/Features/VacationRequest/Contracts/`:

| File | Interface |
|------|-----------|
| `IWorkingDayCalculator.cs` | Working-day calculation |
| `IHolidayCalendar.cs` | Holiday data provider |
| `IVacationRequestRepository.cs` | Data access |
| `IStateTransitionAuditService.cs` | Audit logging |

Also create: `src/NovaLeave.Application/Common/PaginatedResult.cs`

See [contracts.md](./contracts.md) §1 for all signatures.

---

### Step 5 — Create FluentValidation Validators

Files in `src/NovaLeave.Application/Features/VacationRequest/`:

| Path | Validator |
|------|-----------|
| `Create/CreateRequestValidator.cs` | Input validation for create |
| `Reject/RejectRequestValidator.cs` | Rejection reason required |
| `Edit/EditRequestValidator.cs` | Input validation for edit |

See [contracts.md](./contracts.md) §4. Remember: business rules (future dates, overlap, balance) are in the handler/Domain, not here (Constitution §2.IV).

---

### Step 6 — Create Application Command/Query Handlers

Files in `src/NovaLeave.Application/Features/VacationRequest/`:

#### Create Handler (`Create/CreateRequestHandler.cs`)
1. Validate input with `CreateRequestValidator.ValidateAsync()`
2. Verify start date is strictly future (`TimeProvider`)
3. Calculate working days (`IWorkingDayCalculator`)
4. Check overlap (`IVacationRequestRepository.HasOverlappingRequestAsync`)
5. Check balance (`Employee.AvailableBalance >= requestedDays`)
6. Create `VacationRequest` entity (Pending)
7. Reserve days on Employee (`ReserveDays`)
8. Save in serializable transaction
9. Record audit (`IStateTransitionAuditService`)

#### Approve Handler (`Approve/ApproveRequestHandler.cs`)
1. Load request + verify Pending status
2. Verify approver is not the owner (no self-approval)
3. Verify approver is Active
4. Verify balance won't go negative on deduction
5. Call `request.Approve(approverId, timeProvider)`
6. Call `employee.DeductDays(requestedDays)`
7. Save with concurrency check
8. Record audit

#### Reject Handler (`Reject/RejectRequestHandler.cs`)
1. Validate rejection reason
2. Load request + verify Pending
3. Call `request.Reject(approverId, reason, timeProvider)`
4. Call `employee.ReleaseReservation(requestedDays)`
5. Save + audit

#### Cancel Handler (`Cancel/CancelRequestHandler.cs`)
1. Verify owner matches
2. Load request + verify Pending
3. Call `request.Cancel()`
4. Call `employee.ReleaseReservation(requestedDays)`
5. Save + audit

#### Void Handler (`Void/VoidRequestHandler.cs`)
1. Verify owner matches
2. Load request + verify Approved
3. Verify start date is in the future (`TimeProvider`)
4. Call `request.Void(today)`
5. Call `employee.RestoreDays(requestedDays)`
6. Save + audit

#### Edit Handler (`Edit/EditRequestHandler.cs`)
1. Validate input
2. Verify owner + Pending status
3. Release old reservation
4. Recalculate working days
5. Check overlap (excluding current request)
6. Check balance for new amount
7. Call `request.Edit(...)` + `employee.ReserveDays(newDays)`
8. Save in serializable transaction + audit

#### List Handlers
- `ListMyRequestsHandler`: Query by owner with pagination, `AsNoTracking()`
- `ListPendingForApproverHandler`: Query Pending requests with pagination

#### Expire Handler (`ExpireStaleRequestsHandler.cs`)
- Called by background service
- Query expired Pending requests
- For each: `request.Expire()` + `employee.ReleaseReservation()` + audit
- Each in its own transaction (failure of one doesn't block others)

---

### Step 7 — Create Infrastructure Implementations

Files in `src/NovaLeave.Infrastructure/`:

| File | Description |
|------|-------------|
| `Persistence/VacationRequestConfiguration.cs` | EF Core entity config + indexes |
| `Persistence/EmployeeConfiguration.cs` | Employee entity config |
| `Persistence/VacationRequestRepository.cs` | Implements `IVacationRequestRepository` |
| `Services/WorkingDayCalculator.cs` | Implements `IWorkingDayCalculator` |
| `Services/HolidayCalendar.cs` | Implements `IHolidayCalendar` |
| `Services/StateTransitionAuditService.cs` | Implements `IStateTransitionAuditService` |
| `BackgroundJobs/AutoExpiryBackgroundService.cs` | IHostedService for auto-expiry |

See [research.md](./research.md) for implementation details.

Register all services in a DI extension method:

```csharp
public static IServiceCollection AddNovaLeaveVacationServices(this IServiceCollection services)
{
	services.AddScoped<IVacationRequestRepository, VacationRequestRepository>();
	services.AddScoped<IWorkingDayCalculator, WorkingDayCalculator>();
	services.AddScoped<IHolidayCalendar, HolidayCalendar>();
	services.AddScoped<IStateTransitionAuditService, StateTransitionAuditService>();
	services.AddHostedService<AutoExpiryBackgroundService>();
	return services;
}
```

---

### Step 8 — Create EF Core Migration

```bash
dotnet ef migrations add AddVacationRequests \
  --project src/NovaLeave.Infrastructure \
  --startup-project src/NovaLeave.Presentation.Web
```

Review the migration for:
- [ ] `RowVersion` column on VacationRequest
- [ ] Filtered composite index on `(OwnerId, Status, StartDate, EndDate)`
- [ ] Filtered index on `(Status, ExpiryDate)` for auto-expiry
- [ ] Foreign keys with appropriate cascade behavior

```bash
dotnet ef database update \
  --project src/NovaLeave.Infrastructure \
  --startup-project src/NovaLeave.Presentation.Web
```

---

### Step 9 — Create Presentation Layer

#### Controllers

File: `src/NovaLeave.Presentation.Web/Controllers/EmployeeRequestsController.cs`

```csharp
[Authorize(Roles = "User")]
public class EmployeeRequestsController : Controller
{
	// GET /leave-requests
	public async Task<IActionResult> Index(int page = 1) { ... }

	// GET /leave-requests/create
	public async Task<IActionResult> Create() { ... }

	// POST /leave-requests/create
	[HttpPost][ValidateAntiForgeryToken]
	public async Task<IActionResult> Create(CreateRequestViewModel model) { ... }

	// GET /leave-requests/{id}
	public async Task<IActionResult> Detail(Guid id) { ... }

	// GET /leave-requests/{id}/edit
	public async Task<IActionResult> Edit(Guid id) { ... }

	// POST /leave-requests/{id}/edit
	[HttpPost][ValidateAntiForgeryToken]
	public async Task<IActionResult> Edit(Guid id, EditRequestViewModel model) { ... }

	// POST /leave-requests/{id}/cancel
	[HttpPost][ValidateAntiForgeryToken]
	public async Task<IActionResult> Cancel(Guid id) { ... }

	// POST /leave-requests/{id}/void
	[HttpPost][ValidateAntiForgeryToken]
	public async Task<IActionResult> Void(Guid id) { ... }
}
```

File: `src/NovaLeave.Presentation.Web/Controllers/ApproverRequestsController.cs`

```csharp
[Authorize(Roles = "Approver")]
public class ApproverRequestsController : Controller
{
	// GET /approver/pending
	public async Task<IActionResult> Index(int page = 1) { ... }

	// GET /approver/requests/{id}
	public async Task<IActionResult> Detail(Guid id) { ... }

	// POST /approver/requests/{id}/approve
	[HttpPost][ValidateAntiForgeryToken]
	public async Task<IActionResult> Approve(Guid id) { ... }

	// POST /approver/requests/{id}/reject
	[HttpPost][ValidateAntiForgeryToken]
	public async Task<IActionResult> Reject(Guid id, RejectRequestViewModel model) { ... }
}
```

> Routes follow Constitution §3.3: resource-oriented, readable. State changes use POST.

#### ViewModels

See [contracts.md](./contracts.md) §5. Create all ViewModels in `src/NovaLeave.Presentation.Web/ViewModels/`.

#### Views

Create Razor views in `src/NovaLeave.Presentation.Web/Views/`:

| View | Description |
|------|-------------|
| `EmployeeRequests/Index.cshtml` | Request history with pagination + balance display |
| `EmployeeRequests/Create.cshtml` | Create form with date pickers |
| `EmployeeRequests/Edit.cshtml` | Edit form (Pending only) |
| `EmployeeRequests/Detail.cshtml` | Request detail with action buttons |
| `ApproverRequests/Index.cshtml` | Pending queue with pagination |
| `ApproverRequests/Detail.cshtml` | Request detail + approve/reject actions |
| `Shared/_ConfirmationModal.cshtml` | Reusable confirmation modal (SR-010) |

Key UI requirements:
- Bootstrap 5.3 responsive layout
- `asp-for` / `asp-validation-for` Tag Helpers
- Confirmation modal for all final actions (approve, reject, cancel, void)
- Status badges with text (not color-only — Constitution §11.2)
- Server-side pagination controls
- Anti-forgery tokens on all forms

---

### Step 10 — Seed Test Data

Add to the existing seeder:
- Public holidays for the current year
- Sample vacation requests in various states
- Employee with sufficient balance for testing

---

### Step 11 — Write Tests

#### Domain Tests (`tests/NovaLeave.Domain.Tests/`)

| Test Class | Covers |
|-----------|--------|
| `VacationRequestTests.cs` | All state transitions (valid + invalid), self-approval prevention |
| `EmployeeBalanceTests.cs` | Reserve, deduct, restore, release, negative balance prevention |
| `DateRangeTests.cs` | Validation, overlap detection |

#### Application Tests (`tests/NovaLeave.Application.Tests/`)

| Test Class | Covers |
|-----------|--------|
| `CreateRequestHandlerTests.cs` | Happy path, validation failures, overlap, insufficient balance |
| `ApproveRequestHandlerTests.cs` | Happy path, self-approval, wrong state, negative balance |
| `RejectRequestHandlerTests.cs` | Happy path, missing reason, wrong state |

#### Integration Tests (`tests/NovaLeave.Presentation.Tests/`)

| Test Class | Covers |
|-----------|--------|
| `CreateFlowTests.cs` | Full create flow via WebApplicationFactory |
| `ApprovalFlowTests.cs` | Create → approve → balance updated |
| `CancellationTests.cs` | Cancel pending, void approved, void after start rejected |
| `ConcurrencyTests.cs` | Two simultaneous approvals → one succeeds (SC-006) |
| `AuthorizationTests.cs` | IDOR, cross-user access, self-approval, forced browsing |
| `AutoExpiryTests.cs` | Pending → Expired after timeout, balance released |
| `OverlapTests.cs` | Concurrent overlapping submissions → one persisted |

---

### Step 12 — Build and Verify

```bash
dotnet build
dotnet test
```

Verify:
- [ ] Employee can create a request → Pending + days reserved
- [ ] Approver can approve → Approved + days deducted
- [ ] Approver can reject → Rejected + days released + reason stored
- [ ] Employee can cancel Pending → Cancelled + days released
- [ ] Employee can void Approved (future start) → Voided + days restored
- [ ] Void rejected if start date passed
- [ ] Overlap prevented (app + DB level)
- [ ] Negative balance prevented on all paths
- [ ] Self-approval denied
- [ ] Concurrency: one of two simultaneous operations wins
- [ ] Auto-expiry transitions stale requests
- [ ] All transitions produce audit records
- [ ] Pagination works for > 50 records
- [ ] Anti-forgery validated on all POST actions
- [ ] Unauthorized access returns 404 (existence hiding)

---

## Route Summary

| Method | Route | Action | Auth |
|--------|-------|--------|------|
| GET | `/leave-requests` | Employee request list | User |
| GET | `/leave-requests/create` | Create form | User |
| POST | `/leave-requests/create` | Submit request | User |
| GET | `/leave-requests/{id}` | Request detail | User (owner) |
| GET | `/leave-requests/{id}/edit` | Edit form | User (owner, Pending) |
| POST | `/leave-requests/{id}/edit` | Submit edit | User (owner, Pending) |
| POST | `/leave-requests/{id}/cancel` | Cancel request | User (owner, Pending) |
| POST | `/leave-requests/{id}/void` | Void request | User (owner, Approved) |
| GET | `/approver/pending` | Pending queue | Approver |
| GET | `/approver/requests/{id}` | Request detail | Approver |
| POST | `/approver/requests/{id}/approve` | Approve | Approver |
| POST | `/approver/requests/{id}/reject` | Reject | Approver |

---

## Configuration (appsettings.json additions)

```json
{
  "VacationRequest": {
	"ExpiryDays": 10,
	"AutoExpiryCheckIntervalMinutes": 15,
	"MaxReasonLength": 1000,
	"DefaultPageSize": 20,
	"MaxPageSize": 50
  }
}
```

---

## Done Checklist

- [ ] Constitution Check passed (see [plan.md](./plan.md))
- [ ] All FR-001 through FR-017 implemented
- [ ] All SR from [security.md](../common/security.md) applied
- [ ] Tests cover SC-001 through SC-009
- [ ] Domain invariants tested (positive + negative)
- [ ] Concurrency tested (double approval, overlapping submissions)
- [ ] No PII leaks (reason field restricted)
- [ ] PR < 400 net lines (split into multiple PRs if needed)
- [ ] Migration reviewed and reversible
