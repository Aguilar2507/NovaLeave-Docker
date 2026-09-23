# Implementation Plan: Metrics and Observability with Prometheus

**Branch**: `prometheus-implementation` | **Date**: 2026-09-22 | **Spec**: [spec_007-prometheus-observability.md](./spec_007-prometheus-observability.md)

---

## Summary

Instrument NovaLeave with OpenTelemetry metrics, expose a container-internal Prometheus endpoint, emit vacation-request and authentication business metrics through decorators over existing audit contracts, and extend the Compose stack with pinned Prometheus and Grafana containers carrying a provisioned dashboard.

Development environment only. Tracing, alerting and any deployment are out of scope and recorded as gaps.

---

## Technical Context

**Language/Version**: C# / .NET 10
**New dependencies**: `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.Runtime`, `OpenTelemetry.Instrumentation.EntityFrameworkCore`, `OpenTelemetry.Exporter.Prometheus.AspNetCore` (beta — accepted, R-001)
**New infrastructure**: Prometheus, Grafana (pinned; multi-arch, native on arm64)
**Storage**: named volumes for Prometheus and Grafana data
**Testing**: xUnit. Decorator behavior and the safety requirement (FR-011) are unit-testable and will be tested
**Constraints**: no business-code modification (FR-010, SC-006); no PII or unbounded labels (FR-013, FR-014); metrics failure must never affect the user (FR-011)

---

## Common Artifacts (DO NOT DUPLICATE)

- **Architecture**: [`specs/common/architecture.md`](../common/architecture.md) — Observability baseline this delivers half of
- **Security**: [`specs/common/security.md`](../common/security.md) — diagnostics exposure, PII classification
- **Docker stack**: [`specs/005-docker-containerization/`](../005-docker-containerization/) — the stack being extended

---

## Constitution Check

| Gate | Status |
|------|--------|
| §8 Observability | ✅ Delivers metrics |
| architecture.md — metrics baseline | ✅ Half-delivered; tracing remains (GAP-007-1) |
| §3.2 / §12.2 — new infrastructure needs an approved decision | ✅ ADR-002 authored with this feature |
| §7.2 — diagnostics not publicly exposed | ✅ Container-internal; production requirements documented |
| §7.2 — version pinning | ✅ Prometheus and Grafana pinned, verified against the registry |
| §7.2 — secrets outside the repository | ✅ Grafana credentials from `.env` |
| §7.3 — no sensitive data in metrics | ✅ Closed-enum labels; identifiers prohibited; verified by scrape inspection (SC-004) |
| §2.I — Clean Architecture | ✅ Decorators in Infrastructure implementing Application contracts; Domain untouched |
| §2.II — simplicity before speculative abstraction | ✅ Decorators over existing contracts, not a new eventing layer |
| §2.VIII — async I/O | ✅ Async throughout; gauge callbacks bounded |
| §14 — ADR and documentation | ✅ ADR-002 + metrics catalogue |
| §16.1 — Git workflow | ✅ Branch `prometheus-implementation`, Conventional Commits |
| §12.1 — performance targets | ⚠️ Becomes measurable, not verified — and not verifiable on emulated arm64 (GAP-007-4) |

### Complexity Tracking

| Item | Justification |
|------|---------------|
| Two decorator classes rather than direct handler edits | Fewer touch points, automatic coverage of future transitions, and the only approach satisfying FR-010. Alternatives assessed in R-002 |
| `ObservableGauge` performing database queries | The only way to report current state; risk contained by a bounded timeout and graceful degradation (R-003, FR-012) |
| Accepting a beta package | Explicitly agreed with the PO; rationale and risk in R-001 and ADR-002 |

---

## Project Structure

```text
src/NovaLeave.Infrastructure/
├── NovaLeave.Infrastructure.csproj          # MODIFIED — OpenTelemetry packages
└── Observability/                           # NEW
    ├── NovaLeaveMetrics.cs                  # Meter + instrument definitions
    ├── MetricsStateTransitionAuditService.cs# Decorator — transition counters
    ├── MetricsAuthenticationAuditService.cs # Decorator — auth outcome counters
    └── VacationRequestMetricsCollector.cs     # ObservableGauges backed by the database

src/NovaLeave.Presentation.Web/
└── Program.cs                               # MODIFIED — OpenTelemetry wiring, /metrics, decorator registration

docker/                                      # NEW
├── prometheus/prometheus.yml                # Scrape configuration
└── grafana/provisioning/
    ├── datasources/prometheus.yml
    └── dashboards/
        ├── dashboards.yml                   # Provider definition
        └── novaleave-overview.json          # The dashboard

compose.yaml                                 # MODIFIED — prometheus + grafana services, volumes
.env.example                                 # MODIFIED — Grafana credentials, ports
tests/NovaLeave.Application.Tests/
└── MetricsDecoratorTests.cs                 # NEW — pass-through + failure isolation
```

---

## Phase Breakdown

### Phase 0 — Research *(complete)*

R-001 … R-009 resolved in [`research.md`](./research.md). No open questions block implementation.

### Phase 1 — Instrumentation Foundation

Get infrastructure metrics flowing before touching anything business-related, so later failures are unambiguous.

1. Add OpenTelemetry packages, versions verified against the registry.
2. Define `NovaLeaveMetrics` — the meter and its instruments, names following Prometheus conventions (FR-005).
3. Wire OpenTelemetry in `Program.cs`: ASP.NET Core, EF Core and runtime instrumentation, Prometheus exporter, `/metrics` mapped without authentication.
4. Confirm `/metrics` returns exposition-format text containing the built-in meters.

**Exit criteria**: `/metrics` serves real data from inside the container; solution builds clean; existing tests unchanged.

### Phase 2 — Business Metrics

5. `MetricsStateTransitionAuditService` — decorator incrementing the transition counter, then delegating.
6. `MetricsAuthenticationAuditService` — decorator counting auth outcomes without leaking failure reasons (FR-009).
7. `VacationRequestMetricsCollector` — pending count and oldest-pending age, scoped `DbContext`, bounded timeout, no value on failure (FR-012, FR-008).
8. Register decorators so the metrics wrapper resolves in place of the concrete service.
9. Unit tests: pass-through behavior, counter increment, and **failure isolation** — a throwing meter must not fail the audit call (FR-011).

**Exit criteria**: transitions and logins move counters; gauges reflect database state; no handler modified.

### Phase 3 — Prometheus and Grafana

10. `docker/prometheus/prometheus.yml` — scrape `web:8080/metrics`.
11. Grafana provisioning — datasource and dashboard provider, mounted read-only.
12. `novaleave-overview.json` — dashboard covering request rate, latency, errors, transitions, pending queue and oldest-pending age.
13. `compose.yaml` — both services pinned, with volumes; `/metrics` stays unpublished.
14. `.env.example` — Grafana credentials and ports.

**Exit criteria**: `docker compose up` brings up a stack where Prometheus scrapes successfully and the dashboard is present unaided.

### Phase 4 — Verification and Documentation

15. Verify every success criterion, including the negative one: `/metrics` unreachable from the host, reachable from Prometheus.
16. Inspect a full scrape for PII and count active series (SC-004, SC-007).
17. Metrics catalogue — every metric, type, labels, example query.
18. ADR-002.
19. Update `architecture.md`, README, and `docs/Keep-in-mind.md`.
20. Final Constitution Check.

**Exit criteria**: every success criterion either met or explicitly recorded as outstanding.

---

## Verification Strategy

| Criterion | Method |
|-----------|--------|
| SC-001 | Exercise the app, confirm rate/latency/errors in Grafana |
| SC-002 | Create then approve a request; observe counters and gauge |
| SC-003 | `curl` `/metrics` from the host (must fail) and from the Prometheus container (must succeed) |
| SC-004 | Dump a full scrape; grep for seeded emails, names, GUIDs |
| SC-005 | Run the suites; compare against the spec_005 baseline |
| SC-006 | `git diff --stat` — no Domain file, no handler |
| SC-007 | Count series in the scrape output |
| SC-008 | `down -v` then `up`; confirm empty history and a working stack |

---

## Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Beta exporter introduces a breaking API change | Rework on upgrade | Version pinned; risk accepted and recorded in ADR-002 |
| Gauge database query stalls a scrape | Scrapes time out, metrics gap | Bounded timeout, degrade to no value (R-003) |
| A future label introduces unbounded cardinality | Prometheus memory growth | Prohibited labels listed explicitly in R-005 and the catalogue |
| Decorator registration ordering subtly bypasses metrics | Silent under-reporting | Unit test asserts the decorator is what DI resolves |
| Two more containers slow cold start | Exceeds spec_005 NFR-001 | Both images multi-arch and small; cold start re-measured in Phase 4 |
| Metrics exception breaks a business operation | Direct violation of FR-011 | Decorators guard their own instrumentation; unit-tested |

---

## Out of Scope

Distributed tracing, Alertmanager and alert rules, log aggregation, remote-write or long-term storage, production deployment, Grafana hardening, and per-employee metrics of any kind.
