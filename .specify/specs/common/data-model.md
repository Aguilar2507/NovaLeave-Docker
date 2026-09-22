# NovaLeave — Shared Data Model

**Version:** 1.0.0  
**Constitution Reference:** NovaLeave — Constitution v4.0.0  
**Last Updated:** 2026-07-28

---

## Overview

This document defines the shared data model entities used across multiple NovaLeave features. Feature-specific specs reference this document instead of duplicating entity definitions.

---

## Core Entities

### Employee (Application User)

Represents a system user (either User role, Approver role, or both).

| Attribute | Type | Description |
|-----------|------|-------------|
| `Id` | `Guid` | Primary key |
| `IdentityId` | `string` | FK to ASP.NET Core Identity `IdentityUser.Id` |
| `Name` | `string` | Display name |
| `Email` | `string` | Unique email, matches Identity email |
| `AssignedApproverId` | `Guid?` | FK to Employee (the assigned approver) |
| `Balance` | `int` | Available vacation days (non-negative) |
| `Status` | `AccountStatus` | `Active` or `Inactive` |
| `CreatedAt` | `DateTimeOffset` | Account creation timestamp (UTC) |
| `EmploymentStartDate` | `DateOnly` | Used for accrual calculation |

**Invariants:**
- Only `Active` accounts may authenticate or perform state-changing operations.
- Balance MUST NOT go negative.
- An employee cannot be their own approver.

---

### IdentityUser (ASP.NET Core Identity)

Standard ASP.NET Core Identity user extended with account status linkage.

| Attribute | Type | Description |
|-----------|------|-------------|
| `Id` | `string` | Primary key |
| `Email` | `string` | Login credential |
| `PasswordHash` | `string` | Hashed password |
| `AccessFailedCount` | `int` | Failed login counter |
| `LockoutEnd` | `DateTimeOffset?` | Lockout expiry |
| `LockoutEnabled` | `bool` | Whether lockout is active |
| `SecurityStamp` | `string` | Changed on role/status updates to invalidate sessions |

**Relationship:** `IdentityUser.Id` → `Employee.IdentityId` (1:1)

---

### VacationRequest

A single leave/vacation request submitted by an Employee.

| Attribute | Type | Description |
|-----------|------|-------------|
| `Id` | `Guid` | Primary key |
| `OwnerId` | `Guid` | FK to Employee (request owner) |
| `StartDate` | `DateOnly` | First day of vacation |
| `EndDate` | `DateOnly` | Last day of vacation |
| `Reason` | `string` | Employee's reason (privacy-protected) |
| `Status` | `RequestStatus` | `Pending`, `Approved`, `Rejected`, `Cancelled`, `Voided`, `Expired` |
| `RequestedDays` | `int` | Calculated working days (server-side) |
| `CreatedAt` | `DateTimeOffset` | Creation timestamp (UTC) |
| `ResolvedAt` | `DateTimeOffset?` | Resolution timestamp |
| `ResolvedBy` | `Guid?` | FK to Employee (Approver who resolved) |
| `RejectionReason` | `string?` | Mandatory when rejecting |
| `ExpiryDate` | `DateOnly` | Calculated: creation + EXPIRY_DAYS working days |
| `RowVersion` | `byte[]` | Optimistic concurrency token |

**Invariants:**
- `StartDate` < `EndDate`
- `StartDate` must be strictly future (> today)
- No overlapping `Pending`/`Approved` requests for the same owner (DB exclusion constraint)
- Final states (`Approved`, `Rejected`, `Cancelled`, `Voided`, `Expired`) allow no further transitions

---

### AuditRecord

Immutable record of security-relevant and state-changing events.

| Attribute | Type | Description |
|-----------|------|-------------|
| `Id` | `Guid` | Primary key |
| `Timestamp` | `DateTimeOffset` | Event time (UTC) |
| `ActorId` | `string?` | Identity of the actor (null if unresolvable) |
| `ActorRole` | `string?` | Role at time of action |
| `Action` | `string` | e.g., `LoginSuccess`, `LoginFailure`, `Logout`, `RequestCreated`, `RequestApproved` |
| `EntityType` | `string?` | e.g., `VacationRequest`, `Session` |
| `EntityId` | `string?` | ID of affected entity |
| `Result` | `string` | `Success` or `Failure` |
| `Reason` | `string?` | Internal reason category (not exposed to user) |
| `CorrelationId` | `string` | Request correlation |
| `RequestId` | `string?` | HTTP request identifier |
| `Details` | `string?` | JSON with before/after state, additional context |

**Invariants:**
- Immutable: no updates or deletes permitted.
- MUST NOT contain passwords, tokens, or sensitive PII.

**Action names and Details schema:** See [`specs/common/audit-catalog.md`](./audit-catalog.md) for the authoritative catalog of all action values and their expected `Details` JSON content.

---

## Enumerations

### AccountStatus

```csharp
public enum AccountStatus
{
	Active,
	Inactive
}
```

### RequestStatus

```csharp
public enum RequestStatus
{
	Pending,
	Approved,
	Rejected,
	Cancelled,
	Voided,
	Expired
}
```

---

## Roles

| Role | Description |
|------|-------------|
| `User` | Can submit, edit, cancel, and void own requests; view own history/balance |
| `Approver` | Can approve/reject requests assigned to them; also holds User capabilities |

A person MAY hold both roles simultaneously.

---

## Relationships

```text
Employee 1 ──── 0..* VacationRequest (Owner)
Employee 1 ──── 0..* Employee (AssignedApprover)
IdentityUser 1 ──── 1 Employee (via IdentityId)
```

---

## Referenced By

- [`specs/004-initial-setup-and-authentication/plan.md`](../004-initial-setup-and-authentication/plan.md) — IdentityUser, Employee (status/roles), AuditRecord
- [`specs/001-vacation-request/plan.md`](../001-vacation-request/plan.md) — VacationRequest, Employee, AuditRecord
- [`specs/common/audit-catalog.md`](./audit-catalog.md) — Authoritative action catalog for AuditRecord.Action values
