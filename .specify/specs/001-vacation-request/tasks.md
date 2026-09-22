---
description: "Task list for feature 001 - Leave/Vacation Request Management (Core MVP)"
---

# Tasks: Leave/Vacation Request Management - Core MVP

**Input**: Design documents from `.specify/specs/001-vacation-request/`

**Prerequisites**: `plan.md`, `spec_001-vacation-request.md`, `.specify/specs/common/architecture.md`, `.specify/specs/common/data-model.md`, `.specify/specs/common/security.md`

**Tests**: REQUIRED - spec mandates xUnit unit/integration tests (with `WebApplicationFactory`) and Playwright E2E. All test tasks below are non-optional.

**Organization**: Tasks are grouped by user story (US1-US5) so each story can be implemented, tested, and demoed independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: `US1`-`US5` (or `SETUP`/`FOUND`/`POLISH`/`NAV` for shared work)
- All paths are exact and match the "Source Code" section of `plan.md`

## Path Conventions

- Source: `src/NovaLeave.Domain/`, `src/NovaLeave.Application/`, `src/NovaLeave.Infrastructure/`, `src/NovaLeave.Presentation.Web/`
- Tests: `tests/NovaLeave.Domain.Tests/`, `tests/NovaLeave.Application.Tests/`, `tests/NovaLeave.Presentation.Tests/`, `tests/NovaLeave.E2E.Tests/`

---

## Phase 1: Setup (Verify & Extend Shared Infrastructure)

**Purpose**: Verify the shared solution scaffold (created by spec_004 Phase 0) is in place and extend it with spec_001-specific needs.

**Prerequisite**: spec_004 Phase 0 (Solution Scaffold) MUST be complete. That phase creates the 4 src projects, 2 test projects, Directory.Build.props, NuGet packages, shared domain entities (Employee, AuditRecord, AccountStatus), Program.cs with MVC+RazorPages coexistence, and appsettings.Development.json with LocalDB connection string.

- [x] T001 [SETUP] Verify solution scaffold from spec_004 Phase 0 is complete: 4 src projects + 4 test projects exist (including `Domain.Tests` and `E2E.Tests`), `dotnet build` passes, `Directory.Build.props` applies net10.0 with `TreatWarningsAsErrors=true`
- [x] T002 [P] [SETUP] Configure Serilog structured logging in `src/NovaLeave.Presentation.Web/Program.cs` with request-id and correlation-id enrichment (per `common/architecture.md`) - spec_004 Phase 0 only adds the package, does not configure the pipeline

**Checkpoint**: Solution builds with all 8 projects; Serilog writes structured logs on startup.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Cross-cutting infrastructure that ALL user stories depend on. Must be complete before any US phase begins.

**CRITICAL**: No US work can start until this phase is complete.

- [x] T010 [FOUND] Verify `RequestStatus` enum exists in `src/NovaLeave.Domain/Entities/RequestStatus.cs` (created by spec_004 Phase 0 T000o2) with values: `Pending`, `Approved`, `Rejected`, `Cancelled`, `Voided`, `Expired`.
- [x] T011 [P] [FOUND] Verify `DateRange` value object exists in `src/NovaLeave.Domain/ValueObjects/DateRange.cs` (created by spec_004 Phase 0 T000q3) with StartDate/EndDate invariants.
- [x] T012 [P] [FOUND] Verify `Employee` entity in `src/NovaLeave.Domain/Entities/Employee.cs` (created by spec_004 Phase 0 T000p) includes all vacation-specific members: `AssignedApproverId`, `Balance`, `ReserveDays()`, `DeductDays()`, `RestoreDays()`, non-negative balance guard (FR-006).
- [x] T013 [FOUND] Verify `VacationRequest` entity in `src/NovaLeave.Domain/Entities/VacationRequest.cs` (created by spec_004 Phase 0 T000q2) includes state machine (`Submit`, `Approve`, `Reject`, `Cancel`, `Void`, `Expire`, `Edit`), transitions guarded (FR-008), `RowVersion` for optimistic concurrency (FR-013).
- [x] T014 [P] [FOUND] Verify `AuditRecord` entity in `src/NovaLeave.Domain/Entities/AuditRecord.cs` exists (created by spec_004 Phase 0 T000q) and matches `common/data-model.md` (SR-008).
- [x] T015 [P] [FOUND] Define contracts in `src/NovaLeave.Application/Features/VacationRequest/Contracts/`: `IWorkingDayCalculator.cs`, `IHolidayCalendar.cs`, `IVacationRequestRepository.cs`, `IStateTransitionAuditService.cs`. (**Note**: `IBalanceService` removed — balance is managed directly by `Employee` entity methods per Constitution §2.III single-source-of-truth and research.md recommendation.)
- [x] T016 [FOUND] Verify `ApplicationUser` (Identity) in `src/NovaLeave.Infrastructure/Identity/ApplicationUser.cs` exists (created by spec_004 Phase 1 T002) and is linked to `Employee` per `common/data-model.md`.
- [x] T017 [FOUND] Extend `ApplicationDbContext` in `src/NovaLeave.Infrastructure/Identity/ApplicationDbContext.cs` (created by spec_004 Phase 1 T003) with `DbSet<VacationRequest>` - `DbSet<Employee>` and `DbSet<AuditRecord>` should already exist.
- [x] T018 [P] [FOUND] Add EF Core configurations `EmployeeConfiguration.cs`, `VacationRequestConfiguration.cs` (RowVersion + DB-level exclusion/overlap constraint per D-003), `AuditRecordConfiguration.cs` in `src/NovaLeave.Infrastructure/Persistence/`.
- [x] T019 [FOUND] Create **additive** EF Core migration `AddVacationRequest` (builds on top of spec_004's Identity migration) and apply to dev database.
- [x] T020 [P] [FOUND] Implement `WorkingDayCalculator` in `src/NovaLeave.Infrastructure/Services/WorkingDayCalculator.cs` (excludes Sat/Sun + holidays, uses `TimeProvider`) - D-001, FR-003.
- [x] T021 [P] [FOUND] Implement `HolidayCalendar` in `src/NovaLeave.Infrastructure/Services/HolidayCalendar.cs` reading configured holiday list.
- [x] T022 [P] [FOUND] Implement `StateTransitionAuditService` in `src/NovaLeave.Infrastructure/Services/StateTransitionAuditService.cs` (SR-008, FR-011).
- [x] T023 [FOUND] Verify the global `AutoValidateAntiforgeryTokenAttribute` filter and Identity cookie options are configured (done by spec_004 Phase 2 T011b/T010). Add any spec_001-specific middleware if needed.
- [x] T024 [P] [FOUND] Add authorization policies `OwnsRequest`, `IsAssignedApprover`, `NotSelfApproval` in `src/NovaLeave.Presentation.Web/Authorization/RequestPolicies.cs` (SR-007, FR-012).
- [x] T025 [P] [FOUND] Register DI wiring for services, repositories, validators, and `TimeProvider.System` in `src/NovaLeave.Presentation.Web/Program.cs`.
- [x] T026 [P] [FOUND] Extend `WebApplicationFactory<Program>` test fixture in `tests/NovaLeave.Presentation.Tests/Fixtures/VacationTestFixture.cs` (builds on `AuthTestFixture` from spec_004 T012b) - add seeded vacation-specific data: employee with balance, approver assignment, sample VacationRequests in various states.
- [x] T027 [P] [FOUND] Domain unit tests for foundational primitives: `tests/NovaLeave.Domain.Tests/DateRangeTests.cs`, `EmployeeBalanceTests.cs`, `VacationRequestStateMachineTests.cs`.

**Checkpoint**: Foundation ready - user story phases may begin in parallel.

---

## Phase 3: User Story 1 - Employee submits a vacation request (Priority: P1) MVP

**Goal**: Authenticated Employee can submit a vacation request that lands in `Pending` state with reserved days, server-computed working-day count, overlap/balance/date validation, and audit trail.

**Independent Test**: Log in as Employee, POST valid request, verify `Pending` row exists with correct dates, reason preserved, days reserved (not deducted), audit record created; also verify all rejection paths (past/today start, inverted range, overlap, insufficient balance, unauthenticated).

### Tests for User Story 1 (write first, must FAIL before implementation)

- [x] T100 [P] [US1] Domain tests `tests/NovaLeave.Domain.Tests/VacationRequestSubmitTests.cs` - submit invariants, working-day count, reserve behavior.
- [x] T101 [P] [US1] Handler tests `tests/NovaLeave.Application.Tests/CreateRequestHandlerTests.cs` - validation, overlap short-circuit, balance short-circuit, audit invocation.
- [x] T102 [P] [US1] Integration tests `tests/NovaLeave.Presentation.Tests/VacationRequest/CreateFlowTests.cs` - happy path, inverted range, past/today start, overlap, insufficient balance, unauthenticated redirect, anti-forgery rejection (SR-001), over-posting rejection (SR-002).
- [x] T103 [P] [US1] Concurrency test `tests/NovaLeave.Presentation.Tests/VacationRequest/ConcurrencyTests.cs::OverlapUnderConcurrency_OnlyOneSucceeds` verifying DB exclusion constraint (D-003, SC-006).
- [x] T104 [P] [US1] E2E test `tests/NovaLeave.E2E.Tests/VacationRequest/SubmitRequestE2ETests.cs` (C# xUnit + Playwright) - employee logs in, submits form, sees new request in history as `Pending`.

### Dashboard Bridge (spec_004 → spec_001)

> **Context**: spec_004 created placeholder Razor Pages at `/Employee/Dashboard` and `/Approver/Dashboard` with empty `OnGet()`. spec_003 defines their full HTML structure. These tasks replace the placeholders with real content while preserving the routes that spec_004 login tests already verify.

- [x] T108 [US1] Refactor `src/NovaLeave.Presentation.Web/Pages/Employee/Dashboard.cshtml.cs` — inject `ListMyRequestsHandler` (T510), implement `OnGetAsync` with status filter and pagination; keep route `/Employee/Dashboard`. The `EmployeeRequestsController` (T115) handles only Create/Edit/Detail/Cancel/Void actions — NOT the list view.
- [x] T108b [US1] Refactor `src/NovaLeave.Presentation.Web/Pages/Employee/Dashboard.cshtml` — replace placeholder with spec_003 structure: `BalanceCard`, request table (desktop) + `RequestCard` list (mobile), `StatusBadge`, `PaginationControl`, `EmptyState`, status filter dropdown.
- [x] T109 [US1] Refactor `src/NovaLeave.Presentation.Web/Pages/Approver/Dashboard.cshtml.cs` — inject `ListPendingForApproverHandler` (T212), implement `OnGetAsync` with urgency ordering and pagination; keep
- [x] T109b [US1] Refactor `src/NovaLeave.Presentation.Web/Pages/Approver/Dashboard.cshtml` — replace placeholder with spec_003 structure: `stat-card` (pending count, urgent count), pending requests table (desktop) + `RequestCard` list (mobile, approver variant), `PaginationControl`, `EmptyState`.

### Implementation for User Story 1

- [x] T110 [P] [US1] Implement `OverlapChecker` domain service in `src/NovaLeave.Domain/Services/OverlapChecker.cs`.
- [x] T111 [P] [US1] Command + validator: `src/NovaLeave.Application/Features/VacationRequest/Create/CreateRequestCommand.cs`, `CreateRequestValidator.cs` (FluentValidation: dates, reason non-empty).
- [x] T112 [US1] Handler `src/NovaLeave.Application/Features/VacationRequest/Create/CreateRequestHandler.cs` - revalidates identity/balance/overlap (FR-012), calls `IWorkingDayCalculator`, persists atomically with `AuditRecord` (SR-008, FR-011).
- [x] T113 [P] [US1] Repository method `AddAsync`/`ExistsOverlappingAsync`/`GetByIdAsync` in `src/NovaLeave.Infrastructure/Persistence/VacationRequestRepository.cs`. **Note**: `GetByIdAsync` MUST include `.Include(r => r.Owner)` to eagerly load the Owner navigation property - required by ApproverRequestsController (T214) to access Owner.AssignedApproverId, Owner.Name, Owner.Email, and Owner.Balance without NullReferenceException.
- [x] T114 [P] [US1] ViewModel `src/NovaLeave.Presentation.Web/ViewModels/CreateRequestViewModel.cs` (only StartDate, EndDate, Reason - SR-002).
- [x] T115 [US1] Controller actions `Create GET` and `Create POST` in `src/NovaLeave.Presentation.Web/Controllers/EmployeeRequestsController.cs` (`[Authorize]`, PRG pattern, antiforgery).
- [x] T116 [P] [US1] Razor view `src/NovaLeave.Presentation.Web/Views/EmployeeRequests/Create.cshtml` (form aligned with `spec_003-screen-construction-guide.md` standard authenticated layout).
- [x] T117 [US1] Wire logging + audit correlation-id enrichment for create flow.

**Checkpoint**: US1 fully functional. Employee can submit requests end-to-end; all validation, audit, and security paths tested independently. **This is the MVP milestone.**

---

## Phase 4: User Story 2 - Approver approves or rejects a request (Priority: P1)

**Goal**: Assigned Approver views a Pending request, approves (deducting balance) or rejects (with mandatory reason), atomically with audit record; self-approval and cross-team access blocked; concurrent decisions resolved by optimistic concurrency.

**Independent Test**: Seed request from Employee-A assigned to Approver-B; login as B - approve - verify `Approved`, balance decremented, audit row. Repeat as rejection with reason - verify `Rejected`, balance intact. Verify self-approval (A tries to approve A's own request) blocked; verify unrelated approver gets 404 (SR-003).

### Tests for User Story 2

- [x] T200 [P] [US2] Domain tests `tests/NovaLeave.Domain.Tests/VacationRequestApproveRejectTests.cs`.
- [x] T201 [P] [US2] Handler tests `tests/NovaLeave.Application.Tests/ApproveRequestHandlerTests.cs` and `RejectRequestHandlerTests.cs`.
- [x] T202 [P] [US2] Integration test `tests/NovaLeave.Application.Tests/ApprovalFlowTests.cs` - approve happy path, reject with/without reason (SR-006), non-Pending guard, self-approval denied, unassigned-approver gets 404 (SR-003).
- [x] T203 [P] [US2] Concurrency test `tests/NovaLeave.Application.Tests/ConcurrencyTests.cs::TwoApprovals_OnlyOneWins` (optimistic concurrency, SC-006).
- [x] T204 [P] [US2] Authorization test `tests/NovaLeave.Application.Tests/AuthorizationTests.cs` - IDOR on approve/reject endpoints.
- [x] T205 [P] [US2] Audit test `tests/NovaLeave.Application.Tests/AuditTests.cs` - approve/reject transitions produce audit records with all fields (SC-005).
- [ ] T206 [P] [US2] E2E test `tests/NovaLeave.E2E.Tests/VacationRequest/ApproveRejectE2ETests.cs` (C# xUnit + Playwright) including confirmation modal (SR-005).

### Implementation for User Story 2

- [x] T210 [P] [US2] `ApproveRequestCommand.cs` + `ApproveRequestHandler.cs` in `src/NovaLeave.Application/Features/VacationRequest/Approve/` - transitions to `Approved`, calls `Employee.DeductDays`, guards negative balance (FR-006), writes audit.
- [x] T211 [P] [US2] `RejectRequestCommand.cs`, `RejectRequestValidator.cs` (reason required, SR-006), `RejectRequestHandler.cs` in `src/NovaLeave.Application/Features/VacationRequest/Reject/` - transitions to `Rejected`, releases reservation, writes audit.
- [x] T212 [P] [US2] Query + handler `ListPendingForApproverQuery.cs` / `Handler.cs` in `src/NovaLeave.Application/Features/VacationRequest/List/` - paginated (SC-007), filtered by `AssignedApproverId`.
- [x] T213 [P] [US2] ViewModels `src/NovaLeave.Presentation.Web/ViewModels/RejectRequestViewModel.cs` and `RequestDetailViewModel.cs` (owner/approver only expose `Reason` - FR-010, SR-010).
- [x] T214 [US2] `ApproverRequestsController.cs` in `src/NovaLeave.Presentation.Web/Controllers/` with actions `Index` (pending queue), `Detail`, `Approve POST`, `Reject POST` - enforces `IsAssignedApprover` + `NotSelfApproval` policies (SR-007), returns 404 for existence-hiding (SR-003).
- [x] T215 [P] [US2] Views `src/NovaLeave.Presentation.Web/Views/ApproverRequests/Index.cshtml`, `Detail.cshtml`, and `_ConfirmationModal.cshtml` (explicit confirmation, SR-005/FR-017).

**Checkpoint**: US1 + US2 form the end-to-end core loop - submit -> approve/reject -> balance updated. Deploy-ready MVP.

---

## Phase 5: User Story 3 - Employee cancels a pending request (Priority: P2)

**Goal**: Owner can cancel own `Pending` request -> `Cancelled` (final), reservation released, audit recorded.

**Independent Test**: Login as owner of a Pending request, cancel, verify `Cancelled`, days released, absent from approver queue, cannot re-transition. Non-owner cancel attempt denied.

### Tests for User Story 3

- [x] T300 [P] [US3] Domain tests `tests/NovaLeave.Domain.Tests/VacationRequestCancelTests.cs`.
- [x] T301 [P] [US3] Handler tests `tests/NovaLeave.Application.Tests/CancelRequestHandlerTests.cs`.
- [ ] T302 [P] [US3] Integration test `tests/NovaLeave.Presentation.Tests/VacationRequest/CancellationTests.cs` - owner cancels, non-owner denied, non-Pending state guard, terminal state enforcement, confirmation required (SR-005).
- [ ] T303 [P] [US3] Integration test `tests/NovaLeave.Presentation.Tests/VacationRequest/CancellationTests.cs::CancelWithoutConfirmation_IsRejected` - verifies cancel action is not executed without explicit confirmation (SR-005).
- [ ] T304 [P] [US3] E2E test `tests/NovaLeave.E2E.Tests/VacationRequest/CancelRequestE2ETests.cs` (C# xUnit + Playwright) - employee cancels pending request, verifies `Cancelled` status in history and days released.

### Implementation for User Story 3

- [x] T310 [P] [US3] `CancelRequestCommand.cs` + `CancelRequestHandler.cs` in `src/NovaLeave.Application/Features/VacationRequest/Cancel/` - transitions Pending -> Cancelled, releases reservation, writes audit.
- [x] T311 [US3] Add `Cancel POST` action in `src/NovaLeave.Presentation.Web/Controllers/EmployeeRequestsController.cs` (owner policy, antiforgery, confirmation).
- [ ] T312 [P] [US3] Reuse `_ConfirmationModal.cshtml` on Employee detail view for cancellation (SR-005).

**Checkpoint**: US3 independently functional; US1+US2+US3 all work together.

---

## Phase 6: User Story 4 - Employee voids an approved request (Priority: P2)

**Goal**: Owner voids `Approved` request when `StartDate > today` -> `Voided` (final), days returned to balance, audit recorded. Voiding after start date is rejected.

**Independent Test**: Seed `Approved` with future start - owner voids - verify `Voided`, balance restored, audit row. Seed `Approved` with past start - void denied. Non-owner denied.

### Tests for User Story 4

- [x] T400 [P] [US4] Domain tests `tests/NovaLeave.Domain.Tests/VacationRequestVoidTests.cs` (uses `TimeProvider` fake).
- [x] T401 [P] [US4] Handler tests `tests/NovaLeave.Application.Tests/VoidRequestHandlerTests.cs`.
- [ ] T402 [P] [US4] Integration test `tests/NovaLeave.Presentation.Tests/VacationRequest/VoidFlowTests.cs` - future start success, past start denied, non-owner denied, confirmation required.
- [ ] T403 [P] [US4] E2E test `tests/NovaLeave.E2E.Tests/VacationRequest/VoidRequestE2ETests.cs` (C# xUnit + Playwright) - employee voids approved request with future start, verifies `Voided` status and balance restored in UI.
- [ ] T404 [P] [US4] Concurrency test `tests/NovaLeave.Presentation.Tests/VacationRequest/ConcurrencyTests.cs::DoubleVoid_OnlyOneSucceeds` - verifying optimistic concurrency on void (FR-013).
- [ ] T405 [P] [US4] Authorization test `tests/NovaLeave.Presentation.Tests/VacationRequest/AuthorizationTests.cs::VoidIDOR` - non-owner void attempt returns 404 (SR-003).

### Implementation for User Story 4

- [x] T410 [P] [US4] `VoidRequestCommand.cs` + `VoidRequestHandler.cs` in `src/NovaLeave.Application/Features/VacationRequest/Void/` - validates `StartDate > TimeProvider.Now`, transitions Approved -> Voided, `Employee.RestoreDays`, writes audit (FR-015).
- [x] T411 [US4] Add `Void POST` action in `EmployeeRequestsController.cs` (owner policy, confirmation, SR-005).

**Checkpoint**: US4 independently functional.

---

## Phase 7: User Story 5 - Employee views own request history and balance (Priority: P2)

**Goal**: Employee sees paginated history of own requests + current balance; cannot access other employees' history (SR-003).

**Independent Test**: Login as Employee, GET history - see only own requests with correct status and current balance. Attempt to access another employee's history - 404. Seed >50 requests - verify pagination (SC-007).

### Tests for User Story 5

- [x] T500 [P] [US5] Handler tests `tests/NovaLeave.Application.Tests/ListMyRequestsHandlerTests.cs`.
- [ ] T501 [P] [US5] Integration test `tests/NovaLeave.Presentation.Tests/VacationRequest/HistoryViewTests.cs` - own-only, existence hiding on cross-employee access, pagination limits, `Reason` visibility rules (FR-010, SR-010).
- [ ] T502 [P] [US5] E2E test `tests/NovaLeave.E2E.Tests/VacationRequest/HistoryE2ETests.cs` (C# xUnit + Playwright).

### Implementation for User Story 5

- [x] T510 [P] [US5] `ListMyRequestsQuery.cs` + handler in `src/NovaLeave.Application/Features/VacationRequest/List/` (limit/offset pagination).
- [x] T511 [P] [US5] `RequestListViewModel.cs` in `src/NovaLeave.Presentation.Web/ViewModels/` (excludes `Reason` from list per SR-010).
- [x] T512 [US5] Add `Index` + `Detail` actions in `EmployeeRequestsController.cs` (owner-only detail; 404 on unauthorized detail - SR-003).
- [ ] T513 [P] [US5] Views `src/NovaLeave.Presentation.Web/Views/EmployeeRequests/Index.cshtml` (history + balance) and `Detail.cshtml`.

**Checkpoint**: All P1 + P2 user stories independently functional.

---

## Phase 8: Cross-Cutting - Edit Pending, Auto-Expiry, Audit Hardening

**Purpose**: Requirements from `plan.md` that aren't tied to a single user-visible story but are mandated by FR-009, FR-016, SR-008. Kept separate so P1 MVP can ship first.

### Edit Pending Request (FR-009)

- [x] T600 [P] Handler tests `tests/NovaLeave.Application.Tests/EditRequestHandlerTests.cs` (dates re-validated, overlap re-checked, balance re-checked, RowVersion enforced).
- [ ] T601 [P] Integration test `tests/NovaLeave.Presentation.Tests/VacationRequest/EditFlowTests.cs`.
- [ ] T601b [P] E2E test `tests/NovaLeave.E2E.Tests/VacationRequest/EditRequestE2ETests.cs` (C# xUnit + Playwright) - employee edits pending request dates, verifies updated values in detail view.
- [x] T602 [P] `EditRequestCommand.cs`, `EditRequestValidator.cs`, `EditRequestHandler.cs`
- [x] T603 `EditRequestViewModel.cs`, `Edit GET`/`Edit POST` actions in `EmployeeRequestsController.cs`, view `Views/EmployeeRequests/Edit.cshtml`.

### Auto-Expiry (FR-016, D-005, D-006)

- [x] T610 [P] Domain tests `tests/NovaLeave.Domain.Tests/VacationRequestExpiryTests.cs`.
- [ ] T611 [P] Integration test `tests/NovaLeave.Presentation.Tests/VacationRequest/AutoExpiryTests.cs` (deterministic fake `TimeProvider`, SC-008).
- [ ] T612 Implement `AutoExpiryJob : IHostedService` in `src/NovaLeave.Infrastructure/BackgroundJobs/AutoExpiryJob.cs` - reads `EXPIRY_DAYS` from configuration, transitions eligible Pending -> Expired, releases reservation, writes audit atomically.
- [ ] T613 Register `AutoExpiryJob` and `EXPIRY_DAYS` config binding in `Program.cs`.
- [ ] T614 [P] E2E test `tests/NovaLeave.E2E.Tests/VacationRequest/AutoExpiryE2ETests.cs` (C# xUnit + Playwright) - seed pending request past expiry threshold with fake `TimeProvider`, trigger job, verify `Expired` status in employee history view.

### Audit & Security Hardening

- [ ] T620 [P] Audit sweep test `tests/NovaLeave.Presentation.Tests/VacationRequest/AuditingTests.cs::EveryTransitionProducesAudit` covering Create/Approve/Reject/Cancel/Void/Expire/Edit (SC-005, SR-008).
- [ ] T621 [P] Security test `tests/NovaLeave.Presentation.Tests/VacationRequest/SecurityHeadersAndCookiesTests.cs` verifying cookie flags (SR-009) and CSRF enforcement (SR-001).
- [ ] T622 [P] Over-posting/session-revalidation tests `tests/NovaLeave.Presentation.Tests/VacationRequest/SessionRevalidationTests.cs` (SR-002, SR-004).

### Audit Immutability, Retention, and History Query (CU-302)

- [ ] T640 [P] Add EF Core migration `ProtectAuditRecordImmutability` in `src/NovaLeave.Infrastructure/Persistence/Migrations/` with a SQL Server trigger on the `AuditRecord` table that throws on any `UPDATE` or `DELETE`; write integration test `tests/NovaLeave.Presentation.Tests/Audit/ImmutabilityTests.cs` that issues raw SQL `UPDATE`/`DELETE` against an existing `AuditRecord` and asserts both are rejected, and that a failed attempt is logged as a security alert (SR-008, CU-302 RN-003, RN-008, CA-003, CA-011, FE-002).
- [ ] T641 [P] Implement `ListAuditQuery` + `ListAuditQueryHandler` in `src/NovaLeave.Application/Features/VacationRequest/Audit/` returning audit records for a single request in reverse chronological order, authorized by ownership (Employee) or approver assignment (Approver), with server-side pagination at 50 records (CU-302 RN-006, RN-009, FP-06, FP-07, CA-004, CA-005, CA-006, CA-009, FE-004).
- [ ] T642 [P] Add `AuditHistory` GET action + view on `src/NovaLeave.Presentation.Web/Controllers/EmployeeRequestsController.cs` (owner) and `ApproverRequestsController.cs` (assigned approver) rendering the audit timeline most-recent-first with `PaginationControl` when more than 50 records and `EmptyState` when empty (CU-302 FP-07, RN-009, CA-009, FE-004).
- [ ] T643 [P] Retention test `tests/NovaLeave.Presentation.Tests/Audit/RetentionTests.cs` asserting audit records persist at least 7 years (no physical deletion path, Constitution section 13) and that no application code or migration exposes `DELETE` for `AuditRecord` (CU-302 RN-005, CA-010).
- [ ] T644 [P] Masking test `tests/NovaLeave.Presentation.Tests/Audit/PiiMaskingTests.cs` asserting `Details` JSON for `RequestCreated`, `RequestRejected`, and `RequestEdited` never contains the `reason` or `rejectionReason` values (SR-010, CU-302 RN-007, FA-002, CA-007).

### Role Switcher (CU-202)

- [x] T644b [P] Implement `RoleSwitcherViewComponent.cs` in `src/NovaLeave.Presentation.Web/ViewComponents/` with `Invoke()` method that checks for multiple roles and returns `RoleSwitcherViewModel` with available roles and current path; create view `Views/Shared/Components/RoleSwitcher/Default.cshtml` with dropdown UI, inline styles, and **inline JavaScript** (NOT in `@section Scripts` - ViewComponents cannot use sections). JavaScript must use IIFE pattern `(function(){ ... })()` to execute immediately when component renders. Handles click events, ARIA attributes, and navigation to role dashboards.
- [x] T645 [P] E2E test `tests/NovaLeave.E2E.Tests/Shared/RoleSwitcherE2ETests.cs` (C# xUnit + Playwright) covering: switcher visible only for dual-role users, switching navigates to the matching dashboard (`/Employee/Dashboard` or `/Approver/Dashboard`), sidebar links rebuild, ARIA attributes (`aria-haspopup`, `aria-expanded`) update, and a single-role user navigating directly to the other role's route is denied server-side (CU-202 CA-001, CA-002, CA-003, CA-004, CA-005, CA-006, FE-001).

### Approver Dashboard & Auto-Expiry Verification (CU-101, CU-301)

- [ ] T646 [P] E2E test `tests/NovaLeave.E2E.Tests/Approver/ApproverDashboardE2ETests.cs` (C# xUnit + Playwright) covering urgency ordering (nearest expiry first), own requests excluded from the queue, empty-state rendering, and server-side pagination at 50 records (CU-101 RN-001, RN-002, RN-006, CA-002, CA-003, CA-004, CA-005).
- [ ] T647 [P] Auto-expiry robustness test `tests/NovaLeave.Presentation.Tests/VacationRequest/AutoExpiryConfigTests.cs` asserting the job is idempotent (double execution produces no duplicate transitions) and that an invalid or missing `EXPIRY_DAYS` configuration logs a critical error and skips execution without touching requests (CU-301 RN-006, FE-003, CA-002, CA-008).

**Checkpoint**: All FRs and SRs from the spec are exercised by automated tests, and CU-101, CU-202, CU-301, CU-302 have explicit automated coverage.

---

## Phase 9: Polish

- [ ] T700 [P] [POLISH] Update `.specify/specs/001-vacation-request/quickstart.md` with local run + seeded credentials.
- [ ] T701 [P] [POLISH] Add developer README section in `src/NovaLeave.Presentation.Web/README.md` describing feature routes and policies.
- [ ] T702 [POLISH] Run full test suite (`dotnet test`) - verify all US-scoped checkpoints and cross-cutting tests are green.
- [ ] T703 [POLISH] Run Playwright suite end-to-end against a freshly-migrated database.
- [ ] T704 [POLISH] Verify success criteria SC-001..SC-009 have at least one corresponding automated test.
- [ ] T705 [P] [POLISH] Serilog log-review pass: ensure no PII/`Reason` leakage (SR-008, SR-010).
- [ ] T706 [P] [POLISH] Code coverage verification: run `dotnet test --collect:"XPlat Code Coverage"` and verify Domain and Application projects meet **>= 80%** line coverage (Constitution §9.2, `architecture.md`). Infrastructure/Presentation must be **>= 60%** measured.
- [ ] T707 [P] [POLISH] Code documentation audit: verify all public interfaces, domain entities, application handlers, validators, and controller actions have inline comments explaining intent and rationale per `architecture.md` Code Documentation Standards.

---

## Phase 10: Missing Views, Navigation & Route Fixes (spec_003 compliance)

**Purpose**: Close the gaps found in the navigation/views audit (2026-08-05): MVC views that existing controller actions return but that don't exist, redirects targeting non-existent routes, sidebar links that 404, the missing 404 page, and layouts so every view renders inside the spec_003 sidebar shell. No new business logic — only presentation/routing fixes.

**Prerequisite**: Phases 3-8 complete (controllers, handlers, and ViewModels exist). Requires the spec_003 shared layout + component partials (spec_003 T074, T075, T076-T084).

### Missing MVC Views (returned by existing actions)

- [x] T800 [NAV] Create `src/NovaLeave.Presentation.Web/Views/EmployeeRequests/Index.cshtml` (model `RequestListViewModel`) — history + balance per spec_003 §3.2: `BalanceCard`, status filter, desktop table (Dates/Days/Reason/Shipped/Status/Actions) + mobile `RequestCard` list, `StatusBadge`, `PaginationControl`, `EmptyState`. Fixes `EmployeeRequestsController.Index` returning `View()` with no matching file. Completes T513.
- [x] T801 [NAV] Create `src/NovaLeave.Presentation.Web/Views/EmployeeRequests/Detail.cshtml` (model `EmployeeRequestDetailViewModel`) — detail per spec_003 §3.4: `StatusBadge`, `detail-list` summary, conditional actions (Cancel if `CanCancel`, Void if `CanVoid`, Edit link if Pending), `_ConfirmationModal` for cancel/void (SR-005), rejection-reason row hidden when empty. Fixes `EmployeeRequestsController.Detail` returning `View()` with no matching file. Completes T513 + T312.
- [x] T802 [NAV] Create `src/NovaLeave.Presentation.Web/Views/EmployeeRequests/Edit.cshtml` (model `EditRequestViewModel`) — edit Pending form per spec_003 §3.5: `BalanceCard`, pre-filled `FormField`s (dates + reason), computed-days preview, hidden `Id`, POST to `EditRequest`. Fixes `EmployeeRequestsController.Edit` GET/POST returning `View()` with no matching file. **Note**: T603 is marked done but the view is missing — this task closes that gap.

### Missing Controller Action / Route

- [x] T803 [NAV] Create `src/NovaLeave.Presentation.Web/Controllers/HomeController.cs` with `Error` action + view `Views/Home/Error.cshtml` (404 page per spec_003 §3.9: error-card with code, "Go to Home" link, no sidebar layout); register a 404-only status-code re-execute to `/Home/Error` in `Program.cs`. Today the default route `{controller=Home}` points to a non-existent `HomeController` and only `UseExceptionHandler("/Error")` (exceptions, not 404s) is configured.
- [x] T804 [NAV] Fix redirect targets in `src/NovaLeave.Presentation.Web/Controllers/EmployeeRequestsController.cs`:
  - `Create` success → `RedirectToPage("/Employee/Dashboard")` (currently `RedirectToAction("Index", "Employee", new { area = "" })` → `/Employee/Index`, 404)
  - `Cancel` / `Void` success and failure → `RedirectToPage("/Employee/Dashboard")` (currently `RedirectToAction("Index", "Dashboard", new { area = "Employee" })` → `/Employee/Dashboard/Index`, 404)

### Navigation Link Fixes

- [x] T805 [NAV] Fix sidebar links in `Views/Shared/_Layout.cshtml` and `Views/Shared/Components/_Sidebar.cshtml`: `/Employee/Requests/Create` → `/EmployeeRequests/Create` and `/Employee/Requests` → `/EmployeeRequests/Index` (current targets 404; there is no `EmployeeController`). Keep `/Employee/Dashboard`, `/Approver/Dashboard`, `/Approver/Requests` (already resolve).

### Layouts (spec_003 §3.8 — every view inside the sidebar shell)

- [x] T806 [NAV] Create `src/NovaLeave.Presentation.Web/Views/_ViewStart.cshtml` (`@{ Layout = "_Layout"; }`) so MVC views (`EmployeeRequests/*`, `ApproverRequests/*`, `Home/*`) apply the spec_003 sidebar layout. Without it these views render layout-less (no navigation, no logout).
- [x] T807 [NAV] Make `/Employee/Dashboard` and `/Approver/Dashboard` render the spec_003 sidebar layout: point `Pages/_ViewStart.cshtml` at `Layout = "~/Views/Shared/_Layout.cshtml"` (or refactor `Pages/Shared/_Layout.cshtml` to the spec_003 structure). Current `Pages/Shared/_Layout.cshtml` is the scaffold template — references nonexistent `~/lib/bootstrap/dist/` CSS and `~/js/site.js`, and has no sidebar.
- [x] T808 [P] [NAV] Verification pass: confirm `Views/Shared/_Layout.cshtml` renders correctly under both MVC and Razor Pages (sections declared `required: false`; `TempData` alerts and `_LogoutButton` render; `shared.js` sidebar toggle works on both).

**Checkpoint — Phase 10 Verification (Navigation & Views)**:
- [x] `GET /EmployeeRequests` renders history with `BalanceCard`, table/cards, `PaginationControl`, `EmptyState`
- [x] `GET /EmployeeRequests/Detail/{id}` renders detail with conditional Cancel/Void/Edit actions + `_ConfirmationModal`
- [x] `GET /EmployeeRequests/Edit/{id}` renders the pre-filled Pending-only edit form
- [x] All MVC views and both dashboards render with the spec_003 sidebar (`nav.sidebar` present, no scaffold navbar)
- [x] `Create` / `Cancel` / `Void` POST redirect to `/Employee/Dashboard` (HTTP 302 to an existing route)
- [x] Sidebar links resolve without 404: `/EmployeeRequests/Create`, `/EmployeeRequests/Index`, `/Approver/Requests`
- [x] Unmatched route (e.g. `/Nope`) returns the 404 page with HTTP 404
- [x] `dotnet build` passes and existing spec_001 + spec_004 tests stay green

---

## Dependencies & Execution Order

### Phase Dependencies

- **spec_004 Phase 0 (Solution Scaffold)** - MUST be complete before any spec_001 work begins. Creates projects, shared entities, packages, and infrastructure.
- **spec_004 Phase 1-2 (Identity + Foundational)** - MUST be complete before spec_001 Phase 2. Creates ApplicationUser, ApplicationDbContext, Identity config, cookie config, rate limiting.
- **Phase 1 (Setup)** - depends on spec_004 Phase 0. Verifies scaffold, adds Domain.Tests, configures Serilog.
- **Phase 2 (Foundational)** - depends on Phase 1 + spec_004 Phase 1-2. Blocks all US phases.
- **Phase 3 (US1)** and **Phase 4 (US2)** - depend on Phase 2. Both are P1; US2 depends on US1's `VacationRequest` create path being persistable (may share seed data). Can be developed in parallel by different developers once Phase 2 done.
- **Phase 5 (US3)**, **Phase 6 (US4)**, **Phase 7 (US5)** - depend on Phase 2; can proceed in parallel with each other and with US1/US2 (they share entities but touch disjoint handlers/controllers).
- **Phase 8 (Cross-Cutting)** - depends on Phase 2; Edit/Auto-Expiry can start once entities exist; audit sweep test runs after US1+US2 exist. T640/T641 depend on the audit write path (T022) and `AuditRecord` configuration (T018). T644b/T645 (Role Switcher implementation and tests) depend on the shared layout from spec_003 being available and properly rendering ViewComponents.
- **Phase 9 (Polish)** - depends on all above.
- **Phase 10 (Missing Views & Navigation)** - depends on Phases 3-8 (controllers, handlers, and ViewModels already exist); can run in parallel with Phase 9. Requires the spec_003 shared layout (spec_003 T074/T075) and component partials (T076-T084).

### Within Each User Story

- Tests are written first and must FAIL before implementation begins.
- Domain - Application - Infrastructure - Presentation.
- ViewModels/Views may run in parallel with handler impl (different files).

### Parallel Opportunities

- Setup: T002 can proceed in parallel with any spec_004 Phase 0 follow-up tasks.
- Foundational: T011, T012, T014, T015, T018, T020, T021, T022, T024, T025, T026, T027 all parallel-safe.
- US1: T100-T104 parallel; T110, T111, T113, T114, T116 parallel.
- US2: T200-T206 parallel; T210, T211, T212, T213, T215 parallel.
- US3-US5: all test tasks and independent handler/viewmodel tasks parallel-safe.
- Phase 8: T600-T647 are parallel-safe (distinct files) once T022 and the `AuditRecord` configuration exist.

---

## Implementation Strategy

### MVP First (US1 + US2)

1. Complete Phase 1 (Setup).
2. Complete Phase 2 (Foundational) - CRITICAL blocker.
3. Complete Phase 3 (US1) and Phase 4 (US2) - end-to-end submit -> approve/reject loop.
4. **STOP and VALIDATE**: run integration + E2E, demo MVP.

### Incremental Delivery

1. MVP (US1 + US2) - deploy/demo.
2. Add US3 (cancel) - deploy/demo.
3. Add US4 (void) - deploy/demo.
4. Add US5 (history + balance view) - deploy/demo.
5. Add Cross-Cutting (Edit + Auto-Expiry) - deploy/demo.
6. Add Audit Immutability, Retention, and History Query (CU-302).
7. Add Role Switcher verification and Approver Dashboard/Auto-Expiry robustness coverage.
8. Polish.

### Parallel Team Strategy

Once Foundational is done:

- Dev A - US1
- Dev B - US2
- Dev C - US3 + US4
- Dev D - US5 + Auto-Expiry job

---

## Notes

- `[P]` tasks = different files, no dependencies.
- Every `[Story]` label ties the task to a spec user story for traceability to acceptance scenarios and SC-* criteria.
- Common artifacts (`architecture.md`, `data-model.md`, `security.md`) are the single source of truth - do not redefine here.
- Every state-changing action must go through confirmation UI (SR-005/FR-017) and produce an `AuditRecord` (SR-008/FR-011).
- Use `TimeProvider` everywhere date-of-today logic runs (Constitution section 2.VI); tests use a fake to control time deterministically.
- Verify each test fails BEFORE implementing the corresponding production code.
- Commit at each task or logical group; stop at any checkpoint to validate the story independently.
