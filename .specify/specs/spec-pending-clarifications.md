# Feature Specification: Leave/Vacation Request Management — Pending Clarifications

**Feature Branch**: `001-vacation-request-pending`
**Created**: 2026-07-17
**Updated**: 2026-07-22
**Status**: Draft — Partially Resolved, Some Items Pending
**Constitution Reference**: NovaLeave — Constitution v3.0.0

## Overview

This document tracks items that were previously marked as requiring Product Owner clarification, along with deferred scope moved out of the core MVP. Many items have been resolved by the PO's consolidated decisions (see `PO_Decisions.md`). The remaining open items and deferred capabilities are documented here for future iterations.

---

## Resolution Summary

The following items have been **RESOLVED** by PO decisions and are now either incorporated into the core MVP specification (`spec-core.md`) or explicitly deferred/out of scope:

| Original Pending Item | PO Decision | Status |
| :--- | :--- | :--- |
| **US-7 — HR profile configuration** | HR does NOT configure users. Approver relationships are pre-loaded or managed externally. | **RESOLVED — OUT OF SCOPE** |
| **US-5 — HR consults history and balances, read-only** | Not required for the core MVP. Moved out of the core spec and deferred for a future iteration. | **RESOLVED — DEFERRED FROM CORE MVP** |
| **US-8 — Post-approval changes (cancellation/amendment)** | Only full voiding of approved requests BEFORE start date is allowed. No amendments, no partial cancellations. | **RESOLVED — PARTIALLY INCLUDED IN CORE** |
| **Auto-escalation** | NOT included. Auto‑expiry replaces escalation. | **RESOLVED — REPLACED BY AUTO‑EXPIRY** |
| **Leave Administrator role** | NOT required. All employees have an assigned Approver via pre‑loaded data. | **RESOLVED — NOT REQUIRED** |
| **Same-day request policy** | NOT allowed. Start date must be strictly future. | **RESOLVED** |
| **Working calendar policy** | Only working days count. Weekends and public holidays are excluded. | **RESOLVED** |
| **Maximum duration (FR-022)** | Maximum is the employee's available balance. No separate limit. | **RESOLVED — COVERED BY BALANCE** |
| **Future horizon (FR-023)** | No maximum horizon. Any future date is allowed. | **RESOLVED — NO RESTRICTION** |
| **Minimum notice (FR-024)** | No minimum notice required. | **RESOLVED — NO RESTRICTION** |
| **Rejection reason** | Mandatory. | **RESOLVED** |
| **Explicit confirmation** | Required for final actions. | **RESOLVED** |
| **Insufficient balance handling** | Blocked with clear message. | **RESOLVED** |
| **Cancellation after start date** | NOT allowed. | **RESOLVED** |
| **Partial cancellation** | NOT allowed. | **RESOLVED** |
| **Employee deactivation** | Out of scope for MVP. | **RESOLVED — OUT OF SCOPE** |
| **Multiple pending requests** | No explicit limit from PO. Allowed unless balance is insufficient. | **RESOLVED** |
| **HR profile configuration** | Out of scope. Approver assignments are pre‑loaded. | **RESOLVED — OUT OF SCOPE** |
| **Hierarchical cycle prevention** | Out of scope (no HR configuration in MVP). | **RESOLVED — OUT OF SCOPE** |
| **In-App Notifications (US-6)** | Not included in MVP. | **RESOLVED — OUT OF SCOPE** |

---

## Out of Scope for Core MVP (Confirmed)

The following items have been confirmed as **out of scope** for the core MVP and are **not** required for implementation:

- **HR profile configuration and manager/approver assignment** — Approver relationships are pre‑loaded (data seeding), not managed within the system.
- **HR read-only history and balance views** — Not required for the core MVP; deferred to a future iteration.
- **Leave Administrator role** — Not required.
- **Auto-escalation** — Replaced by auto‑expiry.
- **In-App Notifications** — Not included in MVP.
- **Additional date validations** (max duration, future horizon, minimum notice) — No restrictions beyond balance availability and future-date requirement.
- **Hierarchical cycle prevention** — No HR configuration, so not applicable.
- **Employee deactivation handling** — Requires a dedicated specification.
- **External integrations** — None.
- **Reports** — None.

---

## User Scenarios & Testing *(mandatory)*

### User Story 8 - Employee voids (cancels) an approved request (Priority: P2) — [RESOLVED]

**Note**: This user story has been **resolved** and is now partially included in the core MVP. The PO confirmed that:

- Employees **can** void (cancel) an approved request **only if** the start date has not yet passed.
- Voiding returns the deducted days to the balance.
- No amendments or partial cancellations are allowed.
- Once the vacation period has started, cancellation is **not** allowed.

**Resolution incorporated into core spec as FR-015.**

---

### User Story 7 - HR configures employee profiles (Priority: TBD) — [RESOLVED — OUT OF SCOPE]

**Note**: This user story is **out of scope** for the MVP. The PO confirmed that HR does **not** configure employee profiles or assign approvers within the system. Approver relationships are pre‑loaded or managed externally (data seeding).

**Resolution**: Not implemented in MVP.

---

### User Story 5 - HR consults history and balances, read-only (Priority: TBD) — [DEFERRED — OUT OF CORE MVP]

**Note**: This user story was originally included in the core MVP spec, but the PO confirmed that the HR read-only view is **not necessary for the MVP**. The entire story has been moved here for future iteration planning.

As an **HR** user, I want to view the organization-wide request history and employee balances, so that I can answer questions and support reporting without being able to alter any request or balance.

**Why this priority**: Valuable once the employee/approver workflow is stable and data exists to consult, but not required for the core day-one workflow.

**Independent Test**: As an HR user, browse request history across multiple employees and view balances; verify no create/approve/reject/cancel/void/adjust action is reachable through any HR-visible screen, route, or direct HTTP request.

**Acceptance Scenarios**:

1. **Given** an authorized HR user, **When** they request the organization-wide history or balances, **Then** the system shall return the data through an explicitly authorized, read-only use case.
2. **Given** an HR user, **When** they attempt to invoke any create/approve/reject/cancel/void/adjust action, **Then** the system shall deny the action regardless of UI visibility.
3. **Given** an HR user viewing request data, **When** the system generates the view, export, or query result, **Then** it shall restrict the reason field unless explicitly authorized as per Constitution §7.3.
4. **Given** an HR query resulting in over 50 records, **When** the system processes the dataset, **Then** it shall implement server-side pagination to return results in chunks.
5. **Given** an HR user, **When** they attempt to access any approval-related view or action (including the approver dashboard for approvals), **Then** the system shall deny access and redirect to the read-only history view.

**Deferred requirements carried over from the former core MVP spec**:

- **FR-009**: The system **shall** provide HR with read-only access to organization-wide history and balances and **shall not** expose any create/approve/reject/cancel/void/adjust capability to the HR role.
- **FR-010 (deferred HR extension)**: The system **shall** restrict visibility of the reason field to the request's owner, their assigned Approver, and HR only through explicitly authorized use cases.
- **SR-007 (deferred HR variant)**: HR users **must not** have access to approval or management actions; HR capabilities are strictly limited to read-only history and balance consultation.
- **SC-004 (deferred HR variant)**: HR-role test accounts have zero successful invocations of create/approve/reject/cancel/void/adjust actions across all tested endpoints and direct HTTP access attempts.
- **SC-007 (deferred HR variant)**: Paged list tests for HR organization-wide views retrieve data successfully with limit/offset logic verified, ensuring no unpaginated requests return more than 50 records.

**Resolution**: Deferred from core MVP. Reassess when HR reporting/support workflows are prioritized.

---

## Documented Design Decisions (Resolved)

- **D-004 — Auto-Expiry for Prolonged Pending Requests** — [RESOLVED]: The system **shall** automatically expire a `Pending` request if it remains unresolved for `N` working days (where `N` is a configurable parameter). This replaces the auto-escalation concept. See FR-016 in core spec.

- **D-005 — Leave Administrator Role** — [RESOLVED — OUT OF SCOPE]: Not required. All employees have an assigned Approver via pre‑loaded data.

- **D-006 — Hierarchical Cycle Prevention** — [RESOLVED — OUT OF SCOPE]: Not applicable as HR configuration is out of scope for MVP.

---

## Remaining Open Items — [NEEDS CLARIFICATION]

The following items require further clarification from the PO for future iterations:

### Edge Cases (Open)

| Edge Case | Question | Priority |
| :--- | :--- | :--- |
| **Employee account deactivation** | What happens if an Employee's account is deactivated while they have `Pending` or `Approved` requests? Should requests be automatically cancelled, or should they remain with a note? | Future Iteration |
| **Approver reassignment** | What happens if an Approver is reassigned while a request from a former team member is still `Pending`? Should pending requests be re‑routed or remain with the original Approver? | Future Iteration |
| **Balance recalculation on voiding** | When an approved request is voided, the days are returned. Does the system need to handle any special cases (e.g., if the employee has since accrued additional days)? | Future Iteration |
| **Holiday calendar management** | Who manages the public holiday calendar? Should HR have a UI for this, or is it a configuration file? | Future Iteration |

### Functional Requirements (Open)

| FR | Description | Question |
| :--- | :--- | :--- |
| **FR-028** | Employee deactivation | When an employee is deactivated, what happens to their pending and approved vacation requests? Should they be automatically cancelled? |
| **FR-029** | Reassignment of approver | If an approver is changed, should pending requests be reassigned to the new approver? |
| **FR-009** | HR read-only history and balances | When this deferred story is implemented, which HR users are authorized to see organization-wide balances and history, and are there any filtering constraints? |
| **FR-010** | HR access to reason field | When HR read-only views are implemented, should HR always see the request reason, or only through explicitly authorized use cases? |

---

## Decision Log — Resolved Items

| Date | Item | Decision |
| :--- | :--- | :--- |
| 2026-07-20 | US-7 (HR configuration) | Out of scope — Approver relationships pre‑loaded. |
| 2026-07-22 | US-5 (HR read-only views) | Removed from core MVP and deferred to pending clarifications for a future iteration. |
| 2026-07-20 | US-8 (Post-approval changes) | Partial — Voiding allowed only before start date. No amendments or partial cancellations. |
| 2026-07-20 | Auto-escalation | Replaced by auto‑expiry. |
| 2026-07-20 | Leave Administrator | Not required. |
| 2026-07-20 | Same-day requests | Not allowed. Start date must be strictly future. |
| 2026-07-20 | Working calendar | Only working days count. Weekends and holidays excluded. |
| 2026-07-20 | Max duration | Covered by available balance. No separate limit. |
| 2026-07-20 | Future horizon | No restriction. |
| 2026-07-20 | Minimum notice | No restriction. |
| 2026-07-20 | Rejection reason | Mandatory. |
| 2026-07-20 | Explicit confirmation | Required for final actions. |
| 2026-07-20 | Insufficient balance | Blocked with clear message. |
| 2026-07-20 | In-App Notifications (US-6) | Out of scope for MVP. |
| 2026-07-20 | Multiple pending requests | No explicit limit. Allowed unless balance insufficient. |

---

## Next Steps

1. **Core MVP Implementation**: Proceed with `spec-core.md` which now includes all resolved PO decisions.
2. **Future Iterations**: The remaining open items (`[NEEDS CLARIFICATION]`) should be prioritized for a future iteration. The PO will need to provide additional guidance on:
   - Employee deactivation behavior
	  - Approver reassignment handling
   - HR read-only history/balance scope and data visibility rules
   - Holiday calendar management
3. **Decision Documentation**: All resolved decisions are documented in the core spec (`spec-core.md`) and this document for reference.

---

## Cross-Reference: Questions in `preguntas_po.md`

The following questions have been **resolved** and are no longer pending:

| Question | Resolution |
| :--- | :--- |
| P1 — Same-day requests | Not allowed |
| P2 — Working calendar policy | Only working days |
| P3 — Maximum duration | Covered by balance |
| P4 — Future horizon | No restriction |
| P5 — Minimum notice | No restriction |
| P6 — Who approves managers | Pre‑loaded approver table |
| P7 — Leave Administrator | Not required |
| P8 — Hierarchical cycles | Not applicable (out of scope) |
| P9 — Auto-escalation | Replaced by auto‑expiry |
| P10 — Delegation | Not applicable |
| P11 — Post-approval changes | Only voiding before start date |
| P12 — Cancellation after start date | Not allowed |
| P13 — Explicit confirmation | Required |
| P14 — Employee deactivation | Out of scope for MVP |
| P15 — Multiple pending requests | No explicit limit |
| P16 — Manager comment on rejection | Mandatory |

---

## Assumptions (Confirmed)

The following assumptions are now confirmed and incorporated into the core spec:

- ✅ Day count is calculated as **working days only** (excluding weekends and public holidays).
- ✅ Employees **cannot** request the current day — start date must be strictly future.
- ✅ There is **no minimum notice period**.
- ✅ There is **no maximum future horizon**.
- ✅ The **maximum duration** is the employee's available balance.
- ✅ HR **does not** configure users or assign approvers — relationships are pre‑loaded.
- ✅ HR read-only history and balance views are **not required** for the core MVP.
- ✅ **No Leave Administrator role** is required.
- ✅ **No auto-escalation** — auto‑expiry replaces this.
- ✅ **No in-app notifications** in the MVP.
- ✅ Rejection reason is **mandatory**.
- ✅ Explicit confirmation is **required** for all final actions.
- ✅ **No partial cancellations** — only full voiding before start date.
- ✅ **No amendments** to approved requests.
- ✅ Employees **cannot** void approved requests once the start date has passed.