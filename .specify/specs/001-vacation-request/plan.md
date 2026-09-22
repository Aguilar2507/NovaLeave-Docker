# Implementation Plan: Vacation Request Management

**Branch**: `001-vacation-request-core` | **Date**: 2026-07-28 | **Spec**: [spec_001-vacation-request.md](../../.specify/specs/spec_001-vacation-request.md)

**Input**: Feature specification from `.specify/specs/spec_001-vacation-request.md`

---

## Summary

Implement the core leave/vacation request workflow for NovaLeave MVP: Employee submits requests (validated date range, working-day calculation, overlap prevention, balance check), Approver approves/rejects, Employee cancels pending or voids approved (pre-start), auto-expiry of stale pending requests, and request history with balance display.

---

## Technical Context

**Language/Version**: C# / .NET 10  
**Primary Dependencies**: ASP.NET Core MVC, EF Core, FluentValidation, Serilog  
**Storage**: SQL Server (VacationRequest, Employee, AuditRecord + exclusion constraints)  
**Testing**: xUnit, WebApplicationFactory, Playwright  
**Target Platform**: Linux container / Azure App Service  
**Project Type**: Web application (server-rendered MVC)  
**Performance Goals**: Request submission p95 < 500ms, list views p95 < 300ms  
**Constraints**: No negative balances, server-side working-day calc, optimistic concurrency, DB exclusion constraint for overlap

---

## Common Artifacts (DO NOT DUPLICATE)

The following shared artifacts are defined once and referenced here:

- **Data Model**: [`specs/common/data-model.md`](../common/data-model.md) — VacationRequest, Employee, AuditRecord, RequestStatus
- **Security Requirements**: [`specs/common/security.md`](../common/security.md) — SR-001 through SR-011, SR-013
- **Architecture & Tech Stack**: [`specs/common/architecture.md`](../common/architecture.md) — Clean Architecture, layers, testing strategy, configuration

---

## Constitution Check

| Gate | Status |
|------|--------|
| Clean Architecture (§2.I) | ✅ Presentation → Application → Domain |
| Rich Domain / business-rule isolation (§2.IV) | ✅ Invariants in Domain entities |
| Single source of truth (§2.III) | ✅ Balance/overlap/transition rules in Domain only |
| Explicit use cases (§2.V) | ✅ Vertical slices in Application |
| Anti-CSRF global (§7.2) | ✅ AutoValidateAntiforgeryToken |
| Post/Redirect/Get (§7.2) | ✅ All form submissions |
| Dedicated ViewModels (§7.2) | ✅ Per-operation models |
| Server-authoritative validation (§7.2) | ✅ Working days, balance, overlap server-side |
| Read-session validity (SR-013) | ✅ Inactive users lose read access within 5 min, write access immediately |
| Optimistic concurrency (§5) | ✅ RowVersion on VacationRequest |
| TimeProvider (§2.VI) | ✅ Date validation, auto-expiry |
| Async I/O (§2.VIII) | ✅ End-to-end async |
| Structured logging/audit (§8) | ✅ All state transitions audited |

---

## Project Structure

### Documentation (this feature)

```text
specs/001-vacation-request/
├── plan.md              # This file
├── research.md          # Phase 0: EF Core exclusion constraints, working-day calc
├── contracts/           # Phase 1: Interface contracts
│   ├── IWorkingDayCalculator.cs
│   ├── IHolidayCalendar.cs
│   ├── IVacationRequestRepository.cs
│   └── IStateTransitionAuditService.cs
└── quickstart.md        # Phase 1: Getting started guide
```

### Source Code

```text
src/
  NovaLeave.Domain/
	Entities/
	  Employee.cs                 (Balance, ReserveDays, DeductDays, RestoreDays)
	  VacationRequest.cs          (State machine, transitions, invariants)
	ValueObjects/
	  DateRange.cs                (StartDate, EndDate, validation)
	Services/
	  OverlapChecker.cs           (Domain service for overlap logic)

  NovaLeave.Application/
	Features/VacationRequest/
	  Create/
		CreateRequestCommand.cs
		CreateRequestValidator.cs
		CreateRequestHandler.cs
	  Approve/
		ApproveRequestCommand.cs
		ApproveRequestHandler.cs
	  Reject/
		RejectRequestCommand.cs
		RejectRequestValidator.cs
		RejectRequestHandler.cs
	  Cancel/
		CancelRequestCommand.cs
		CancelRequestHandler.cs
	  Void/
		VoidRequestCommand.cs
		VoidRequestHandler.cs
	  Edit/
		EditRequestCommand.cs
		EditRequestValidator.cs
		EditRequestHandler.cs
	  List/
		ListMyRequestsQuery.cs
		ListMyRequestsHandler.cs
		ListPendingForApproverQuery.cs
		ListPendingForApproverHandler.cs
	  Contracts/
		IWorkingDayCalculator.cs
		IHolidayCalendar.cs
		IVacationRequestRepository.cs

  NovaLeave.Infrastructure/
	Persistence/
	  VacationRequestConfiguration.cs   (EF Core, exclusion constraint)
	  EmployeeConfiguration.cs
	  VacationRequestRepository.cs
	Services/
	  WorkingDayCalculator.cs
	  HolidayCalendar.cs
	BackgroundJobs/
	  AutoExpiryJob.cs                   (Hosted service for pending expiry)

  NovaLeave.Presentation.Web/
	Controllers/
	  EmployeeRequestsController.cs     (Create, Edit, Cancel, Void, List)
	  ApproverRequestsController.cs     (List pending, Approve, Reject)
	ViewModels/
	  CreateRequestViewModel.cs
	  EditRequestViewModel.cs
	  RejectRequestViewModel.cs
	  RequestListViewModel.cs
	  RequestDetailViewModel.cs
	Views/
	  EmployeeRequests/
		Create.cshtml
		Edit.cshtml
		Index.cshtml (history)
		Detail.cshtml
	  ApproverRequests/
		Index.cshtml (pending queue)
		Detail.cshtml
		_ConfirmationModal.cshtml

tests/
  NovaLeave.Domain.Tests/
	VacationRequestTests.cs        (State transitions, invariants)
	EmployeeBalanceTests.cs        (Reserve, deduct, restore)
	DateRangeTests.cs
	OverlapCheckerTests.cs
  NovaLeave.Application.Tests/
	CreateRequestHandlerTests.cs
	ApproveRequestHandlerTests.cs
	RejectRequestHandlerTests.cs
	EditRequestHandlerTests.cs
  NovaLeave.Presentation.Tests/
	VacationRequest/
	  CreateFlowTests.cs
	  ApprovalFlowTests.cs
	  CancellationTests.cs
	  ConcurrencyTests.cs
	  AuthorizationTests.cs
	  AutoExpiryTests.cs
	  AuditingTests.cs
```

---

## Implementation Phases

### Phase 0 — Research

- EF Core database exclusion constraints for date range overlap (SQL Server `tsrange` equivalent or check constraints)
- Working-day calculation library options vs custom implementation
- Background job scheduling for auto-expiry (IHostedService vs Hangfire vs simple timer)
- Optimistic concurrency patterns with EF Core `RowVersion`

### Phase 1 — Design & Contracts

1. Define `VacationRequest` entity with state machine (transitions, guards)
2. Define `DateRange` value object with validation
3. Define `Employee` balance methods (Reserve, Deduct, Restore)
4. Define `IWorkingDayCalculator` and `IHolidayCalendar` contracts
5. Define command/query models for each operation
6. Design EF Core configuration with exclusion constraint

### Phase 2 — Implementation

1. **Domain entities**: `VacationRequest` (state machine), `Employee` (balance), `DateRange`
2. **Domain service**: `OverlapChecker`
3. **Application handlers**: Create, Approve, Reject, Cancel, Void, Edit
4. **Transition auditing**: Invoke `IStateTransitionAuditService.RecordTransitionAsync` in each handler (Create, Approve, Reject, Cancel, Void, Edit, Expire) post-transition, within the same database transaction
5. **Application queries**: List (employee history + approver queue) with pagination
6. **Infrastructure**: EF Core configurations, repository, working-day calculator, holiday calendar
7. **Infrastructure**: Auto-expiry background job
8. **Presentation**: `EmployeeRequestsController` (CRUD for employee)
9. **Presentation**: `ApproverRequestsController` (queue + approve/reject)
10. **ViewModels + Views**: Forms, lists, detail, confirmation modals
11. **Authorization policies**: Owner-only, assigned-approver-only, no self-approval

### Phase 3 — Testing

1. Domain unit tests: state transitions (all valid + invalid), balance, date range, overlap
2. Application unit tests: handlers with mocked infrastructure
3. Integration tests: full create→approve flow, create→reject flow
4. Integration tests: overlap prevention under concurrency (DB constraint)
5. Integration tests: auto-expiry, void pre-start, cancel pending
6. Integration tests: edit flow (re-overlap, re-balance validation, concurrency)
7. Authorization tests: IDOR, self-approval, cross-team access
8. Audit tests: verify every state transition produces an AuditRecord with all required fields (SC-005, Constitution §8)
9. E2E tests: employee submits → approver resolves → balance updated

---

## Feature-Specific Functional Requirements

> Common security requirements are in [`specs/common/security.md`](../common/security.md). Shared entity definitions in [`specs/common/data-model.md`](../common/data-model.md).

| ID | Requirement |
|----|-------------|
| FR-001 | Employee submits request with start date, end date, reason |
| FR-002 | Reject invalid dates, past/today start, overlapping ranges (app + DB constraint) |
| FR-003 | Server-side working-day calculation (exclude weekends + holidays) |
| FR-004 | Approver approves/rejects assigned Pending requests; no self-approval; rejection reason mandatory |
| FR-005 | Reserve days on Pending; deduct on Approve; release on Reject/Cancel/Void/Expire |
| FR-006 | Prevent negative balance on any transition |
| FR-007 | Employee cancels own Pending request → Cancelled (final) |
| FR-008 | Final states: Approved, Rejected, Cancelled, Voided, Expired — no further transitions |
| FR-009 | Edit Pending request: full revalidation (dates, overlap, balance) with optimistic concurrency |
| FR-010 | Reason field visible only to owner + assigned approver |
| FR-011 | Audit entry for every state transition |
| FR-012 | Revalidate identity, ownership, approver relationship, balance before state change |
| FR-013 | Optimistic concurrency; DB exclusion constraint for overlap |
| FR-014 | Employee views only own history + balance |
| FR-015 | Void Approved request only if start date not passed; return days |
| FR-016 | Auto-expiry after EXPIRY_DAYS working days; release reserved days |
| FR-017 | Explicit confirmation for all final actions |

---

## Design Decisions (Feature-Specific)

| ID | Decision |
|----|----------|
| D-001 | Working days exclude Sat/Sun + configurable holiday calendar (server-side) |
| D-002 | Date range as value object; duration derived, not client-supplied |
| D-003 | DB exclusion constraint + app-level check for overlap (defense-in-depth) |
| D-004 | Reserve on create, deduct on approve, restore on deactivation |
| D-005 | Auto-expiry via IHostedService periodic check |
| D-006 | Expired is a final state |

---

## Success Criteria

See spec: SC-001 through SC-009 in `spec_001-vacation-request.md`.

---

## Dependencies

- **Depends on**: spec_004 (authentication must exist for any protected operation)
- **Depends on**: Common data model, common security, common architecture
- **Blocks**: Future features (notifications, reports, HR views)
