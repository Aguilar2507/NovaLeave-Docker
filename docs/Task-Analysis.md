# Task Analysis

**Project:** NovaLeave  
**Analysis date:** 2026-09-23  
**Scope:** Point-by-point revalidation of all 23 findings listed in `docs/Keep-in-mind.md` against the current source tree, tests, configuration, and available test execution.  
**Status:** Analysis only; no implementation performed.

This revision treats `Keep-in-mind.md` as the baseline rather than as current evidence. It distinguishes resolved work, partial progress, decisions that remain intentionally open, and findings that still reproduce. The presentation test suite was executed with the .NET 10 SDK; it still fails and reproduces the shared InMemory database and obsolete-role problems. A separate business defect was also found while reviewing the request lifecycle: approval deducts days after creation has already reserved them. It is recorded under point 2 because it affects the same request-lifecycle and balance invariant.

> This document analyzes the documented issues individually. It does not modify the application, implement fixes, or assume that every unchecked task is still missing. Some findings may be stale and require confirmation against the current source tree.

## Contents

- [Task Analysis](#task-analysis)
  - [Contents](#contents)
  - [1. Presentation Test Suite Is Non-Deterministic](#1-presentation-test-suite-is-non-deterministic)
    - [1.1 Current Issue](#11-current-issue)
    - [1.2 Benefits of Resolution](#12-benefits-of-resolution)
    - [1.3 Consequences of Not Resolving](#13-consequences-of-not-resolving)
    - [1.4 Necessity](#14-necessity)
    - [1.5 Conclusion](#15-conclusion)
    - [1.6 Proposed Solution](#16-proposed-solution)
  - [2. Auto-Expiry Is Missing](#2-auto-expiry-is-missing)
    - [2.1 Current Issue](#21-current-issue)
    - [2.2 Benefits of Resolution](#22-benefits-of-resolution)
    - [2.3 Consequences of Not Resolving](#23-consequences-of-not-resolving)
    - [2.4 Necessity](#24-necessity)
    - [2.5 Conclusion](#25-conclusion)
    - [2.6 Proposed Solution](#26-proposed-solution)
  - [3. Audit Immutability Is Not Database-Enforced](#3-audit-immutability-is-not-database-enforced)
    - [3.1 Current Issue](#31-current-issue)
    - [3.2 Benefits of Resolution](#32-benefits-of-resolution)
    - [3.3 Consequences of Not Resolving](#33-consequences-of-not-resolving)
    - [3.4 Necessity](#34-necessity)
    - [3.5 Conclusion](#35-conclusion)
    - [3.6 Proposed Solution](#36-proposed-solution)
  - [4. Audit History Is Not Available](#4-audit-history-is-not-available)
    - [4.1 Current Issue](#41-current-issue)
    - [4.2 Benefits of Resolution](#42-benefits-of-resolution)
    - [4.3 Consequences of Not Resolving](#43-consequences-of-not-resolving)
    - [4.4 Necessity](#44-necessity)
    - [4.5 Conclusion](#45-conclusion)
    - [4.6 Proposed Solution](#46-proposed-solution)
  - [5. Destructive-Action Confirmation Is Reported Missing](#5-destructive-action-confirmation-is-reported-missing)
    - [5.1 Current Issue](#51-current-issue)
    - [5.2 Benefits of Resolution](#52-benefits-of-resolution)
    - [5.3 Consequences of Not Resolving](#53-consequences-of-not-resolving)
    - [5.4 Necessity](#54-necessity)
    - [5.5 Conclusion](#55-conclusion)
    - [5.6 Proposed Solution](#56-proposed-solution)
  - [6. CDN Scripts Lack Subresource Integrity](#6-cdn-scripts-lack-subresource-integrity)
    - [6.1 Current Issue](#61-current-issue)
    - [6.2 Benefits of Resolution](#62-benefits-of-resolution)
    - [6.3 Consequences of Not Resolving](#63-consequences-of-not-resolving)
    - [6.4 Necessity](#64-necessity)
    - [6.5 Conclusion](#65-conclusion)
    - [6.6 Proposed Solution](#66-proposed-solution)
  - [7. Security Headers Are Missing](#7-security-headers-are-missing)
    - [7.1 Current Issue](#71-current-issue)
    - [7.2 Benefits of Resolution](#72-benefits-of-resolution)
    - [7.3 Consequences of Not Resolving](#73-consequences-of-not-resolving)
    - [7.4 Necessity](#74-necessity)
    - [7.5 Conclusion](#75-conclusion)
    - [7.6 Proposed Solution](#76-proposed-solution)
  - [8. Concurrency Protection Is Untested](#8-concurrency-protection-is-untested)
    - [8.1 Current Issue](#81-current-issue)
    - [8.2 Benefits of Resolution](#82-benefits-of-resolution)
    - [8.3 Consequences of Not Resolving](#83-consequences-of-not-resolving)
    - [8.4 Necessity](#84-necessity)
    - [8.5 Conclusion](#85-conclusion)
    - [8.6 Proposed Solution](#86-proposed-solution)
  - [9. Authorization and IDOR Coverage Is Incomplete](#9-authorization-and-idor-coverage-is-incomplete)
    - [9.1 Current Issue](#91-current-issue)
    - [9.2 Benefits of Resolution](#92-benefits-of-resolution)
    - [9.3 Consequences of Not Resolving](#93-consequences-of-not-resolving)
    - [9.4 Necessity](#94-necessity)
    - [9.5 Conclusion](#95-conclusion)
    - [9.6 Proposed Solution](#96-proposed-solution)
  - [10. E2E Testing Is Not Operational](#10-e2e-testing-is-not-operational)
    - [10.1 Current Issue](#101-current-issue)
    - [10.2 Benefits of Resolution](#102-benefits-of-resolution)
    - [10.3 Consequences of Not Resolving](#103-consequences-of-not-resolving)
    - [10.4 Necessity](#104-necessity)
    - [10.5 Conclusion](#105-conclusion)
    - [10.6 Proposed Solution](#106-proposed-solution)
  - [11. Accessibility Has Not Been Verified](#11-accessibility-has-not-been-verified)
    - [11.1 Current Issue](#111-current-issue)
    - [11.2 Benefits of Resolution](#112-benefits-of-resolution)
    - [11.3 Consequences of Not Resolving](#113-consequences-of-not-resolving)
    - [11.4 Necessity](#114-necessity)
    - [11.5 Conclusion](#115-conclusion)
    - [11.6 Proposed Solution](#116-proposed-solution)
  - [12. Performance Targets Are Unmeasured](#12-performance-targets-are-unmeasured)
    - [12.1 Current Issue](#121-current-issue)
    - [12.2 Benefits of Resolution](#122-benefits-of-resolution)
    - [12.3 Consequences of Not Resolving](#123-consequences-of-not-resolving)
    - [12.4 Necessity](#124-necessity)
    - [12.5 Conclusion](#125-conclusion)
    - [12.6 Proposed Solution](#126-proposed-solution)
  - [13. Code Coverage Has Not Been Measured](#13-code-coverage-has-not-been-measured)
    - [13.1 Current Issue](#131-current-issue)
    - [13.2 Benefits of Resolution](#132-benefits-of-resolution)
    - [13.3 Consequences of Not Resolving](#133-consequences-of-not-resolving)
    - [13.4 Necessity](#134-necessity)
    - [13.5 Conclusion](#135-conclusion)
    - [13.6 Proposed Solution](#136-proposed-solution)
  - [14. Rate Limiting Is Unverified](#14-rate-limiting-is-unverified)
    - [14.1 Current Issue](#141-current-issue)
    - [14.2 Benefits of Resolution](#142-benefits-of-resolution)
    - [14.3 Consequences of Not Resolving](#143-consequences-of-not-resolving)
    - [14.4 Necessity](#144-necessity)
    - [14.5 Conclusion](#145-conclusion)
    - [14.6 Proposed Solution](#146-proposed-solution)
  - [15. Infrastructure Test Project Is Missing](#15-infrastructure-test-project-is-missing)
    - [15.1 Current Issue](#151-current-issue)
    - [15.2 Benefits of Resolution](#152-benefits-of-resolution)
    - [15.3 Consequences of Not Resolving](#153-consequences-of-not-resolving)
    - [15.4 Necessity](#154-necessity)
    - [15.5 Conclusion](#155-conclusion)
    - [15.6 Proposed Solution](#156-proposed-solution)
  - [16. Required Documentation Directories Are Missing](#16-required-documentation-directories-are-missing)
    - [16.1 Current Issue](#161-current-issue)
    - [16.2 Benefits of Resolution](#162-benefits-of-resolution)
    - [16.3 Consequences of Not Resolving](#163-consequences-of-not-resolving)
    - [16.4 Necessity](#164-necessity)
    - [16.5 Conclusion](#165-conclusion)
    - [16.6 Proposed Solution](#166-proposed-solution)
  - [17. Documentation Language Is Inconsistent](#17-documentation-language-is-inconsistent)
    - [17.1 Current Issue](#171-current-issue)
    - [17.2 Benefits of Resolution](#172-benefits-of-resolution)
    - [17.3 Consequences of Not Resolving](#173-consequences-of-not-resolving)
    - [17.4 Necessity](#174-necessity)
    - [17.5 Conclusion](#175-conclusion)
    - [17.6 Proposed Solution](#176-proposed-solution)
  - [18. Docker Must Not Be Used for Official Benchmarks](#18-docker-must-not-be-used-for-official-benchmarks)
    - [18.1 Current Issue](#181-current-issue)
    - [18.2 Benefits of Respecting the Decision](#182-benefits-of-respecting-the-decision)
    - [18.3 Consequences of Ignoring the Decision](#183-consequences-of-ignoring-the-decision)
    - [18.4 Necessity](#184-necessity)
    - [18.5 Conclusion](#185-conclusion)
    - [18.6 Proposed Solution](#186-proposed-solution)
  - [19. SQL Retry Strategy Must Remain Disabled](#19-sql-retry-strategy-must-remain-disabled)
    - [19.1 Current Issue](#191-current-issue)
    - [19.2 Benefits of Respecting the Decision](#192-benefits-of-respecting-the-decision)
    - [19.3 Consequences of Ignoring the Decision](#193-consequences-of-ignoring-the-decision)
    - [19.4 Necessity](#194-necessity)
    - [19.5 Conclusion](#195-conclusion)
    - [19.6 Proposed Solution](#196-proposed-solution)
  - [20. Docker Appsettings File Must Not Be Added](#20-docker-appsettings-file-must-not-be-added)
    - [20.1 Current Issue](#201-current-issue)
    - [20.2 Benefits of Respecting the Decision](#202-benefits-of-respecting-the-decision)
    - [20.3 Consequences of Ignoring the Decision](#203-consequences-of-ignoring-the-decision)
    - [20.4 Necessity](#204-necessity)
    - [20.5 Conclusion](#205-conclusion)
    - [20.6 Proposed Solution](#206-proposed-solution)
  - [21. Startup Migrations Are Development-Only](#21-startup-migrations-are-development-only)
    - [21.1 Current Issue](#211-current-issue)
    - [21.2 Benefits of Resolution](#212-benefits-of-resolution)
    - [21.3 Consequences of Not Resolving](#213-consequences-of-not-resolving)
    - [21.4 Necessity](#214-necessity)
    - [21.5 Conclusion](#215-conclusion)
    - [21.6 Proposed Solution](#216-proposed-solution)
  - [22. Host Builds Require .NET 10 or Docker](#22-host-builds-require-net-10-or-docker)
    - [22.1 Current Issue](#221-current-issue)
    - [22.2 Benefits of Resolution](#222-benefits-of-resolution)
    - [22.3 Consequences of Not Resolving](#223-consequences-of-not-resolving)
    - [22.4 Necessity](#224-necessity)
    - [22.5 Conclusion](#225-conclusion)
    - [22.6 Proposed Solution](#226-proposed-solution)
  - [23. Housekeeping and Naming Deviations](#23-housekeeping-and-naming-deviations)
    - [23.1 Current Issue](#231-current-issue)
    - [23.2 Benefits of Resolution](#232-benefits-of-resolution)
    - [23.3 Consequences of Not Resolving](#233-consequences-of-not-resolving)
    - [23.4 Necessity](#234-necessity)
    - [23.5 Conclusion](#235-conclusion)
    - [23.6 Proposed Solution](#236-proposed-solution)
  - [Overall Conclusion](#overall-conclusion)

## 1. Presentation Test Suite Is Non-Deterministic

### 1.1 Current Issue

The suite remains non-deterministic and is still not an acceptance gate. A current `dotnet test` run reproduces `Sequence contains more than one element` from the shared `NovaLeaveTestDb`, `Role EMPLOYEE does not exist`, and unrelated response/assertion failures. The fixtures now have an early seed guard, but that guard does not provide isolation; the fixed database name remains shared across factories and the fixture still creates obsolete `Employee` and `Admin` roles. The current test run therefore confirms the original diagnosis rather than closing it.

### 1.2 Benefits of Resolution

Isolated fixtures and aligned roles would make test results deterministic, restore reliable CI validation, reduce diagnostic time, and provide trustworthy regression protection for authentication, authorization, sessions, and vacation workflows.

### 1.3 Consequences of Not Resolving

The project cannot distinguish production defects from test contamination. CI may fail or pass unpredictably, real regressions may remain hidden, and developers may change correct production code in response to false failures.

### 1.4 Necessity

**100%.** This remains a direct quality-gate blocker. The current test execution demonstrates that the suite cannot yet distinguish application regressions from fixture contamination.

### 1.5 Conclusion

The application may run correctly manually, but its primary integration safety net is still unreliable. The test infrastructure must be repaired without changing business behavior. Reference: [spec_006](../.specify/specs/006-integration-test-isolation/spec_006-integration-test-isolation.md).

### 1.6 Proposed Solution

Give each fixture an isolated database root or unique database name, seed through the application's real service provider, and align fixture roles with the application role model (`User` and `Approver`). Remove order-dependent seed guards, then verify the complete suite across repeated runs without changing production behavior. The current run should be recorded as baseline failure evidence, not as proof of a new application defect.

## 2. Auto-Expiry Is Missing

### 2.1 Current Issue

`VacationRequest.Expire()` exists and has domain tests, but no production service invokes it. The only current call is seed-data setup, not operational processing. There is no expiry handler/job, no registered expiry hosted service, and no binding for `EXPIRY_DAYS`; a pending request can remain pending forever.

The lifecycle review also found a separate balance defect in the same path: `CreateRequestHandler` calls `ReserveDays`, while `ApproveRequestHandler` calls `DeductDays`, and both methods subtract from `Employee.Balance`. A request approved after creation is therefore charged twice. This is a live business-logic issue that must be tracked with the expiry work because both affect reservation, release, and balance invariants.

`Keep-in-mind.md` also records two delivered-but-incomplete operational surfaces that belong in this lifecycle/readiness assessment. Metrics are now exported to Prometheus and visualized in Grafana, but distributed tracing, alerting, retention/storage sizing, approval-duration measurement, and deployed-environment protection remain open. Prometheus is host-published without authentication. Logs are aggregated through Alloy and Loki, but Alloy requires root-level Docker-socket access for the local stack, Loki is host-published without authentication, log-based alerting is absent, retention is fixed at seven days with no size cap, and no automated check prevents Sensitive/PII request reasons from being logged. These are accepted development-stack tradeoffs where documented, not production-ready controls.

### 2.2 Benefits of Resolution

Implementing auto-expiry would complete the request lifecycle, release reserved days, remove stale approval items, create the required audit event, and enforce the Product Owner decision that expiry replaces auto-escalation.

### 2.3 Consequences of Not Resolving

Pending requests may remain unresolved indefinitely, employees may lose access to reserved balance, approver queues may contain stale work, and the configured `EXPIRY_DAYS` value will have no effect.

### 2.4 Necessity

**99%.** Auto-expiry is a genuine missing business capability, and the newly identified double-deduction makes the request lifecycle more urgent than the previous analysis indicated. The only reason not to use 100% is that the application remains usable for some ordinary flows.

### 2.5 Conclusion

The domain supports expiry, but the application does not operationalize it. The current observability work makes the missing behavior visible through queue-age metrics, but does not execute it. The same observability and logging stack is useful for local diagnosis but still has production security and retention gaps. The next analysis or implementation plan must cover configuration validation, an idempotent hosted process, atomic balance release and auditing, the approval reservation invariant, the GAP-007/GAP-009 operational controls, and integration/E2E evidence. Reference: `FR-016`, `T611`, `T612`, `T613`, `T614`, and `T647` in spec 001.

### 2.6 Proposed Solution

Implement and register a scoped background service that validates `EXPIRY_DAYS`, selects eligible pending requests using working-day rules, transitions them atomically, restores reserved balance, and writes one audit event per transition. Correct or explicitly redesign the reservation-to-approval balance transition, then add deterministic integration tests for expiry, invalid configuration, idempotency, create-to-approve balance, and the full E2E status change.

## 3. Audit Immutability Is Not Database-Enforced

### 3.1 Current Issue

Application code still treats `AuditRecord` as immutable, but the current migration set still has no trigger or other database guard preventing direct `UPDATE` or `DELETE` operations. The Docker and startup-migration work did not change this boundary.

### 3.2 Benefits of Resolution

Database enforcement would protect the audit trail even when accessed outside the application, support compliance, and ensure that operational history cannot be silently rewritten.

### 3.3 Consequences of Not Resolving

Anyone with database access could alter or delete audit records. Investigations, compliance reviews, and security evidence could therefore rely on data that is no longer trustworthy.

### 3.4 Necessity

**95%.** The risk is compliance and security significant, especially before any production deployment.

### 3.5 Conclusion

Application-level immutability is insufficient against direct database access. A reviewed migration with database-level protection and SQL Server integration tests is justified. Reference: spec 001 task `T640`.

### 3.6 Proposed Solution

Create the `ProtectAuditRecordImmutability` migration with SQL Server guards against `UPDATE` and `DELETE`. Test both operations against a real SQL Server database and verify that rejected attempts are observable as security events where required.

## 4. Audit History Is Not Available

### 4.1 Current Issue

Audit records are still written, but `ListAuditQuery`, its handler, and the `AuditHistory` actions and views remain absent. Existing employee request list/detail views are not an audit-history feature, so the stale-task caveat does not close this finding. Retention and PII-masking tests are also missing.

### 4.2 Benefits of Resolution

A protected audit history would provide traceability for request decisions, help investigate incidents, support compliance review, and expose whether sensitive reasons are being improperly stored or displayed.

### 4.3 Consequences of Not Resolving

Users and authorized operators cannot inspect request history through the application. Operational investigations become dependent on direct database access, and retention and privacy behavior remain unverified.

### 4.4 Necessity

**90%.** The feature is important for accountability and compliance, although the core request workflow can operate without a user-facing history view.

### 4.5 Conclusion

Audit data that cannot be safely queried is only partially useful. Implement an authorized, paginated history query and add retention and PII tests. Reference: spec 001 tasks `T641` through `T644`.

### 4.6 Proposed Solution

Add a request-scoped audit query and handler with owner or assigned-approver authorization, server-side pagination capped at 50 records, dedicated ViewModels, and employee and approver views. Add retention and masking tests before exposing the history route.

## 5. Destructive-Action Confirmation Is Reported Missing

### 5.1 Current Issue

The original finding is stale. The current repository contains `Views/Shared/Components/_ConfirmationModal.cshtml`, and the employee and approver detail views use confirmation flows for final actions. The component includes an antiforgery token; voiding additionally uses a reason field and its own confirmation form. The remaining risk is coverage consistency and browser verification, not absence of the component.

### 5.2 Benefits of Resolution

Verified confirmation on irreversible actions reduces accidental cancellations, voids, and rejections and makes the user interface consistent with the Product Owner decision requiring explicit confirmation.

### 5.3 Consequences of Not Resolving

If no equivalent confirmation exists, users may trigger irreversible transitions accidentally. If an equivalent component already exists, failing to verify its usage may leave one or more destructive paths unprotected.

### 5.4 Necessity

**10%.** The required confirmation capability is implemented. A focused review/test is still useful to prove that cancel, void, approve, and reject all invoke the intended server-side POST and state validation, but this is no longer a missing-feature finding.

### 5.5 Conclusion

Do not add another modal. Verify the existing component and the separate void/reject forms across every final action, including keyboard behavior, antiforgery failure, and server-side state validation. Reference: spec 001 task `T312`.

### 5.6 Proposed Solution

Test every cancel, void, approve, and reject path against the existing confirmation UI and the server-side state machine. Keep the shared component, and only change an individual view if a specific final action bypasses it.

## 6. CDN Scripts Lack Subresource Integrity

### 6.1 Current Issue

`_ValidationScriptsPartial.cshtml` loads jQuery, jquery-validation, and jquery-validation-unobtrusive from jsDelivr without `integrity` attributes. The browser cannot verify that the downloaded files match the approved versions.

### 6.2 Benefits of Resolution

Local hosting or verified SRI reduces supply-chain risk, improves deployment predictability, and aligns the application with Constitution section 7.2 and the spec 004 asset checklist.

### 6.3 Consequences of Not Resolving

A compromised CDN response could execute modified JavaScript in users' browsers, affect form data, alter validation, or assist a client-side attack. External CDN availability also remains a runtime dependency.

### 6.4 Necessity

**90%.** It is a release requirement before production exposure, although the current development-only environment reduces immediate operational urgency.

### 6.5 Conclusion

Prefer local hosting under `wwwroot/lib/`; otherwise use approved SRI, CSP, and `crossorigin` settings. Reference: spec 004 asset requirements.

### 6.6 Proposed Solution

Prefer local copies of the three validation libraries, remove external script references, and add a static check that rejects unapproved CDN URLs. If CDN hosting remains necessary, pin exact versions, add verified SRI hashes, set `crossorigin`, and align the CSP with the decision.

## 7. Security Headers Are Missing

### 7.1 Current Issue

The current pipeline still has no centrally managed Content Security Policy, `X-Content-Type-Options`, framing protection, or `Referrer-Policy` middleware. Metrics and log aggregation were added, but they do not provide browser security headers. The three validation scripts also remain CDN-hosted, so the asset and CSP decisions are still coupled.

### 7.2 Benefits of Resolution

These headers provide baseline browser defenses against script injection, MIME sniffing, clickjacking, and unnecessary referrer disclosure.

### 7.3 Consequences of Not Resolving

Production responses will lack required browser protections. The future CSP may also conflict with the current CDN scripts if the asset decision is not resolved first.

### 7.4 Necessity

**90%.** The headers are required before production deployment and should be coordinated with point 6.

### 7.5 Conclusion

Add centrally managed headers, test them through integration tests, and define CSP after choosing local assets or approved CDN usage. Reference: Constitution section 7.2.

### 7.6 Proposed Solution

Add one centralized response-header policy or middleware for CSP, `X-Content-Type-Options`, framing protection, and `Referrer-Policy`. Start with a restrictive policy compatible with local assets, then add integration tests for every required header in production-like responses.

## 8. Concurrency Protection Is Untested

### 8.1 Current Issue

The two current concurrency tests remain skipped because InMemory cannot model real SQL Server transactions and row-version conflicts. `DoubleVoid_OnlyOneSucceeds` is still not covered. The SQL Server container makes these tests possible, but no evidence shows that they have been activated or run.

### 8.2 Benefits of Resolution

SQL Server integration tests would verify Serializable overlap prevention, optimistic concurrency, single-success state transitions, and balance consistency.

### 8.3 Consequences of Not Resolving

Race conditions may corrupt request state or balances while all InMemory tests remain green. Production-only failures would be difficult to reproduce.

### 8.4 Necessity

**95%.** These tests protect critical financial and state-machine invariants and should run against the now-available SQL Server stack.

### 8.5 Conclusion

Create a SQL Server test profile, activate the skipped tests, and add the double-void scenario. Do not enable retry strategies until transaction compatibility is designed.

### 8.6 Proposed Solution

Run concurrency scenarios against SQL Server with separate contexts and coordinated tasks. Verify Serializable overlap prevention, `RowVersion` conflicts, and single-success voiding; keep InMemory tests for non-relational behavior only.

## 9. Authorization and IDOR Coverage Is Incomplete

### 9.1 Current Issue

Coverage is still missing for voiding another employee's request and for direct HTTP authorization bypass attempts. Session revalidation is no longer wholly missing: current tests cover account deactivation during a session and role changes/security-stamp behavior. The presence of an over-posting comment in a create-flow test is not equivalent to a passing assertion, so the input-binding guarantee remains unproven.

### 9.2 Benefits of Resolution

Dedicated HTTP-level authorization tests would prove ownership checks, fail-closed behavior, and resistance to direct-request attacks.

### 9.3 Consequences of Not Resolving

A user could potentially access or mutate another employee's request, or a stale session could perform an operation after authorization state changed.

### 9.4 Necessity

**85%.** Broken access control remains a primary security risk. The tested session-revalidation paths reduce the gap, but ownership/IDOR and over-posting evidence are still absent.

### 9.5 Conclusion

Implement focused integration tests for IDOR and over-posting, and retain the existing session-revalidation tests as regression coverage. Do not mark the authorization layer complete until direct HTTP attempts prove denial and unchanged data. Reference: spec 001 tasks `T405` and `T622`.

### 9.6 Proposed Solution

Add direct HTTP tests using authenticated users with different ownership and role states. Keep the existing checks for active status and role changes, then cover identity, assignment, route identifiers, and bindable input at operation time; assert denial and unchanged data for every unauthorized attempt.

## 10. E2E Testing Is Not Operational

### 10.1 Current Issue

The E2E project now contains a substantial role-switcher suite, including single-role denial and dual-role navigation assertions. However, the submission journey remains skipped, the tests use a hard-coded HTTPS base URL, browser installation/startup is not demonstrated in this environment, and approval, void, history, edit, expiry, and dashboard journeys remain unverified. The suite is therefore partially implemented but not an operational acceptance suite.

### 10.2 Benefits of Resolution

Operational E2E tests validate browser, Razor, JavaScript, routes, middleware, authentication, and server behavior together.

### 10.3 Consequences of Not Resolving

Browser-specific defects may reach users even when unit and HTTP integration tests pass. Critical journeys will depend on manual testing.

### 10.4 Necessity

**70%.** Role-switcher coverage is useful partial progress, but the critical submission and lifecycle journeys still lack repeatable browser evidence. The reduction reflects delivered scope, not completion.

### 10.5 Conclusion

Install and verify Playwright browsers, replace the hard-coded endpoint with controlled startup/configuration, establish deterministic data setup and failure artifacts, and implement the submission journey before extending coverage to the remaining lifecycle flows.

### 10.6 Proposed Solution

Create a repeatable E2E harness with controlled application startup, clean seeded data, configurable base URL, browser installation, traces, and failure artifacts. Keep the role-switcher tests as partial coverage, then implement submission first, followed by approval, void, history, edit, expiry, and dashboard scenarios before enabling CI execution.

## 11. Accessibility Has Not Been Verified

### 11.1 Current Issue

Responsive behavior, keyboard navigation, focus indicators, WCAG AA contrast, axe-core scans, reduced-motion support, and touch-target requirements have not been verified.

### 11.2 Benefits of Resolution

Accessibility verification improves usability across devices and abilities and provides evidence against the project's constitutional requirements.

### 11.3 Consequences of Not Resolving

Accessibility defects may exclude users, create legal or compliance exposure, and remain undiscovered until late acceptance or production use.

### 11.4 Necessity

**80%.** It is not necessarily a runtime blocker for the current development environment, but it is required for a responsible production-quality interface.

### 11.5 Conclusion

Add viewport, keyboard, contrast, axe-core, reduced-motion, and touch-target checks to the browser verification strategy. Reference: Constitution section 11.5 and spec 002.

### 11.6 Proposed Solution

Define accessibility checks as automated Playwright gates across the required viewports, complemented by axe-core scans and manual keyboard verification. Record exceptions explicitly and prevent regressions through CI reports.

## 12. Performance Targets Are Unmeasured

### 12.1 Current Issue

No load test has recorded the required p95, RPS, or concurrency results for login or other critical operations. The project now exports OpenTelemetry metrics to Prometheus and provisions Grafana dashboards, so latency and runtime signals are measurable; this is observability progress, not performance evidence. Tracing, alerting, retention sizing, and production exposure controls are still incomplete.

### 12.2 Benefits of Resolution

Representative measurements establish whether the application meets its performance targets and reveal capacity limits before production.

### 12.3 Consequences of Not Resolving

Performance regressions and capacity constraints remain unknown. Release decisions may rely on intuition rather than evidence.

### 12.4 Necessity

**75%.** Performance evidence is important for production readiness, but it should be measured only in a representative environment and is not an immediate development blocker. The new metrics reduce the instrumentation gap but do not reduce the need for representative benchmark results.

### 12.5 Conclusion

Define workloads and measure p95, p99, throughput, and concurrency on native or production-equivalent infrastructure, not the Docker development stack. Treat Prometheus/Grafana data as operational observability unless a controlled benchmark procedure and workload metadata are present. Reference: Constitution section 12.1.

### 12.6 Proposed Solution

Define representative datasets and workloads for login, request creation, lists, and dashboards. Run measurements on native or production-equivalent infrastructure, record environment and workload metadata, and publish p95, p99, RPS, concurrency, and error-rate results per release.

## 13. Code Coverage Has Not Been Measured

### 13.1 Current Issue

The project has no recorded coverage result against the required thresholds: 80% for Domain and Application and 60% measured for Infrastructure and Presentation.

### 13.2 Benefits of Resolution

Coverage reporting identifies untested critical paths and gives CI an objective quality signal.

### 13.3 Consequences of Not Resolving

Important regressions may pass without detection, and the project cannot demonstrate compliance with its testing targets.

### 13.4 Necessity

**80%.** Coverage is not a substitute for test quality, but the absence of measurement leaves a significant verification gap.

### 13.5 Conclusion

Add repeatable coverage collection and publish threshold results by project. Reference: spec 001 task `T706` and Constitution section 9.2.

### 13.6 Proposed Solution

Add a repeatable .NET coverage command to CI, collect results per project, enforce 80% thresholds for Domain and Application and 60% measured coverage for Infrastructure and Presentation, and report exclusions explicitly.

## 14. Rate Limiting Is Unverified

### 14.1 Current Issue

Rate limiting is disabled in the `Testing` environment, so the expected `429` behavior and its interaction with failed-login counters are not tested.

### 14.2 Benefits of Resolution

A dedicated fixture would verify protection against credential stuffing and confirm that rejected requests do not incorrectly increment account failure counters.

### 14.3 Consequences of Not Resolving

A configured security control may be broken or misconfigured in production without automated detection.

### 14.4 Necessity

**60%.** Rate limiting is implemented for the login endpoint with a fixed-window policy and a `429` response, but it is deliberately disabled in the `Testing` environment. The remaining issue is verification of the deployed configuration and its interaction with failed-login counters, not absence of the production feature.

### 14.5 Conclusion

Test rate limiting with a dedicated integration profile that enables the limiter while preserving database isolation. Reference: spec 004 tasks `T031` and its rate-limit acceptance checks.

### 14.6 Proposed Solution

Create a dedicated integration fixture that enables rate limiting and uses isolated state. Send controlled repeated login requests, assert `429` behavior, and verify rejected requests do not increment `AccessFailedCount`.

## 15. Infrastructure Test Project Is Missing

### 15.1 Current Issue

The architecture document lists `NovaLeave.Infrastructure.Tests`, but the repository contains no such project.

### 15.2 Benefits of Resolution

A dedicated project would provide an explicit home for EF Core, migrations, SQL Server, persistence, health-check, and infrastructure integration tests.

### 15.3 Consequences of Not Resolving

Infrastructure behavior will remain covered indirectly, inconsistently, or not at all. The architecture documentation will also remain inconsistent with the repository.

### 15.4 Necessity

**65%.** The need depends on whether the architecture requires a separate project or whether the documentation should be corrected instead.

### 15.5 Conclusion

Choose one consistent solution: create the project with meaningful infrastructure tests, or formally update the architecture documentation to remove the requirement.

### 15.6 Proposed Solution

Compare the architecture contract with the intended test strategy. If SQL Server, migrations, health checks, and persistence require independent coverage, create `NovaLeave.Infrastructure.Tests`; otherwise record an approved architecture correction and place those tests in the existing projects.

## 16. Required Documentation Directories Are Missing

### 16.1 Current Issue

`docs/adr/` now exists and contains the Docker and Prometheus decisions, so the earlier documentation-directory finding is only partially current. `docs/runbooks/` and a diagrams directory are still absent despite constitutional requirements for operational runbooks and diagrams as code.

### 16.2 Benefits of Resolution

Runbooks improve incident response and diagrams preserve architecture and workflow knowledge in a reviewable format.

### 16.3 Consequences of Not Resolving

Operational knowledge remains undocumented, onboarding becomes harder, and the repository does not fully satisfy its documentation requirements.

### 16.4 Necessity

**55%.** ADR documentation is present, but operational runbooks and diagrams remain missing. This is governance and operational debt rather than an immediate application failure.

### 16.5 Conclusion

Create `docs/runbooks/` and a diagrams directory, then add the minimum useful operational procedures and Mermaid diagrams before production operations begin. Preserve the existing ADRs. Reference: Constitution sections 12.3 and 14.

### 16.6 Proposed Solution

Create `docs/runbooks/` with startup, migration, rollback, backup, and incident procedures, and create a diagrams directory containing current architecture and workflow Mermaid diagrams. Link both from the relevant documentation and operational alerts.

## 17. Documentation Language Is Inconsistent

### 17.1 Current Issue

The Constitution specifies Spanish for documentation, while most specifications and technical artifacts are written in English.

### 17.2 Benefits of Resolution

A single documented language policy removes contributor ambiguity and makes future documentation easier to review and maintain.

### 17.3 Consequences of Not Resolving

Contributors may follow conflicting conventions, producing inconsistent project knowledge and duplicated translation effort.

### 17.4 Necessity

**45%.** This is a governance issue requiring a decision, not an urgent technical defect.

### 17.5 Conclusion

The Product Owner and architecture authority should select and document the official convention. Do not translate the repository piecemeal before that decision.

### 17.6 Proposed Solution

Record a formal documentation-language decision, update the contribution guidance, and apply the selected convention consistently to new and materially revised documents. Treat existing artifacts with a planned migration rather than an uncontrolled rewrite.

## 18. Docker Must Not Be Used for Official Benchmarks

### 18.1 Current Issue

The repository decision remains that the Docker development stack is not an official benchmark environment. Its database/runtime topology is intended for development and functional validation, not capacity or release evidence; current metrics dashboards do not change that boundary.

### 18.2 Benefits of Respecting the Decision

Excluding this stack from official benchmarks prevents misleading latency, throughput, capacity, and release-readiness conclusions.

### 18.3 Consequences of Ignoring the Decision

The team may optimize for emulation overhead, misjudge production capacity, or approve releases using non-representative measurements.

### 18.4 Necessity

**100%.** This remains mandatory whenever measurements are used for capacity planning, optimization, or release approval.

### 18.5 Conclusion

Keep Docker for development and functional validation, but benchmark only on native or production-equivalent infrastructure. No current evidence shows that Docker measurements have been presented as official benchmarks. Reference: Constitution section 12.1 and the Docker research notes.

### 18.6 Proposed Solution

Add an explicit benchmark-environment policy to the performance procedure. Require architecture, database, storage, and workload metadata before accepting results, and reject measurements from emulated Docker as release evidence.

## 19. SQL Retry Strategy Must Remain Disabled

### 19.1 Current Issue

`EnableRetryOnFailure` remains absent, as required by the current design. It is still incompatible with the user-initiated `Serializable` transaction used for overlap prevention unless that operation is redesigned around an execution strategy.

### 19.2 Benefits of Respecting the Decision

Keeping it disabled prevents a seemingly beneficial configuration change from breaking a critical production path.

### 19.3 Consequences of Ignoring the Decision

The InMemory tests may remain green while SQL Server throws an execution-strategy transaction exception in production.

### 19.4 Necessity

**90%.** The decision remains necessary until the transaction is migrated to an execution-strategy-compatible design and verified against SQL Server. Current compliance is not the same as resolved resiliency.

### 19.5 Conclusion

Do not enable retries casually. If resiliency is later required, redesign the transaction, add SQL Server concurrency tests, and document the change. Reference: Docker research R-010 and GAP-005-4.

### 19.6 Proposed Solution

Keep `EnableRetryOnFailure` disabled. If connection resiliency becomes necessary, wrap the Serializable operation in a compatible execution strategy, test transient failures and concurrency against SQL Server, and review the design before enabling it.

## 20. Docker Appsettings File Must Not Be Added

### 20.1 Current Issue

No `appsettings.Docker.json` has been added. The current Compose workflow continues to use `Development`, so the original rejected approach remains a decision boundary rather than an outstanding file defect.

### 20.2 Benefits of Respecting the Decision

Keeping the environment as `Development` preserves predictable local behavior while environment variables override the connection string and keep secrets out of tracked files.

### 20.3 Consequences of Ignoring the Decision

Developers may unknowingly run with production-like behavior, lose seeded accounts, receive less useful diagnostics, or accidentally expose configuration assumptions.

### 20.4 Necessity

**85%.** This decision protects the correctness and usability of the approved development workflow, and current configuration continues to honor it.

### 20.5 Conclusion

Keep Docker on `Development` and use environment-variable configuration. Revisit only through an explicit architecture decision. Reference: Docker decision documentation.

### 20.6 Proposed Solution

Retain `ASPNETCORE_ENVIRONMENT=Development` for the local Compose workflow and supply secrets and connection strings through environment variables. Document this as the supported approach and reject Docker-specific appsettings unless a new ADR defines equivalent behavior.

## 21. Startup Migrations Are Development-Only

### 21.1 Current Issue

`DatabaseInitializer` still limits automatic migration to Development, and tests cover the production guard. A reviewed production migration and rollback procedure has not been defined, so the guard is correct but the deployment process remains incomplete. Multiple replicas could also attempt migrations concurrently if this boundary is ignored.

### 21.2 Benefits of Resolution

A formal deployment migration process enables controlled, reversible schema changes and reduces operational risk.

### 21.3 Consequences of Not Resolving

Production deployments may rely on unsafe manual changes, lack rollback guidance, or encounter migration races in multi-instance environments.

### 21.4 Necessity

**90%.** The application can remain development-only without this, and the guard is currently correct, but the operational gap must be closed before deployment.

### 21.5 Conclusion

Preserve the Production guard and define a reviewed migration and rollback runbook before any production release. Reference: GAP-005-3 and Constitution section 16.2.

### 21.6 Proposed Solution

Keep automatic startup migration limited to Development. For production, define a single-owner deployment migration step with prechecks, backup, rollback boundaries, multi-replica coordination, and post-migration verification.

## 22. Host Builds Require .NET 10 or Docker

### 22.1 Current Issue

The solution targets `net10.0`. The current environment has SDK `10.0.400`, so a host build is available here; older SDKs still cannot build the solution directly. Docker remains the reproducible fallback, but no `global.json` or CI gate pins/enforces the required SDK.

### 22.2 Benefits of Resolution

Documented SDK requirements and a standard container build reduce machine-specific failures and make onboarding reproducible.

### 22.3 Consequences of Not Resolving

Developers on machines without .NET 10 may waste time diagnosing host-toolchain errors. The current host can build with SDK `10.0.400`, so Docker is a fallback and reproducibility mechanism rather than the only local option in this environment.

### 22.4 Necessity

**50%.** The prerequisite is satisfied on the current host and the Docker fallback exists, but onboarding and CI enforcement remain incomplete. This is an environment issue, not an application runtime defect.

### 22.5 Conclusion

Document and enforce the .NET 10 prerequisite while retaining the Docker build path. Add SDK/CI validation when the build workflow is formalized; do not alter the target framework solely to accommodate an outdated host SDK.

### 22.6 Proposed Solution

Pin and document the required .NET SDK, add a clear container build command, and add CI validation for the solution. Use SDK checks or `global.json` where appropriate, without lowering the target framework.

## 23. Housekeeping and Naming Deviations

### 23.1 Current Issue

`TimeProvider.System` is registered twice, and database tables use plural names despite a documented preference for singular names. The plural convention is already applied consistently.

### 23.2 Benefits of Resolution

Removing the duplicate registration reduces configuration noise. Documenting the plural naming convention eliminates ambiguity without requiring a risky migration.

### 23.3 Consequences of Not Resolving

The duplicate registration may hide future configuration mistakes, and the undocumented naming deviation may cause unnecessary discussions or inconsistent future schema decisions.

### 23.4 Necessity

**25%.** These remain low-risk housekeeping issues and are not blockers for application operation. The duplicate registration persists, while the plural table convention is consistent but undocumented as an intentional deviation.

### 23.5 Conclusion

Remove the redundant registration during routine maintenance and document plural table names as an accepted, consistent deviation. Do not prioritize a schema migration solely for this convention.

### 23.6 Proposed Solution

Remove the duplicate `TimeProvider` registration in a routine cleanup and record the plural table convention in the architecture documentation. Avoid a database rename migration unless a future business or operational requirement justifies its risk.

## Overall Conclusion

The 23 findings do not have equal status. The presentation suite still fails for the documented isolation and role reasons; auto-expiry and the approval double-deduction are active business risks; audit protection, concurrency validation, CDN integrity, security headers, and missing authorization evidence remain high-priority controls. The observability/logging work is useful but still carries the concrete GAP-007 and GAP-009 production-readiness risks documented above. Audit history, accessibility, coverage, performance, rate-limiting verification, infrastructure-test organization, and operational documentation are verification or readiness debt.

The original snapshot is materially stale in four ways: the destructive-action confirmation component now exists and is in use; session revalidation has meaningful tests; a role-switcher E2E suite and metrics/Grafana observability have been added; and the ADR directory plus the Docker, retry, appsettings, and development-migration decisions are present or being honored. These changes reduce some percentages, but they do not establish production readiness because the main test suite remains red and the critical lifecycle, security, and operational gaps remain.

The project can run without resolving every finding, but running successfully is not equivalent to being verifiable, secure, operationally ready, or protected against regression. The percentages in this document express the necessity of addressing each point, not the probability that the application will fail immediately. No new numbered issue was added: the balance defect is deliberately recorded under point 2 because it belongs to the same request-lifecycle invariant.

No implementation was performed. This document is an analysis and prioritization artifact only.
