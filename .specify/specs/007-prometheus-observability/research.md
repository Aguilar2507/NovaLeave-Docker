# Phase 0 Research: Metrics and Observability with Prometheus

**Feature**: `007-prometheus-observability`
**Date**: 2026-09-22
**Spec**: [spec_007-prometheus-observability.md](./spec_007-prometheus-observability.md)

---

## Purpose

Unknowns resolved before implementation, with the options considered and the reasoning. Decisions here bind `plan.md` and `tasks.md`.

---

## R-001: Instrumentation Library

**Question**: What produces Prometheus-format metrics from an ASP.NET Core application?

**Versions checked against nuget.org on 2026-09-22:**

| Package | Latest |
|---------|--------|
| `OpenTelemetry.Extensions.Hosting` | 1.19.1 |
| `OpenTelemetry.Instrumentation.AspNetCore` | 1.19.0 |
| `OpenTelemetry.Instrumentation.Runtime` | 1.19.0 |
| `OpenTelemetry.Exporter.Prometheus.AspNetCore` | **1.19.1-beta.1** |
| `prometheus-net.AspNetCore` | 8.2.1 (stable) |

**Options considered**:

| Option | Assessment |
|--------|------------|
| OpenTelemetry + Prometheus exporter (pull) | Idiomatic Prometheus. Reads .NET's built-in `System.Diagnostics.Metrics` meters natively. Carries tracing later. **The exporter is beta** |
| OpenTelemetry + OTLP push to Prometheus 3.x's OTLP receiver | Every package stable. But inverts the pull model and loses the `up` metric — the signal that tells you a target died rather than went idle |
| `prometheus-net` | Stable and mature, but a separate ecosystem from OpenTelemetry, no tracing path, and extra work to surface the built-in .NET meters |

**Decision**: OpenTelemetry with the Prometheus exporter, pull model. **PO accepted the beta caveat explicitly.**

**Rationale**: .NET 8+ emits rich metrics through `System.Diagnostics.Metrics` — `Microsoft.AspNetCore.Hosting`, `Microsoft.AspNetCore.Server.Kestrel`, `Microsoft.EntityFrameworkCore`, `System.Net.Http`. OpenTelemetry consumes these directly, so substantial value arrives before any custom metric is written. `architecture.md` requires metrics *and tracing*; choosing a metrics-only library would mean replacing it later.

**On the beta label**: `OpenTelemetry.Exporter.Prometheus.AspNetCore` has been beta for years, not because it is unstable but because the OpenTelemetry specification's Prometheus compatibility section has not been declared stable. The package is widely used in production. It is nonetheless a genuine supply-chain and API-stability consideration, and it is recorded in ADR-002 rather than glossed over.

---

### R-001 Outcome (Phase 1, verified 2026-09-22)

The plan called for five packages. **Two were needed.** Three were dropped after the compiler rejected them, and investigation showed why:

| Planned package | Outcome |
|-----------------|---------|
| `OpenTelemetry.Extensions.Hosting` 1.19.1 | ✅ Referenced |
| `OpenTelemetry.Exporter.Prometheus.AspNetCore` 1.19.1-beta.1 | ✅ Referenced — **the only pre-release dependency** |
| `OpenTelemetry.Instrumentation.AspNetCore` | ❌ Dropped — `AddAspNetCoreInstrumentation()` **no longer exists on the metrics builder**. ASP.NET Core 8+ emits these meters natively; the package now covers tracing only |
| `OpenTelemetry.Instrumentation.Runtime` | ❌ Dropped — `AddRuntimeInstrumentation()` likewise gone; `System.Runtime` is a native meter since .NET 9 |
| `OpenTelemetry.Instrumentation.EntityFrameworkCore` | ❌ Never added — avoided deliberately (also beta); EF Core's native meter is subscribed by name |

All meters are therefore subscribed **by name**, and the empirical check in T105 confirmed every one produces data:

| Meter subscribed | Series produced |
|------------------|-----------------|
| `Microsoft.AspNetCore.Hosting` | `http_server_request_duration_seconds`, `http_server_active_requests` |
| `Microsoft.AspNetCore.Server.Kestrel` | `kestrel_active_connections`, `kestrel_connection_duration_seconds`, `kestrel_queued_connections` |
| `Microsoft.AspNetCore.Routing` | `aspnetcore_routing_match_attempts_total` |
| `System.Runtime` | 20 `dotnet_*` families (GC, JIT, thread pool, memory, exceptions) |
| `Microsoft.EntityFrameworkCore` | `microsoft_entityframeworkcore_queries_total`, `savechanges_total`, `active_dbcontexts`, `optimistic_concurrency_failures_total`, compiled-query cache hits/misses |

**Net effect**: the beta surface is smaller than planned. One pre-release package instead of three, and it is the exporter — replaceable without touching instrumentation, because nothing depends on its API beyond a single `AddPrometheusExporter()` call.

**Unexpected benefit**: `microsoft_entityframeworkcore_optimistic_concurrency_failures_total` is now exported. Optimistic concurrency is one of the invariants whose tests are skipped (`docs/Keep-in-mind.md` item 8), so this metric provides the first live signal for behavior that currently has no passing test.

---

## R-002: Where Business Metrics Are Emitted

**Question**: How are vacation-request metrics captured without editing handlers?

**Finding**: All six handlers — Create, Approve, Reject, Cancel, Void, Edit — call `IStateTransitionAuditService.RecordTransitionAsync`, which already receives `fromStatus` and `toStatus`. `IAuthenticationAuditService` occupies the same position for login outcomes.

**Options considered**:

| Option | Assessment |
|--------|------------|
| Decorator over the existing audit contracts | One class per contract; zero handler edits; every present and future transition is captured automatically |
| Edit each handler to emit a metric | Six edits now, and a silent gap every time someone adds a seventh transition |
| Domain events | The correct long-term design, but the project has no event infrastructure; introducing one for metrics is precisely the speculative abstraction §2.II prohibits |
| Poll the database for state counts | Produces gauges, but cannot produce transition counters — a request created and approved between two scrapes would leave no trace |

**Decision**: Decorators registered in DI, wrapping `IStateTransitionAuditService` and `IAuthenticationAuditService`.

**Rationale**: The audit contract is already the chokepoint every transition passes through — auditing and metrics answer the same question at different resolutions. A decorator inherits that completeness for free and satisfies FR-010 exactly. Constitution §2.II favors this over inventing an eventing layer.

**Consequence**: A transition that bypasses the audit service would also bypass metrics. That is acceptable and arguably desirable: such a transition would already be an audit defect, and the metric gap makes it more visible, not less.

---

## R-003: Gauges That Need Database State

**Question**: How are "pending request count" and "age of oldest pending request" produced?

**Context**: Counters come from events. Gauges describe current state, which lives in the database. An `ObservableGauge` callback runs on every scrape — every 15 seconds by default.

**Options considered**:

| Option | Assessment |
|--------|------------|
| `ObservableGauge` querying the database per scrape | Always current. Two indexed aggregate queries per scrape is negligible load, but a slow database could stall the scrape |
| Background service refreshing a cached value | Decouples scrape from database entirely, at the cost of a hosted service and staleness |
| Derive from counters | Impossible: counters cannot recover state that existed before the process started |

**Decision**: `ObservableGauge` with a scoped `DbContext` resolved per callback, executing with a **bounded timeout**, degrading to *no measurement* on failure or timeout.

**Rationale**: Two `COUNT`/`MIN` queries every 15 seconds is trivial next to normal traffic, and freshness matters for a queue-depth signal. The timeout is the load-bearing part: FR-012 requires that a slow database degrade the scrape rather than block it. Emitting no measurement is correct — Prometheus records a gap, which is honest, instead of a fabricated zero.

**Implementation note**: the meter is a singleton; the callback must create a DI scope to obtain `ApplicationDbContext`, which is scoped. Resolving a scoped service from the root provider would throw, and doing it inside a metrics callback would surface as a mysterious scrape failure.

### R-003 AMENDED during Phase 2 (2026-09-22)

**The decision above is not implementable, and was reversed.**

`ObservableGauge` callbacks are **synchronous**. Querying the database inside one requires either sync-over-async or EF Core's blocking APIs, and Constitution §2.VIII prohibits both: *"All I/O uses `async`/`await` end-to-end. `.Result`, `.Wait()`, sync-over-async prohibited."* The rejected "background service refreshing a cached value" option is the only one that satisfies §2.VIII.

**Amended decision**: `VacationRequestMetricsCollector`, a `BackgroundService` that refreshes an immutable snapshot every 15 seconds using async EF Core queries with a 5-second `TimeProvider`-driven timeout. The gauge callbacks read that snapshot — **no I/O on the scrape path at all**.

**Why this is better, not merely compliant**:

- A slow or unavailable database now *cannot* stall a scrape. The original design's timeout only bounded the damage; this removes the coupling.
- `BackgroundService` is started by the host automatically, which also solves eager instantiation — an `ObservableGauge` only reports while the object that registered it is alive, so a plain singleton would have needed forcing into existence.
- The cost is bounded staleness of at most one refresh interval, which is immaterial for a queue-depth signal.

**Failure behavior unchanged**: on failure or timeout the snapshot is cleared and both gauges report *no measurement*, so Prometheus records a visible gap rather than a fabricated zero (FR-012). Clearing rather than retaining the last value is deliberate: stale queue depth presented as current is worse than a gap, because somebody would act on it.

**Lesson recorded**: the original decision was made without checking the signature of the callback it depended on. A synchronous extension point in an async-only codebase is a constraint worth checking during Phase 0, not Phase 2.

---

## R-004: Approval Duration

**Question**: Should time-from-creation-to-resolution be a histogram?

**Finding**: The decorator receives `requestId`, `fromStatus` and `toStatus` — **not** `CreatedAt`. Recording a duration per event would require either widening `IStateTransitionAuditService` (an Application-layer contract change rippling through six handlers, contradicting FR-010) or an extra database read on every transition.

**Options considered**:

| Option | Assessment |
|--------|------------|
| Defer the histogram; export "age of oldest pending request" as a gauge | No contract change, no extra read, and arguably the better operational signal: it answers "is anything stuck right now?" |
| Widen the contract | Contradicts FR-010 and touches business code for an observability feature |
| Extra read per transition | Adds a database round-trip to every state change to serve a metric |

**Decision**: Defer the histogram. Export `novaleave_vacation_request_oldest_pending_age_seconds` instead.

**Rationale**: A histogram of completed approvals is a retrospective statistic. The oldest-pending age is a live one — and it is precisely the signal that would reveal the missing auto-expiry job (`docs/Keep-in-mind.md` item 2), where `Pending` requests currently never expire. The more useful metric is also the cheaper one.

**Consequence**: Recorded as **GAP-007-5**.

---

## R-005: Cardinality and PII

**Question**: Which labels are safe?

**Context**: Every distinct label-value combination is a separate time series held in memory. An employee id label would create one series per employee, forever. Constitution §7.3 additionally classifies request reasons and personal identifiers as Sensitive/PII and forbids them in metrics.

**Decision**:

| Metric | Labels | Bound |
|--------|--------|-------|
| `novaleave_vacation_request_transitions_total` | `from_status`, `to_status` | 6 statuses × 6 — at most 36, realistically ~8 |
| `novaleave_authentication_attempts_total` | `result` | 2 |
| `novaleave_vacation_requests_pending` | none | 1 |
| `novaleave_vacation_request_oldest_pending_age_seconds` | none | 1 |

**Explicitly prohibited as labels**: employee id, identity id, email, name, request id, request reason, raw URL path, correlation id.

**Rationale**: Both constraints point the same way. Every label above comes from a closed enum. The built-in ASP.NET Core metrics use *route templates*, not raw paths, which is why they are bounded — this must not be "fixed" into raw paths by anyone trying to get more detail.

---

## R-006: Metrics Endpoint Exposure

**Question**: How is `/metrics` protected?

**Context**: Constitution §7.2: *"Diagnostics and detailed errors MUST NOT be publicly exposed in production."* Metrics disclose route names, traffic volume, error patterns and infrastructure shape.

**Options considered**:

| Option | Assessment |
|--------|------------|
| Same port, not published to the host | Prometheus reaches it over the Docker network; nothing outside the network can. Zero extra configuration |
| Separate Kestrel port | Cleaner firewall separation, more configuration, no additional protection inside a Compose network |
| Authentication on the endpoint | Prometheus supports bearer tokens, but this is a development stack and the token would live in `.env` |

**Decision**: Served on the application port, **not published to the host**. The `web` service publishes only 8080 for the app; Prometheus reaches `web:8080/metrics` internally.

**Rationale**: In a Compose network, not publishing the port *is* the boundary. Note that the application port 8080 is published and `/metrics` is on the same port — so this control works because nothing forwards `/metrics` specifically, and the endpoint is not exposed on any additional host binding. **For any deployed environment this is insufficient**: production requires a separate port bound to an internal interface, a network policy, or authentication. Stated in ADR-002 and the documentation (FR-023).

**Verification**: SC-003 requires proving both halves — unreachable from the host, reachable from Prometheus.

### R-006 CAVEAT discovered during Phase 3 (2026-09-22)

**Keeping `/metrics` off the host does not keep the metric *data* off the host.**

Phase 3 publishes Prometheus itself on `${PROMETHEUS_PORT:-9090}` so developers can use its expression browser. Prometheus ships with **no authentication**, and it holds every series it scraped. Anyone who can reach `localhost:9090` can therefore read exactly the data `/metrics` exposes — route names, traffic volumes, error patterns — via `GET /api/v1/query`.

So SC-003, as written, is satisfied in the letter and weakened in the spirit. The honest statement is:

- The application's `/metrics` endpoint is not reachable from the host. ✅
- The metric data is reachable from the host, unauthenticated, through Prometheus. ⚠️

**Why this is accepted here**: the stack binds to `localhost` on a single developer machine, the data describes seeded development records, and the expression browser is most of Prometheus's day-to-day value. Grafana, by contrast, does require a login.

**Why it must not be carried into a deployed environment**: there, publishing Prometheus without authentication would expose the same disclosure surface Constitution §7.2 prohibits. A deployed stack needs Prometheus behind authentication or on an internal-only network, with Grafana as the only exposed entry point.

Recorded as **GAP-007-6** and stated in ADR-002. Anyone who wants the stricter posture locally can simply remove the `ports:` block from the `prometheus` service and reach it through Grafana.

---

## R-007: Container Images

**Question**: Which images and versions?

**Decision**:

| Component | Image | Notes |
|-----------|-------|-------|
| Prometheus | `prom/prometheus:v3.14.0` | Multi-arch — runs natively on arm64, unlike SQL Server |
| Grafana | `grafana/grafana:13.2.2` | Multi-arch |

Both pinned to explicit versions per Constitution §7.2.

**Corrected during Phase 3**: this table originally guessed `prom/prometheus:v3.7.3` and `grafana/grafana:12.3.1`. Querying the registry showed the current releases are **v3.14.0** and **13.2.2** — the Grafana guess was an entire major version behind and would have pinned the stack to an outdated image. Verifying tags against the registry rather than assuming them is the same discipline that caught the floating `2022-latest` tag in spec_005.

**Storage**: named volumes `novaleave-prometheus-data` and `novaleave-grafana-data`, removed by `down -v` along with the rest.

---

## R-008: Grafana Provisioning

**Question**: How does the dashboard exist without manual setup?

**Decision**: Grafana's file-based provisioning. A datasource YAML pointing at `http://prometheus:9090` and a dashboard provider YAML loading dashboard JSON, all mounted read-only from `docker/grafana/` in the repository.

**Rationale**: A dashboard clicked together in the UI lives in a container volume, disappears on `down -v`, and cannot be reviewed in a pull request. Provisioned files are version-controlled, reviewable, and satisfy US1 scenario 2 — the dashboard is simply *there* on first start.

**Mount read-only**: provisioning files are inputs. A writable mount invites drift between what the repository says and what Grafana is actually running.

---

## R-009: Histogram Buckets

**Question**: Do the default buckets fit?

**Finding**: OpenTelemetry's default explicit bucket boundaries are tuned for sub-second latency — appropriate for HTTP request duration, which is what the built-in ASP.NET Core histogram measures. The Prometheus exporter emits these as native histogram buckets.

**Decision**: Keep the defaults for HTTP duration. No custom histogram is introduced in this feature (R-004 deferred the only candidate).

**Rationale**: Changing bucket boundaries without a measured reason produces worse data, not better. This is recorded so that anyone later adding a duration measured in hours or days remembers that default buckets would place every observation in the overflow bucket and render the metric useless.

---

## Resolved Unknowns Summary

| ID | Unknown | Resolution |
|----|---------|------------|
| R-001 | Library | OpenTelemetry + Prometheus exporter (pull); beta accepted and recorded |
| R-002 | Business metric hook | Decorators over `IStateTransitionAuditService` and `IAuthenticationAuditService` |
| R-003 | State gauges | `ObservableGauge` + scoped `DbContext`, bounded timeout, degrade to no value |
| R-004 | Approval duration | Histogram deferred (GAP-007-5); oldest-pending-age gauge instead |
| R-005 | Cardinality / PII | Closed-enum labels only; identifiers and reasons prohibited |
| R-006 | Endpoint exposure | Container-internal; production requirements documented |
| R-007 | Images | Prometheus and Grafana pinned; tags verified at implementation |
| R-008 | Dashboards | File-based provisioning, mounted read-only |
| R-009 | Buckets | Defaults kept; rationale recorded for future duration metrics |

---

## Forward Constraints

For whoever adds tracing or deploys this:

1. Tracing reuses this OpenTelemetry setup — add `OpenTelemetry.Instrumentation.*` tracing and an OTLP exporter; do not introduce a second framework.
2. In production, `/metrics` needs a separate internal-only binding or authentication — not-publishing-a-port is a Compose-local control.
3. Prometheus retention and storage sizing are unconfigured; a deployed instance needs both.
4. Alerting (GAP-007-2) should build on the oldest-pending-age gauge — it is the metric that detects a stalled process.
5. If SQL Server ever runs natively rather than emulated, §12.1 latency targets become verifiable against these metrics (GAP-007-4).

---

## R-011: Community Dashboard Sourcing *(added 2026-09-23, post-Phase 4)*

**Question**: Should NovaLeave vendor community Grafana dashboards for .NET, or hand-build everything?

**Context**: `novaleave-overview.json` was written for this project and is business-focused. It covers the .NET runtime with a single memory/GC panel, leaving 19 emitted `dotnet_*` instruments unvisualised.

**The risk that had to be checked first**: most .NET dashboards on grafana.com target **`prometheus-net`** naming (`dotnet_collection_count_total`, `http_requests_received_total`), not the **OpenTelemetry** naming this project emits (`dotnet_gc_collections_total`, `http_server_request_duration_seconds`). Importing a mismatched dashboard yields a page where every panel reads "No data" — and worse, looks like a broken stack rather than a wrong dashboard.

**What was verified before adopting anything**:

1. Pulled dashboard JSON from `grafana.com/api/dashboards/{id}/revisions/latest/download`.
2. Extracted every `expr` and compared metric names against a live scrape.
3. Confirmed the publisher: grafana.com IDs **19924** and **19925** are published by **the .NET team** (`orgSlug: dotnetteam`), 25k and 20k downloads, last revised 2025-04-07.
4. Executed **every panel query** against live Prometheus with template variables substituted.

**Decision**: vendor 19924 and 19925 unmodified; hand-build a third dashboard for runtime metrics.

| Dashboard | Source | Why |
|-----------|--------|-----|
| *NovaLeave — Overview* | Hand-built | Business metrics; no community equivalent can exist |
| *ASP.NET Core* (19924) | .NET team | HTTP depth: connections, protocol, TLS, top endpoints, exception endpoints |
| *ASP.NET Core Endpoint* (19925) | .NET team | Per-route drill-down with `route`/`method` variables |
| *.NET Runtime* | Hand-built | **No community dashboard targets OTel `dotnet_*` naming.** Covers the 19 `System.Runtime` instruments |

**Vendoring modifications** (deliberately minimal, so re-vendoring a newer revision stays trivial):

- `${DS_PROMETHEUS}` → `novaleave-prometheus`. File-provisioned dashboards cannot prompt for a datasource input.
- `__inputs` / `__requires` removed — they exist for the UI import flow only.
- `editable: false`, `id: null`.

Nothing else was touched. The provider sets `allowUiUpdates: false`, so edits made in the UI are discarded on restart; the correct workflow is to re-download and re-transform.

**Rejected**: rewriting the community panels to taste. Divergence from upstream would make future re-vendoring a manual merge, in exchange for cosmetics.

**Note on the two "Unhandled Exceptions" panels**: they report no data, and that is correct. They filter `error_type!=""` on `http_server_request_duration_seconds_count`, and ASP.NET Core only attaches `error_type` when a request throws an unhandled exception. Verified: 4 `http_server` series exist, 0 carry the label. The query is right; there is simply nothing to show yet.
