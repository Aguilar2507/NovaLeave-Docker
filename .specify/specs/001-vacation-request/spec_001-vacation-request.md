# Feature Specification: Leave/Vacation Request Management — Core MVP

**Feature Branch**: `001-vacation-request-core`
**Created**: 2026-07-15
**Last Updated**: 2026-07-22
**Status**: Draft — Ready for Implementation
**Constitution Reference**: NovaLeave — Constitution v3.0.0

## Overview

This specification covers the core workflow for NovaLeave MVP: an Employee submits a vacation request, their assigned Approver approves or rejects it, and the Employee can track their own requests and balance. It resolves the functional behavior required by Constitution §4 (Actors) and §5 (Invariants and Lifecycle); it does not redefine those rules, only operationalizes them into testable requirements.

**Out of scope for this spec**: calendar days vs. working days/holidays (already resolved — only working days count), medical/legal exceptions, multiple pending requests per employee, half-day/hourly requests, balance accrual and carryover, manager delegation, retroactive corrections, company-wide blackout periods, post-approval changes (cancellation/amendment), HR read-only history/balance views, HR profile configuration and manager assignment, and **in-app notifications**. These require their own specifications before implementation.

---

## Key PO Decisions (Documented)

The following decisions were made by the Product Owner and are incorporated into this specification:

- **Only vacation requests**: Only vacation/leave requests are managed in this iteration.
- **Single balance pool**: All vacation days belong to a single accumulated balance.
- **Accrual**: 1 day per full month worked, from the start of employment.
- **Unused days accumulate**: Days not used in the year are carried over.
- **No negative balances**: The system prevents requests that would leave a negative balance.
- **Minimum request**: 1 day.
- **Maximum request**: The employee's available balance at the time of request.
- **Working days only**: Saturdays, Sundays, and public holidays are not counted as vacation days.
- **Date range selection**: The employee selects a start and end date (range).
- **Single timezone**: All employees share the same timezone.
- **No same-day requests**: The start date must be strictly future (cannot be today).
- **No minimum notice**: There is no minimum advance notice requirement.
- **Pending status**: Days are temporarily reserved but not deducted from the balance.
- **Editable while Pending**: The employee can edit the request while it is in `Pending` state.
- **Overlap prevention**: Requests cannot overlap with existing `Pending` or `Approved` requests.
- **Auto‑expiry**: If not approved within `N` working days (parametrized), the request expires (`Expired` state) and days are released.
- **No escalation**: There is no escalation or reassignment of pending requests.
- **Approval table**: A separate table defines which Approver is assigned to each Employee.
- **HR does not configure users**: Approver assignments are assumed to be pre‑loaded or managed externally.
- **Approvers can request**: Approvers can submit vacation requests, but cannot approve their own requests.
- **User status**: Employees have an `Active` or `Inactive` status.
- **Approved is final**: Once approved, the request cannot be modified.
- **Cancellation of approved requests**: Approved requests can only be cancelled (voided) if the vacation period has not started. Days are returned to the balance.
- **No cancellation once started**: Cancellation is not allowed once the start date has passed.
- **No partial cancellations**: Cancellations are full only; partial reductions are not allowed.
- **Rejection reason required**: The Approver must provide a reason when rejecting a request.
- **Audit trail**: Complete traceability from creation to resolution.
- **No reports**: Reporting functionality is not required for the MVP.
- **No external integrations**: No integrations with external systems.
- **Session management**: Basic sessions with ASP.NET Core Identity; session expiry and single‑session behavior are handled via Identity.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Employee submits a vacation request (Priority: P1)

As an **Employee**, I want to submit a vacation request with start date, end date, and a reason, so that my assigned Approver can review and resolve it.

**Why this priority**: This is the entry point of the entire workflow — without it, no other story can be exercised. Independently valuable: an Employee can see their request move into `Pending` even before an approval capability exists.

**Independent Test**: Log in as an Employee, submit a request with valid data, and verify it appears in the Employee's own history in `Pending` state with the submitted dates and reason preserved.

**Acceptance Scenarios**:

1. **Given** an authenticated Employee, **When** they submit a request with a valid start date, end date (start < end), and reason, **Then** the system shall create the request in `Pending` state, reserve the requested days temporarily (not deduct from balance), and record the actor, timestamp, and submitted data in the audit trail.
2. **Given** an authenticated Employee, **When** the submitted start date is later than the end date, **Then** the system shall reject the submission and return a validation error identifying the invalid field.
3. **Given** an authenticated Employee, **When** the submitted start date is the current day or in the past, **Then** the system shall reject the submission without creating a request.
4. **Given** an Employee with an existing `Pending` or `Approved` request, **When** they submit a date range that overlaps it — including via near-simultaneous submissions (e.g. double-click, multiple tabs) — **Then** the system shall reject the submission and shall not create a duplicate or conflicting request, enforced by both application-level validation and a database-level exclusion constraint (per D-003).
5. **Given** an Employee whose available balance is insufficient for the requested day count, **When** they submit a request, **Then** the system shall reject the submission before creating the request and indicate that the balance is insufficient.
6. **Given** a successfully created request, **When** the system calculates the duration, **Then** it shall compute the requested day count server-side as working days (excluding Saturdays, Sundays, and public holidays) and shall not accept a client-supplied day count as authoritative.
7. **Given** an unauthenticated user, **When** they attempt to submit a request, **Then** the system shall deny the action and redirect to authentication.

---

### User Story 2 - Approver approves or rejects a request (Priority: P1)

As an **Approver**, I want to review a `Pending` request from an employee assigned to me and approve or reject it, so that the workflow reaches a resolution and, if approved, the employee's balance reflects the decision.

**Why this priority**: Without resolution, requests accumulate indefinitely and balances never change — this is the second half of the core loop and is equally load-bearing as Story 1.

**Independent Test**: As an Approver, open a `Pending` request from an assigned employee, approve it, and verify the request becomes `Approved`, the employee's balance is decremented, and an audit record is created. Repeat for rejection and verify the balance is untouched and the rejection reason is stored.

**Acceptance Scenarios**:

1. **Given** an authorized Approver viewing an assigned employee's `Pending` request, **When** they approve it, **Then** the system shall transition the request to `Approved`, deduct the reserved days from the employee's balance, and record the transition in the audit trail within the same atomic operation.
2. **Given** an authorized Approver viewing an assigned employee's `Pending` request, **When** they reject it, **Then** the system shall transition the request to `Rejected`, release the reserved days (no balance change), store the rejection reason (mandatory), and record the transition in the audit trail.
3. **Given** a user viewing their own request, **When** they attempt to approve or reject it, **Then** the system shall deny the action regardless of any Approver role the user holds.
4. **Given** a user without an active approver relationship to the request's owner, **When** they attempt to approve or reject it, **Then** the system shall deny the action and shall not reveal whether the request exists.
5. **Given** a request not in `Pending` state, **When** an approve or reject attempt is made, **Then** the system shall reject it and throw an explicit, predictable error rather than silently no-op.
6. **Given** a single `Pending` request, **When** two approval or rejection actions are submitted concurrently, **Then** the system shall allow only one to succeed and handle the conflicting attempt through optimistic concurrency control.
7. **Given** a `Pending` request, **When** an approval would cause the employee's balance to go negative, **Then** the system shall reject the approval before the transition is committed.

---

### User Story 3 - Employee cancels a pending request (Priority: P2)

As an **Employee**, I want to cancel my own request while it is still `Pending`, so that I can correct a mistake or withdraw a request that is no longer needed without involving my Approver.

**Why this priority**: Not required for the core approval loop to function, but a realistic and low-effort addition once Stories 1 and 2 exist; it prevents unnecessary Approver workload for requests the employee already knows are wrong.

**Independent Test**: As the Employee who owns a `Pending` request, cancel it and verify it transitions to `Cancelled`, is excluded from the Approver's pending queue, and cannot be reopened.

**Acceptance Scenarios**:

1. **Given** an Employee who owns a `Pending` request, **When** they cancel it, **Then** the system shall transition the request to `Cancelled`, release the reserved days, and record the transition in the audit trail.
2. **Given** a user viewing another employee's request, **When** they attempt to cancel it, **Then** the system shall deny the action.
3. **Given** a request not currently in `Pending` state, **When** a cancellation attempt is made, **Then** the system shall reject the cancellation attempt.
4. **Given** a request that reaches the `Cancelled` state, **When** further transition is attempted, **Then** the system shall prevent it, treating `Cancelled` as a final state.

---

### User Story 4 - Employee cancels (voids) an approved request (Priority: P2)

As an **Employee**, I want to cancel (void) an approved vacation request before the start date, so that I can free up days that I no longer plan to use and create a new request if needed.

**Why this priority**: Realistic addition once Stories 1 and 2 exist; allows employees to correct plans without depending on manual intervention from others.

**Independent Test**: As the Employee who owns an `Approved` request with a future start date, void it and verify the request transitions to `Voided`, the days are returned to the balance, and the request cannot be reactivated.

**Acceptance Scenarios**:

1. **Given** an Employee who owns an `Approved` request with a start date strictly in the future, **When** they request to void it, **Then** the system shall transition the request to `Voided`, return the deducted days to the employee's balance, and record the transition in the audit trail.
2. **Given** an Employee who owns an `Approved` request, **When** the start date has already passed, **Then** the system shall reject the void attempt and display a message indicating that cancellation is not allowed once the vacation period has started.
3. **Given** a user viewing another employee's request, **When** they attempt to void it, **Then** the system shall deny the action.

---

### User Story 5 - Employee views own request history and balance (Priority: P2)

As an **Employee**, I want to see my own past and current requests along with my remaining balance, so that I can plan future time off and track the status of my submissions.

**Why this priority**: Improves usability and reduces manual follow-up, but the system is functionally complete for the core loop without it once Stories 1–2 exist.

**Independent Test**: As an Employee, view the request list and confirm only their own requests and balance are visible, with accurate current status for each.

**Acceptance Scenarios**:

1. **Given** an Employee querying their request history, **When** the system processes the query, **Then** it shall return only the requests they own, along with current status and remaining balances.
2. **Given** an Employee, **When** they attempt to access another employee's history or balance, **Then** the system shall deny the action and conceal the target resource's existence (see SR-003).
3. **Given** an employee's history query returning over 50 records, **When** the system processes the dataset, **Then** it shall use server-side pagination to return results in chunks.

---

## Documented Design Decisions

The following decisions are purely technical and do not affect business policy. They are documented here to ensure implementation consistency.

- **D-001 — Working Days Calculation**: The system calculates vacation duration as working days only, excluding Saturdays, Sundays, and public holidays. The calculation is performed server-side using a configurable holiday calendar. See FR-003.

- **D-002 — Date Range Selection**: The system captures vacation requests as a date range (start and end date). Duration is derived from the range rather than pre‑calculated by the client. See FR-001 and FR-003.

- **D-003 — Overlap Prevention Under Concurrency**: The date overlap check (Story 1, Scenario 4) is reinforced with a database-level exclusion constraint (date range + `Pending`/`Approved`/`Voided` status + employee), in addition to validation in the application layer. This covers near-simultaneous duplicate submissions (double-clicks, multiple browser tabs) that application-level validation alone cannot guarantee. See FR-002 and FR-013.

- **D-004 — Temporary Reservation of Days in Pending State**: When a request enters `Pending` state, the requested days are temporarily reserved (not available for other requests) but are not deducted from the employee's balance. This reservation is released if the request is rejected, expired, cancelled, or voided, and becomes permanent (deduction) only upon approval. See FR-005 and FR-007.

- **D-005 — Auto‑Expiry of Pending Requests**: If a `Pending` request remains unresolved for `N` working days (where `N` is a configurable parameter), the system automatically transitions it to `Expired` state, releases the reserved days, and records the transition in the audit trail. See FR-017.

- **D-006 — Expired State is Final**: The `Expired` state is treated as a final state. No further transitions are permitted from `Expired`. See FR-008.

---

## Security & Privacy Requirements

The following requirements are derived from the NovaLeave Constitution (v3.0.0) and industry best practices. They are mandatory for the MVP and must be implemented alongside functional requirements. Each security requirement is testable and enforceable through automated tests.

### SR-001 — Anti-CSRF Protection

Every HTTP request that changes system state (`POST`, `PUT`, `PATCH`, `DELETE`) **must** validate an anti-forgery token (anti-CSRF) generated by the server and rendered in the form. Requests lacking a valid token **must** be rejected with an HTTP 400 or 403 error, and the action **must not** be executed.

**Testing**: Unit/integration tests must verify that requests without a valid token are rejected, and that valid tokens are accepted.

---

### SR-002 — Dedicated Input Models for State Transitions

Each state-changing operation (create, approve, reject, cancel, void) **must** receive user input through a dedicated ViewModel or command object that contains **only** the fields required for that specific operation. The system **must not** directly bind HTTP request data to domain entities, to prevent over-posting attacks.

**Testing**: Integration tests must verify that extra fields (e.g., `Status`, `Balance`, `ApproverId`) sent in the request are ignored by the model binder or rejected.

---

### SR-003 — Existence Hiding for Unauthorized Access

When an authenticated user requests a specific resource (e.g., a leave request detail view) and they are **not** authorized to view it (not the owner and not their approver), the system **must** respond in a way that makes "resource does not exist" and "resource exists but you are not authorized" indistinguishable. This applies uniformly to:
- Individual request views (by ID)
- API endpoints (if any)
- Any other direct access by resource identifier

**Implementation**: Return HTTP 404 (Not Found) for both cases. Do **not** return HTTP 403 with a distinct message, as that would leak existence information.

**Testing**: Test suites must verify that for a valid but unowned request ID, the response is identical to that for a non-existent ID.

---

### SR-004 — Session Revalidation at Execution Time

Authorization and identity **must** be revalidated at the **exact moment** a state-changing operation is processed, not at the moment the page was loaded. If the session is invalid, expired, or the user no longer holds the required role/relationship at that time, the operation **must** be rejected (fail-closed) without applying any partial changes.

**Implementation**: Every handler or controller action must validate the current session state, including authentication status, role, and approver-employee relationship, before executing business logic. This cannot rely on cached values from earlier in the same request.

**Testing**: Tests must simulate expired sessions, role removals, and approver reassignments occurring after page load, verifying that the operation is rejected.

---

### SR-005 — Explicit Confirmation for Final Actions

Approving, rejecting, cancelling, voiding, and expiry are **final and irreversible** transitions. The system **must** require an additional explicit confirmation step before executing any of these actions, separate from the initial form submission. This confirmation **must** clearly state that the action is irreversible and, for approvals, that it will affect the employee's leave balance.

**Implementation**: A confirmation modal or checkbox that is not pre‑checked, with a clear warning message. The confirmation token **must** be validated server-side before the operation executes.

**Testing**: Tests must verify that actions are not executed unless explicit confirmation is provided, and that the confirmation cannot be bypassed by direct HTTP requests.

---

### SR-006 — Mandatory Rejection Reason

When an Approver rejects a request, the system **must** require a rejection reason. The reason field must be provided and cannot be empty. See FR-004, Scenario 2.

**Testing**: Tests must verify that rejection attempts without a reason are rejected, and that the reason is stored and visible to the employee.

---

### SR-007 — Role-Based Access Separation

Only the request owner and the currently assigned Approver **must** be able to access request-specific views or execute request state transitions permitted by the workflow. No other authenticated user may gain access through direct HTTP requests, guessed routes, or hidden UI elements.

Access control **must** be enforced at the authorization layer (policies) and tested for all endpoints, including direct HTTP access attempts, not just UI hiding.

**Testing**: Test suites must verify that non-owner, non-approver accounts have zero successful invocations of request-specific view or state-changing endpoints outside their authorized scope.

---

### SR-008 — Audit Logging for All Security-Relevant Events

The system **must** log the following events in a tamper‑evident audit trail (immutable storage):
- All state transitions (approved, rejected, cancelled, voided, expired) with actor ID, timestamp, and action details.
- Failed authorization attempts (e.g., IDOR attempts, cross‑team access, self‑approval attempts).
- Failed authentication attempts (if implemented in the authentication system).

Logs **must not** contain sensitive data such as passwords, tokens, or the full `reason` field (unless explicitly authorized by a specific use case). The audit trail **must** be protected against unauthorized modification or deletion.

**Testing**: Integration tests must verify that each security event (successful and failed) generates an audit record with all required fields populated.

---

### SR-009 — Secure Cookie Configuration

Authentication cookies (ASP.NET Core Identity) **must** be configured with the following settings in production:
- `HttpOnly`: true
- `Secure`: true
- `SameSite`: `Strict` or `Lax`
- A reasonable lifetime (e.g., 8–12 hours) with sliding expiration and idle timeout (e.g., 20 minutes)
- Revocation behavior: when the user logs out or their role changes, the security stamp **must** be updated, invalidating all existing cookies

**Testing**: Test suites must verify that cookies are not readable by client‑side scripts, are only sent over HTTPS, and that role changes/logout invalidate the session.

---

### SR-010 — Data Minimization and Privacy Protection

The system **must** collect and store only the minimum data necessary for vacation management. Specifically:
- The `reason` field **must** be visible only to the request owner and their assigned Approver in the core MVP, consistent with Constitution §7.3.
- Employee profile data (approver relationships, balances) **must** be accessible only to the employee themselves and their approver within the core MVP scope.

**Testing**: Tests must verify that the `reason` field is not present in any list, export, or notification payload, and that profile data is only accessible to authorized roles.

---

### Edge Cases

- **Approver reassignment with pending requests**: What happens when an Approver is reassigned while a request is still `Pending`? (Out of scope — requires a dedicated specification.)
- **Employee account deactivation**: What happens if the Employee's account is deactivated while a request is `Pending`? (Out of scope — requires a dedicated specification.)
- **Employee account deactivation with approved requests**: What happens if an Employee with approved future requests is deactivated? (Out of scope — requires a dedicated specification.)
- **Auto‑expiry while balance has changed**: If a request expires, the reserved days are released. The system must ensure this operation is atomic and does not create negative balances. (Resolved by D-005 and FR-017.)
- **Client‑supplied payload spoofing**: **Given** an authenticated user submitting a request payload, **When** the payload contains authorization values (role, ID, balance) differing from server truth, **Then** the system shall ignore the client-supplied values and use only server-derived metrics.
- **Near‑simultaneous overlapping submissions**: **Given** two near-simultaneous submissions from the same Employee with overlapping date ranges, **When** both reach the database layer, **Then** the exclusion constraint (D-003) shall guarantee at most one is persisted, and the rejected attempt shall receive an explicit, predictable error.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system **shall** allow an authenticated Employee to submit a vacation request with a start date, end date, and reason.

- **FR-002**: The system **shall** reject requests with start date later than end date, with start date in the past or equal to the current day, or overlapping an existing `Pending`/`Approved` request for the same employee. Overlap rejection **shall** be enforced both at the application layer and via a database-level exclusion constraint to guarantee correctness under concurrent submissions (D-003).

- **FR-003**: The system **shall** calculate the requested day count server-side as working days (excluding Saturdays, Sundays, and public holidays from a configurable holiday calendar) and **shall not** accept a client-supplied value as authoritative (D-001).

- **FR-004**: The system **shall** allow an authorized Approver to approve or reject a `Pending` request owned by an employee assigned to them (based on the employee's `AssignedApproverId` at the time of the operation), and **shall not** allow self-approval. When rejecting, the Approver **must** provide a reason (SR-006).

- **FR-005**: The system **shall** temporarily reserve requested days when a request is in `Pending` state (not deduct from balance), and **shall** deduct them only upon approval (D-004). The reservation **shall** be released upon rejection, expiry, cancellation, or voiding.

- **FR-006**: The system **shall** prevent any transition that would leave the applicable balance negative.

- **FR-007**: The system **shall** allow an Employee to cancel their own `Pending` request, transitioning it to `Cancelled` as a final state and releasing the reserved days.

- **FR-008**: The system **shall** treat `Approved`, `Rejected`, `Cancelled`, `Voided`, and `Expired` as final states; no further transition **shall** be permitted from any of them.

- **FR-010**: The system **shall** restrict visibility of the reason field to the request's owner and their assigned Approver within the core MVP scope.

- **FR-011**: The system **shall** record an audit entry for every state transition, including actor, timestamp, action, entity, and result.

- **FR-012**: The system **shall** revalidate identity, ownership, approver relationship (based on the employee's current `AssignedApproverId`), and balance immediately before any state-changing operation, independent of any client-supplied or previously cached value (per SR-004).

- **FR-013**: The system **shall** resolve concurrent conflicting operations on the same request through optimistic concurrency control, allowing only one to succeed. This includes concurrent submission attempts that would create overlapping requests (see FR-002, D-003).

- **FR-014**: The system **shall** allow an Employee to view only their own request history and balance.

- **FR-015**: The system **shall** allow an Employee to void (cancel) an `Approved` request **only if** the start date has not yet passed. Upon voiding, the deducted days **shall** be returned to the employee's balance and the request **shall** transition to `Voided` as a final state.

- **FR-016**: The system **shall** apply a configurable expiry timeout (`EXPIRY_DAYS`) in working days for requests in `Pending` state. When the timeout is reached, the system shall automatically transition the request to `Expired` state, release the reserved days, and record the transition in the audit trail (D-005, D-006).

- **FR-017**: The system **shall** require explicit confirmation for approval, rejection, cancellation, and voiding actions before executing them, to prevent accidental execution of final/destructive transitions (per SR-005).

---

### Key Entities

- **Employee**: The person who submits vacation requests. Attributes: `Id`, `Name`, `Email`, `AssignedApproverId` (reference to another Employee), `Balance` (available vacation days), `Status` (`Active` or `Inactive`).

- **Approver**: A user who reviews and decides on requests assigned to them. An Approver is also an Employee who can submit their own requests (but cannot self‑approve).

- **VacationRequest**: A single request for time off. Attributes: `Id`, `OwnerId` (Employee), `StartDate`, `EndDate`, `Reason`, `Status` (`Pending`, `Approved`, `Rejected`, `Cancelled`, `Voided`, `Expired`), `RequestedDays` (calculated working days), `CreatedAt`, `ResolvedAt`, `ResolvedBy` (Approver), `RejectionReason` (optional for rejection, mandatory when rejecting), `ExpiryDate` (calculated from creation date + expiry timeout).

- **AuditRecord**: Immutable record of a state-changing action. Attributes: `Id`, `Timestamp` (UTC), `ActorId` (Employee), `ActorRole`, `Action`, `EntityType`, `EntityId`, `Result`, `CorrelationId`, `Details` (JSON with before/after state, reason, etc.).

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of leave requests with past dates, inverted date ranges, or overlapping ranges are rejected before creation, verified by automated tests covering each condition independently.

- **SC-002**: 100% of approval attempts that would cause a negative balance are rejected, with zero occurrences of negative balances in integration test suites covering approval flows.

- **SC-003**: 100% of self-approval and cross-team approval attempts are denied in authorization test suites.

- **SC-004**: Employee history and detail authorization tests confirm that users can access only their own request history and only request records they are authorized to view.

- **SC-005**: Every state transition produced in integration tests has a corresponding audit record with all required fields populated.

- **SC-006**: Concurrent-approval race-condition tests show exactly one successful transition out of two simultaneous conflicting attempts, with zero double-approvals or double-deductions observed. This includes near-simultaneous overlapping submission attempts (D-003), where exactly one of two conflicting requests is persisted.

- **SC-007**: Paged list tests for Employee history views retrieve data successfully with limit/offset logic verified, ensuring no unpaginated requests return > 50 records.

- **SC-008**: Auto‑expiry tests verify that requests reaching the configured expiry timeout transition to `Expired` state, release reserved days, and create an audit record.

- **SC-009**: Voiding tests verify that approved requests with future start dates transition to `Voided` and return days to the balance, while requests with passed start dates are rejected.

---

## Assumptions

- Employees have been previously provisioned in the system with their initial vacation balances.
- The authentication and role management system (identifying who is an Employee vs Approver) is already in place.
- Approver assignments are pre‑loaded (data seeding) and not managed within the system.
- All employees share the same timezone (no timezone conversion is required).
- The system uses a configurable list of public holidays for working day calculation.
- The expiry timeout (`EXPIRY_DAYS`) is a configurable parameter (default value to be defined during deployment).