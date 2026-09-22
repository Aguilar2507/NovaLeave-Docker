# NovaLeave — Audit Action Catalog

**Version:** 1.0.0  
**Constitution Reference:** NovaLeave — Constitution v4.0.0, §8 (Auditing, Logging, and Observability)  
**Last Updated:** 2026-07-28

---

## Overview

This document is the **single authoritative source** for all audit action names, their expected `Details` JSON content, and the implementation pattern for recording audit events. Feature plans MUST use the action names defined here and MUST NOT invent new ones without updating this catalog.

**Constitution §2.III compliance:** Every business rule (including audit action naming) exists in one authoritative location.

---

## Action Catalog

### Authentication Actions (Feature 004)

| Action | EntityType | Actor | Result | Description |
|--------|-----------|-------|--------|-------------|
| `LoginSuccess` | `Session` | Authenticated user | `Success` | User authenticated successfully |
| `LoginFailure` | `Session` | Unknown / resolvable user | `Failure` | Authentication attempt failed |
| `Logout` | `Session` | Authenticated user | `Success` | User signed out voluntarily |

### Vacation Request Actions (Feature 001)

| Action | EntityType | Actor | Result | Description |
|--------|-----------|-------|--------|-------------|
| `RequestCreated` | `VacationRequest` | User (owner) | `Success` | New request created in Pending state |
| `RequestApproved` | `VacationRequest` | Approver | `Success` | Pending → Approved |
| `RequestRejected` | `VacationRequest` | Approver | `Success` | Pending → Rejected |
| `RequestCancelled` | `VacationRequest` | User (owner) | `Success` | Pending → Cancelled |
| `RequestVoided` | `VacationRequest` | User (owner) | `Success` | Approved → Voided (pre-start) |
| `RequestExpired` | `VacationRequest` | System | `Success` | Pending → Expired (auto-expiry timeout) |
| `RequestEdited` | `VacationRequest` | User (owner) | `Success` | Pending request dates/reason modified |

### Security / Authorization Actions (Cross-Feature)

| Action | EntityType | Actor | Result | Description |
|--------|-----------|-------|--------|-------------|
| `AuthorizationFailure` | Varies | Authenticated user | `Failure` | Access denied: IDOR, cross-team, self-approval, or role violation |

---

## Details Schema per Action

The `Details` field contains a JSON string with before/after state and relevant context. **Sensitive fields are redacted** per Constitution §7.3 and §8 ("Logs MUST NOT contain passwords, hashes, tokens, keys, secrets, full payloads, or complete medical reasons").

### Authentication Actions

| Action | Details Content | Redacted Fields |
|--------|----------------|-----------------|
| `LoginSuccess` | `{ "redirectUrl": "/dashboard" }` | — |
| `LoginFailure` | `{ "reason": "InvalidPassword" }` | Password NEVER logged |
| `Logout` | `{}` | — |

> `LoginFailure` reason values: `InvalidPassword`, `InactiveAccount`, `LockedOut`, `UnknownEmail`.

### Vacation Request Actions

| Action | Details Content | Redacted Fields |
|--------|----------------|-----------------|
| `RequestCreated` | `{ "startDate": "2026-08-01", "endDate": "2026-08-05", "requestedDays": 3, "balanceBefore": 15, "balanceAfter": 15, "reservedDaysBefore": 0, "reservedDaysAfter": 3 }` | `reason` (PII §7.3) |
| `RequestApproved` | `{ "startDate": "2026-08-01", "endDate": "2026-08-05", "requestedDays": 3, "balanceBefore": 15, "balanceAfter": 12 }` | — |
| `RequestRejected` | `{ "startDate": "2026-08-01", "endDate": "2026-08-05", "requestedDays": 3, "reservedDaysReleased": 3 }` | `reason`, `rejectionReason` (PII §7.3) |
| `RequestCancelled` | `{ "startDate": "2026-08-01", "endDate": "2026-08-05", "requestedDays": 3, "reservedDaysReleased": 3 }` | — |
| `RequestVoided` | `{ "startDate": "2026-08-01", "endDate": "2026-08-05", "requestedDays": 3, "balanceBefore": 12, "balanceAfter": 15 }` | — |
| `RequestExpired` | `{ "startDate": "2026-08-01", "endDate": "2026-08-05", "requestedDays": 3, "reservedDaysReleased": 3, "expiryDate": "2026-07-25" }` | — |
| `RequestEdited` | `{ "before": { "startDate": "2026-08-01", "endDate": "2026-08-05", "requestedDays": 3 }, "after": { "startDate": "2026-08-04", "endDate": "2026-08-08", "requestedDays": 3 }, "reservedDaysChange": 0 }` | `reason` before/after (PII §7.3) |

### Security Actions

| Action | Details Content | Redacted Fields |
|--------|----------------|-----------------|
| `AuthorizationFailure` | `{ "attemptedAction": "Approve", "targetEntityId": "abc-123", "denialReason": "SelfApproval" }` | — |

> `AuthorizationFailure` denial reasons: `SelfApproval`, `NotOwner`, `NotAssignedApprover`, `CrossTeamAccess`, `InactiveApprover`, `ResourceNotFound`.

---

## Implementation Pattern

The following pattern is **mandatory** for all audit recording in NovaLeave, per Constitution §6 and §8.

### Pattern: In-Handler, Post-Transition, Same Transaction

```text
Handler execution order:
  1. Validate input (FluentValidation)
  2. Revalidate identity, role, active status, ownership (SR-004)
  3. Execute domain transition (entity.Approve(), employee.DeductDays(), etc.)
  4. Record audit (IStateTransitionAuditService.RecordTransitionAsync)
  5. SaveChangesAsync — persists entity + balance + audit atomically
```

### Rules

1. **Post-transition**: The audit call happens AFTER the domain entity transitions state and AFTER balance operations, but BEFORE `SaveChangesAsync`.
2. **Same transaction**: The `SaveChangesAsync` call persists the state change, balance change, and audit record in a single atomic transaction. If any part fails, everything rolls back.
3. **No silent changes**: If the audit write fails (e.g., validation error on the AuditRecord entity), the entire transaction fails. There MUST NOT be a state change without a corresponding audit record.
4. **Handler responsibility**: Each Application handler is responsible for calling the audit service. This is NOT deferred to middleware, events, or post-commit hooks.
5. **Correlation**: The handler receives `correlationId` from the HTTP pipeline (`HttpContext.TraceIdentifier`) and passes it to the audit service.

### Code Example

```csharp
// Inside ApproveRequestHandler.HandleAsync:

// 1-2. Validation + revalidation already done above

// 3. Domain transition
request.Approve(command.ApproverId, timeProvider);
employee.DeductDays(request.RequestedDays);

// 4. Audit
await auditService.RecordTransitionAsync(
	entityId: request.Id,
	entityType: "VacationRequest",
	action: "RequestApproved",
	actorId: command.ApproverId.ToString(),
	actorRole: "Approver",
	result: "Success",
	correlationId: correlationId,
	details: JsonSerializer.Serialize(new
	{
		startDate = request.StartDate,
		endDate = request.EndDate,
		requestedDays = request.RequestedDays,
		balanceBefore = employee.Balance + request.RequestedDays,
		balanceAfter = employee.Balance
	}),
	ct: cancellationToken);

// 5. Atomic save
await repository.SaveChangesAsync(cancellationToken);
```

### Constitution Justification

- **§6**: "Approval, balance deduction, and auditing MUST execute within the same atomic transaction."
- **§8**: "Every transition or critical change MUST record at least: `timestamp_utc`, `actor_id`, `actor_role`, `action`, `entity_type`, `entity_id`, `result`, `correlation_id`, and `request_id`."
- **§8**: "Audit records MUST be protected against unauthorized modification or deletion."
- **§7.3**: "Logs, traces, metrics, and diagrams MUST NOT contain the complete reason or other unnecessary sensitive data."

---

## Referenced By

- [`specs/common/data-model.md`](./data-model.md) — AuditRecord entity definition
- [`specs/001-vacation-request/plan.md`](../001-vacation-request/plan.md) — FR-011, Phase 2 step 4, Phase 3 step 8
- [`specs/001-vacation-request/contracts.md`](../001-vacation-request/contracts.md) — IStateTransitionAuditService
- [`specs/004-initial-setup-and-authentication/plan.md`](../004-initial-setup-and-authentication/plan.md) — FR-018, FR-019
- [`specs/004-initial-setup-and-authentication/contracts.md`](../004-initial-setup-and-authentication/contracts.md) — IAuthenticationAuditService
