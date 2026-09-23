# ADR-002: Metrics and Observability with Prometheus, OpenTelemetry and Grafana

**Status**: Accepted
**Date**: 2026-09-23
**Deciders**: Product Owner, Development Team
**Constitution Reference**: v4.0.0 — §3.2 (Approved Stack), §7.2 (Secure Configuration), §7.3 (Sensitive Data), §8 (Observability), §12.2 (Infrastructure), §14 (ADRs)
**Specification**: [spec_007-prometheus-observability](../../.specify/specs/007-prometheus-observability/spec_007-prometheus-observability.md)
**Supersedes nothing. Builds on**: [ADR-001](./ADR-001-docker-local-development-environment.md)

---

## Context

`specs/common/architecture.md` lists *"Metrics and tracing (baseline, not optional)"* under Observability. No feature had delivered either. Constitution §12.1 sets latency targets — p95 < 300 ms for focused operations, < 500 ms for standard pages — that **nothing in the system could measure**, so they were assertions rather than requirements.

Meanwhile the project has two invariants with no passing test: optimistic concurrency and overlap prevention, both skipped in `ConcurrencyTests.cs` (`docs/Keep-in-mind.md` item 8). And it has a missing feature — auto-expiry — where `Pending` requests never expire because nothing calls `VacationRequest.Expire()` (item 2). Neither condition is visible from anywhere.

Prometheus and Grafana are **not** in the Constitution's approved stack (§3.2 lists Docker, GitHub Actions and Azure-ready deployment), and §12.2 requires that infrastructure is not added without an approved decision. Hence this ADR.

## Decision

Instrument the application with **OpenTelemetry**, export in **Prometheus** exposition format, collect with Prometheus and visualise with **Grafana**, all within the development Compose stack from ADR-001.

Specifically:

1. **OpenTelemetry with the Prometheus exporter**, classic pull model.
2. **Meters subscribed by name**, not through instrumentation packages — ASP.NET Core, EF Core and the .NET runtime all emit natively.
3. **Business metrics via decorators** over `IStateTransitionAuditService` and `IAuthenticationAuditService`, so no business code changes.
4. **Queue gauges from a `BackgroundService`** refreshing a cached snapshot, never from the scrape path.
5. **`/metrics` on a dedicated, unpublished port (9464)**, separate from the application port.
6. **Prometheus and Grafana pinned** (`v3.14.0`, `13.2.2`), with Grafana's datasource and dashboard provisioned from files in the repository.

## Alternatives Considered

| Decision | Alternatives rejected | Reason |
|----------|----------------------|--------|
| OpenTelemetry + Prometheus exporter | **`prometheus-net`** (stable) | Separate ecosystem from OpenTelemetry, no path to tracing, extra work to surface .NET's native meters |
| | **OTLP push to Prometheus 3.x** | All-stable packages, but inverts the pull model and loses the `up` metric — the signal that distinguishes "down" from "idle" |
| Decorators over audit contracts | **Edit each handler** | Six edits now and a silent gap whenever a seventh transition is added |
| | **Domain events** | Correct long-term, but no event infrastructure exists; introducing one for metrics is the speculative abstraction §2.II prohibits |
| `BackgroundService` for gauges | **Query inside the gauge callback** | `ObservableGauge` callbacks are synchronous; this would require sync-over-async, which §2.VIII prohibits |
| Separate metrics port | **Same port as the application** | **Proven insecure during verification — see Consequences** |
| Provisioned dashboards | **Configure in the Grafana UI** | Lives in a container volume, vanishes on `down -v`, cannot be code-reviewed |

## Consequences

### Positive

- §12.1's latency targets are measurable for the first time.
- Optimistic concurrency failures are now observable, giving the first live signal for an invariant whose tests are skipped.
- The oldest-pending-request gauge makes the missing auto-expiry feature visible instead of silent.
- Rate limiting became observable — its tests are disabled in `Testing`, so nothing previously confirmed it worked.
- Only **one** pre-release dependency, and it is the exporter: replaceable without touching any instrumentation, since nothing depends on its API beyond one call.
- Zero changes to domain logic, handlers, validators or authorization rules.

### Negative

- **`OpenTelemetry.Exporter.Prometheus.AspNetCore` is beta** (`1.19.1-beta.1`). It has been beta for years because the OpenTelemetry Prometheus compatibility specification is not declared stable, not because the package is unstable, and it is widely used in production. The risk is a breaking API change on upgrade; the version is pinned, and the blast radius is a single call site.
- Two more containers to run and understand.
- Queue gauges are up to 15 seconds stale by design.
- **Prometheus is published to the host without authentication** — see below.

### Neutral

- Metric history is disposable and resets with `docker compose down -v`.

## Security Analysis

### What verification found, and what changed because of it

The original design served `/metrics` on the application port and relied on "Compose does not forward `/metrics` specifically". **The Phase 4 negative test proved that wrong**: `curl http://localhost:8080/metrics` from the host returned HTTP 200 and 66 KB of metric data. Publishing a port publishes *every path on it*.

This was a genuine exposure of exactly what Constitution §7.2 prohibits — diagnostics disclosing route names, traffic volumes and error patterns — and it existed because the control was reasoned about rather than tested.

**The design was changed, not documented around.** `/metrics` now binds to port 9464 via `RequireHost`, and compose publishes only 8080. Verified four ways:

| Check | Result |
|-------|--------|
| `/metrics` from host on app port | **HTTP 404** |
| Port 9464 from host | **Connection refused** |
| `web:9464/metrics` from the Prometheus container | **Succeeds** |
| Application from host | **HTTP 200**, unaffected |

### What remains exposed, and why

Prometheus itself is published on port 9090 **with no authentication**, and it holds every series it scraped. Anyone who can reach `localhost:9090` can read the same data through `/api/v1/query`.

Accepted for a local development stack: it binds to localhost on one machine, the data describes seeded development records, and the expression browser is most of Prometheus's day-to-day value. Grafana does require a login.

**This must not be carried into a deployed environment.** There, Prometheus belongs behind authentication or on an internal-only network with Grafana as the sole exposed entry point. Recorded as **GAP-007-6**. Anyone wanting the stricter posture locally can delete the `ports:` block from the `prometheus` service.

### PII and cardinality

Constitution §7.3 classifies request reasons and personal identifiers as Sensitive. Every label is drawn from a closed enum: `from_status`/`to_status` (≤36 combinations) and `result` (2 values). Gauges carry no labels at all.

The authentication metric deliberately carries **no failure reason**: spec_004 requires that failures never reveal whether an account exists, and `/metrics` has no authentication in front of it. Verified by driving both a wrong password and a nonexistent account — both report identically as `result="failure"`.

A full scrape was audited for seeded emails, employee names, passwords and request reasons: **zero hits**. The only GUID present is OpenTelemetry's generated `service_instance_id`.

## Compliance

| Rule | Status |
|------|--------|
| §8 Observability | ✅ Metrics delivered; tracing outstanding (GAP-007-1) |
| architecture.md — metrics baseline | ✅ Half delivered |
| §3.2 / §12.2 — infrastructure needs an approved decision | ✅ This ADR |
| §7.2 — diagnostics not publicly exposed | ✅ For `/metrics`, verified by negative test. ⚠️ Prometheus itself published locally (GAP-007-6) |
| §7.2 — explicit version pinning | ✅ `v3.14.0`, `13.2.2`, verified against the registry |
| §7.2 — secrets outside the repository | ✅ Grafana credentials from git-ignored `.env` |
| §7.3 — no sensitive data in metrics | ✅ Audited scrape, zero hits |
| §2.I — Clean Architecture | ✅ Decorators in Infrastructure implementing Application contracts; Domain untouched |
| §2.II — simplicity before abstraction | ✅ Decorators, not an eventing layer |
| §2.VI — testable time | ✅ `TimeProvider` for refresh interval and timeout |
| §2.VIII — async I/O end-to-end | ✅ The reason gauges use a `BackgroundService` |
| §14 — ADR and documentation | ✅ This ADR + metrics catalogue |
| §16.1 — Git workflow | ✅ Branch `prometheus-implementation`, Conventional Commits |

### Deviations

None.

### Gaps Carried Forward

| ID | Gap |
|----|-----|
| GAP-007-1 | No distributed tracing. The Observability baseline is half met. The OpenTelemetry foundation carries it — add tracing instrumentation and an OTLP exporter; do not introduce a second framework |
| GAP-007-2 | No alerting. A stalled approval queue is visible only if somebody looks. Alerting should build on `novaleave_vacation_request_oldest_pending_age_seconds` |
| GAP-007-3 | Nothing deployed; §12.3 operational targets unaddressed. Prometheus retention and storage sizing unconfigured |
| GAP-007-4 | §12.1 latency targets are now measurable but not verified, and cannot be verified here — SQL Server runs emulated on arm64 |
| GAP-007-5 | Approval-duration histogram deferred. Recording it per event needs either a widened `IStateTransitionAuditService` or an extra read per transition; the oldest-pending-age gauge delivers most of the value without either |
| GAP-007-6 | Prometheus published without authentication. Acceptable locally, not in a deployed environment |

## Verification

Executed 2026-09-23 on Docker 29.8.0 / Compose v5.5.1, Apple M2 Pro:

- Both scrape targets **up**; business metrics queryable through Prometheus.
- Real actions drove real metrics: login, failed logins, and a request created through the UI moved the counters and the pending gauge.
- Grafana datasource and dashboard provisioned with no manual setup, surviving `down -v`.
- Every dashboard panel query executed against live data — p95 latency 42.5 ms.
- PII audit clean; 1,702 series against a 5,000 ceiling.
- Cold start after a full reset: **40 s** to healthy, against a 3-minute budget.

Full record in [tasks.md](../../.specify/specs/007-prometheus-observability/tasks.md).

## References

- [spec_007](../../.specify/specs/007-prometheus-observability/spec_007-prometheus-observability.md)
- [research.md](../../.specify/specs/007-prometheus-observability/research.md) — R-001 … R-009 with amendments
- [metrics-catalogue.md](../../.specify/specs/007-prometheus-observability/metrics-catalogue.md)
