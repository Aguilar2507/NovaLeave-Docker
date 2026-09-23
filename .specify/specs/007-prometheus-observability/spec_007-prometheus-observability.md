# Feature Specification: Metrics and Observability with Prometheus

**Feature Branch**: `prometheus-implementation`
**Created**: 2026-09-22
**Status**: Draft — Ready for Implementation
**Constitution Reference**: NovaLeave — Constitution v4.0.0
**Architecture Reference**: `specs/common/architecture.md`
**Depends on**: `005-docker-containerization` (the Compose stack this extends)

---

## Overview

This specification adds metrics collection and visualization to NovaLeave: the application is instrumented with OpenTelemetry, exposes a Prometheus-format `/metrics` endpoint, and the development stack gains Prometheus and Grafana containers with a provisioned dashboard.

`specs/common/architecture.md` lists *"Metrics and tracing (baseline, not optional)"* under Observability. Nothing had delivered it. Constitution §8 requires observability, and §12.1 sets latency targets (p95 < 300 ms for focused operations, < 500 ms for standard MVC pages) that **no one can currently verify, because nothing measures them**.

Two kinds of metric are in scope, and the second is the point of the feature:

- **Infrastructure metrics** — HTTP request rate, duration and error rate; EF Core query and connection-pool behavior; .NET runtime GC, thread pool and memory. These come from meters .NET already emits and cost nothing but wiring.
- **Business metrics** — vacation request state transitions, pending queue depth, the age of the oldest unresolved request, and authentication outcomes. These answer questions about NovaLeave rather than about servers.

**Out of scope**: distributed tracing (the same OpenTelemetry foundation will carry it, but it is a separate specification), Alertmanager and alert rules, log aggregation, long-term metric storage or remote-write, production deployment of any of these components, and per-employee metrics of any kind.

---

## Key Decisions (Documented)

Agreed with the Product Owner before drafting:

- **OpenTelemetry with the Prometheus exporter**, using the classic pull model. Accepted with the known caveat that `OpenTelemetry.Exporter.Prometheus.AspNetCore` is a long-standing beta package (see `research.md` R-001).
- **Infrastructure and business metrics together.** Infrastructure alone would show that a page is slow without showing that approvals have stalled.
- **Prometheus and Grafana**, with the datasource and dashboard provisioned as code rather than clicked together by hand.
- **`/metrics` is never published to the host.** It is reachable only over the Docker network, by Prometheus.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Operator Sees Whether the Application Is Healthy (Priority: P1)

As someone running NovaLeave, I want to see request rates, latencies and error rates over time, so that I can tell whether the application is behaving and whether it meets the latency targets the Constitution sets.

**Why this priority**: Without it, §12.1's targets are unmeasurable assertions, and "is it slow?" is answered by opinion.

**Independent Test**: Start the stack, browse the application, then open Grafana and observe request rate and latency for the paths just exercised.

**Acceptance Scenarios**:

1. **Given** the stack is running, **When** requests are served, **Then** HTTP request count, duration distribution and status-code breakdown shall be queryable within one scrape interval.
2. **Given** the stack is running, **When** an operator opens Grafana, **Then** a provisioned NovaLeave dashboard shall be present without any manual setup.
3. **Given** a request returns 5xx, **When** the operator inspects the dashboard, **Then** the error shall be visible and distinguishable from 4xx.
4. **Given** EF Core executes queries, **When** the operator inspects the dashboard, **Then** database query activity and connection usage shall be visible.
5. **Given** Prometheus is scraping, **When** the application container stops, **Then** the target shall report as down rather than silently going quiet.

---

### User Story 2 - Operator Sees What Is Happening to Vacation Requests (Priority: P1)

As someone responsible for NovaLeave, I want to see how many requests are being created, approved, rejected, cancelled and voided, and how many are waiting, so that I can tell whether the process itself is working.

**Why this priority**: This is the reason the feature exists. A vacation system where requests silently pile up unapproved is broken in a way no CPU graph reveals.

**Independent Test**: Create a request through the UI, approve it as the approver, and watch both the transition counters and the pending-queue gauge change.

**Acceptance Scenarios**:

1. **Given** a request is created, **When** the metric is scraped, **Then** a transition counter labelled with the originating and resulting state shall increase.
2. **Given** requests are approved, rejected, cancelled or voided, **When** metrics are scraped, **Then** each transition shall be counted separately and distinguishably.
3. **Given** requests are awaiting approval, **When** metrics are scraped, **Then** the current number of `Pending` requests shall be reported as a gauge.
4. **Given** a request has been `Pending` for some time, **When** metrics are scraped, **Then** the age of the **oldest** unresolved request shall be reported, so a stalled queue is visible.
5. **Given** a user signs in successfully or fails to, **When** metrics are scraped, **Then** authentication outcomes shall be counted by result.
6. **Given** any business metric is exported, **When** its labels are inspected, **Then** no employee identifier, email, name or request reason shall appear in any label or metric name.

---

### User Story 3 - Instrumentation Does Not Change Application Behavior (Priority: P1)

As a developer, I want metrics collection to be strictly additive, so that observability cannot become the cause of an incident.

**Why this priority**: Instrumentation that can break a business transaction is worse than no instrumentation. This constrains the design of every other story.

**Independent Test**: Run the existing test suites before and after; confirm no business-logic file changed and no result differs.

**Acceptance Scenarios**:

1. **Given** the metrics subsystem fails or throws, **When** a user performs any action, **Then** the action shall complete normally and the failure shall not surface to the user.
2. **Given** instrumentation is added, **When** the diff is reviewed, **Then** no domain entity, handler, validator or authorization rule shall have been modified.
3. **Given** the existing test suites are run, **When** results are compared with the pre-change baseline, **Then** they shall be unchanged.
4. **Given** a metric is collected by querying the database, **When** the query is slow or fails, **Then** the scrape shall degrade rather than block application traffic.

---

### User Story 4 - Diagnostics Are Not Publicly Exposed (Priority: P1)

As the party accountable for security, I want the metrics endpoint unreachable from outside the container network, so that adding observability does not add an information-disclosure surface.

**Why this priority**: Constitution §7.2 prohibits publicly exposed diagnostics. Metrics reveal internal route names, traffic volumes, error patterns and infrastructure shape — useful to an attacker mapping a system.

**Independent Test**: From the host, attempt to reach `/metrics` on the published application port and confirm it is unreachable; from the Prometheus container, confirm it is reachable.

**Acceptance Scenarios**:

1. **Given** the stack is running, **When** `/metrics` is requested from the host on the application's published port, **Then** it shall not return metric data.
2. **Given** the stack is running, **When** Prometheus scrapes over the container network, **Then** it shall succeed.
3. **Given** the metrics endpoint is served, **When** it is requested, **Then** no authentication cookie or user context shall be required or consumed.
4. **Given** the documentation is read, **When** production deployment is considered, **Then** the required protection for a deployed environment shall be stated explicitly.

---

### Edge Cases

- **Metric cardinality explosion**: a label whose values are unbounded (request id, employee id, raw URL path) would grow Prometheus's memory without limit. Labels must be drawn from small, fixed sets.
- **Route templates vs raw paths**: ASP.NET Core's built-in metrics use route templates (`/EmployeeRequests/Detail/{id}`). Raw paths would be unbounded — this must not be "improved" into raw paths.
- **Scrape-time database queries**: a gauge that queries the database on every scrape multiplies load by scrape frequency and can hang a scrape if the database is slow.
- **Empty queue**: with no pending requests, "age of oldest pending" has no value. Reporting `0` would be indistinguishable from "a request was just created", which is a meaningfully different situation.
- **Counter reset on restart**: Prometheus counters reset when a process restarts; queries must use `rate()`/`increase()` rather than raw totals.
- **Clock skew between containers**: Prometheus timestamps scrapes itself, so application clock drift affects business timestamps but not scrape timing.
- **Histogram bucket mismatch**: default histogram buckets are tuned for sub-second HTTP latency. A duration measured in days would land entirely in the overflow bucket and be useless.

---

## Requirements *(mandatory)*

### Functional Requirements

**Instrumentation**

- **FR-001**: The application MUST be instrumented with OpenTelemetry metrics and expose them in Prometheus exposition format.
- **FR-002**: The built-in ASP.NET Core meters MUST be collected, including HTTP server request duration and active request count.
- **FR-003**: The built-in EF Core meters MUST be collected.
- **FR-004**: .NET runtime metrics (GC, thread pool, memory, exceptions) MUST be collected.
- **FR-005**: All custom metric names MUST follow Prometheus conventions: lowercase `snake_case`, a `novaleave_` prefix, base units (seconds, not milliseconds), and a `_total` suffix on counters.

**Business metrics**

- **FR-006**: Every vacation request state transition MUST increment a counter labelled with the originating and resulting status.
- **FR-007**: The current count of `Pending` requests MUST be exported as a gauge.
- **FR-008**: The age of the oldest unresolved `Pending` request MUST be exported as a gauge **in seconds**, and MUST be absent — not zero — when no request is pending. *(Amended during Phase 1: originally specified in days, which contradicted FR-005's own requirement to use base units. Grafana formats seconds as days for display.)*
- **FR-009**: Authentication outcomes MUST be counted by result (success, failure), without distinguishing failure reasons in a way that reveals whether an account exists (spec_004 existence hiding).
- **FR-010**: Business metrics MUST be emitted without modifying any handler, domain entity, validator or authorization rule.

**Safety**

- **FR-011**: A failure anywhere in metrics collection MUST NOT fail, block or alter the business operation being measured.
- **FR-012**: Metrics that require a database query MUST execute with a bounded timeout and MUST degrade to reporting no value rather than blocking.
- **FR-013**: No metric name or label value may contain an employee identifier, email, name, request reason, or any other personal data (Constitution §7.3).
- **FR-014**: Label values MUST be drawn from bounded sets; unbounded values are prohibited.

**Stack**

- **FR-015**: The Compose stack MUST include Prometheus, pinned to an explicit version, with a named volume for its data.
- **FR-016**: The Compose stack MUST include Grafana, pinned to an explicit version, with its datasource and at least one NovaLeave dashboard provisioned from files in the repository.
- **FR-017**: Prometheus MUST scrape the application over the Docker network.
- **FR-018**: `/metrics` MUST NOT be published to the host (FR-018 supersedes convenience; see US4).
- **FR-019**: Grafana's admin credentials MUST come from the git-ignored `.env`, with placeholders in `.env.example`.
- **FR-020**: The existing `docker compose up` workflow MUST continue to work unchanged, and `down -v` MUST reset metric storage along with everything else.

**Documentation**

- **FR-021**: An ADR MUST record the adoption of Prometheus and Grafana as infrastructure (Constitution §3.2, §12.2, §14).
- **FR-022**: Documentation MUST list every exported metric, its type, its labels, and an example query.
- **FR-023**: Documentation MUST state what protecting `/metrics` requires in a deployed environment.

### Non-Functional Requirements

- **NFR-001**: Instrumentation SHOULD add less than 5 ms to p95 request latency.
- **NFR-002**: A scrape SHOULD complete in under 1 second.
- **NFR-003**: Total active time series SHOULD remain under 5,000 for the development stack.
- **NFR-004**: The added containers SHOULD not prevent the stack from starting within the existing cold-start budget (spec_005 NFR-001, under 3 minutes).

### Key Entities

No domain entity is introduced or modified. The feature adds infrastructure services, decorators over two existing Application contracts, and container definitions.

---

## Success Criteria *(mandatory)*

- **SC-001**: After exercising the application, request rate, latency distribution and error rate are visible in Grafana without manual configuration.
- **SC-002**: Creating and then approving a request is visible in the transition counters, and the pending gauge rises and falls accordingly.
- **SC-003**: `/metrics` cannot be reached from the host; Prometheus can reach it.
- **SC-004**: No employee identifier, email, name or reason appears anywhere in the exported metric text. Verified by inspecting a full scrape.
- **SC-005**: The existing test suites produce the same results as before this feature.
- **SC-006**: No file under `src/NovaLeave.Domain/` and no existing handler is modified.
- **SC-007**: Total active series stay under 5,000 with the seeded dataset.
- **SC-008**: `docker compose down -v && docker compose up` yields a working stack with empty metric history.

---

## Assumptions

- The development stack is the target. Nothing here is deployed, and Prometheus retention is whatever the container default gives.
- Metric history is disposable; the volume exists for convenience across restarts, not durability.
- Grafana runs unauthenticated-by-default configuration only insofar as the admin password comes from `.env`; it is not hardened for exposure.
- Measurements taken on this stack are **not** valid for Constitution §12.1 performance verification while SQL Server runs emulated on arm64 (spec_005 research R-002).

---

## Constitution Compliance Notes

| Rule | Relevance | Compliance |
|------|-----------|------------|
| §8 Observability | Directly implements metrics | Implements |
| architecture.md — *"Metrics and tracing (baseline, not optional)"* | Delivers the metrics half | Implements |
| §3.2 / §12.2 — new infrastructure requires an approved decision | Prometheus and Grafana are new infrastructure | ADR-002 authored with this feature |
| §7.2 — diagnostics not publicly exposed | `/metrics` | Container-internal only |
| §7.2 — explicit version pinning | Prometheus and Grafana images | Pinned |
| §7.2 — secrets outside the repository | Grafana admin password | From `.env`, template committed |
| §7.3 — no unnecessary sensitive data in metrics | Business metric labels | Bounded, non-identifying labels only |
| §2.II — simplicity before speculative abstraction | Decorators over existing contracts rather than a new abstraction layer | Complies |
| §14 — ADR and documentation | | ADR-002 + metrics catalogue |

### Recorded Gaps

- **GAP-007-1**: Distributed tracing is not delivered. `architecture.md`'s Observability baseline remains half-met.
- **GAP-007-2**: No alerting. A stalled approval queue is visible only if somebody looks at the dashboard.
- **GAP-007-3**: Nothing is deployed, so §12.3 operational targets remain unaddressed (inherited from GAP-005-1).
- **GAP-007-4**: Constitution §12.1 latency targets become *measurable* but are not yet *verified*, and cannot be verified on emulated arm64 hardware.
- **GAP-007-6** *(found during Phase 3)*: Prometheus is published to the host without authentication, so the metric data is reachable locally even though `/metrics` is not. Acceptable for a localhost development stack; unacceptable in any deployed environment, where Prometheus must sit behind authentication or on an internal-only network with Grafana as the sole exposed entry point. See `research.md` → R-006 caveat.
- **GAP-007-5**: Approval-duration-as-histogram is deferred. Recording it per event requires either widening `IStateTransitionAuditService` or an extra database read per transition; the oldest-pending-age gauge delivers most of the operational value without either. See `research.md` R-004.
