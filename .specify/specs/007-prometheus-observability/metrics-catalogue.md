# NovaLeave Metrics Catalogue

**Feature**: `007-prometheus-observability` (T405, FR-022)
**Last verified**: 2026-09-23 against the running stack
**Scrape endpoint**: `http://web:9464/metrics` — container-internal only
**Prometheus**: http://localhost:9090 · **Grafana**: http://localhost:3000

Every metric below was observed in a real scrape. Nothing here is aspirational.

---

## How to read this

Prometheus is a query engine, not a dashboard — it shows nothing until you ask it something. Use **Grafana** to look at things and **Prometheus** to ask questions.

Four dashboards are provisioned: *NovaLeave — Overview* and *.NET Runtime* (hand-built for this project), plus *ASP.NET Core* and *ASP.NET Core Endpoint* vendored from the .NET team (grafana.com 19924/19925). Sourcing and verification are recorded in `research.md` → R-011.

Counters only ever increase and reset to zero when the process restarts, so query them with `rate()` or `increase()`, never as raw totals.

---

## NovaLeave business metrics

These are the ones that describe the vacation process rather than the servers.

### `novaleave_vacation_request_transitions_total`

**Type**: counter · **Labels**: `from_status`, `to_status`

Every vacation request state transition. Emitted by a decorator over `IStateTransitionAuditService`, which all six handlers (Create, Approve, Reject, Cancel, Void, Edit) call — so every transition is captured without any handler knowing about metrics.

> **⚠️ Request creation appears as `Pending → Pending`.**
> `CreateRequestHandler.cs:93` passes `fromStatus: Pending` for a new request by convention — there is no `None` state. It is the only self-transition, so it is unambiguous, but a query written expecting `from_status="None"` will silently return nothing.

```promql
# Transitions per second, broken out
sum by (from_status, to_status) (rate(novaleave_vacation_request_transitions_total[5m]))

# Requests created in the last 24h (the self-transition)
increase(novaleave_vacation_request_transitions_total{from_status="Pending",to_status="Pending"}[24h])

# Approvals vs rejections in the last 24h
sum by (to_status) (increase(novaleave_vacation_request_transitions_total{from_status="Pending",to_status=~"Approved|Rejected"}[24h]))
```

### `novaleave_vacation_requests_pending`

**Type**: gauge · **Labels**: none

Requests currently awaiting approver action. Refreshed every 15s by `VacationRequestMetricsCollector`.

**A gap means "unknown", not "zero"** — the collector reports no value when it cannot reach the database, deliberately, so a fabricated zero never gets acted on.

```promql
novaleave_vacation_requests_pending

# Queue growing faster than it drains?
deriv(novaleave_vacation_requests_pending[1h]) > 0
```

### `novaleave_vacation_request_oldest_pending_age_seconds`

**Type**: gauge · **Labels**: none · **Unit**: seconds

Age of the oldest request still waiting. **The stall signal.**

> **This metric will grow without bound, and that is a real finding, not a bug.**
> Auto-expiry is not implemented (`docs/Keep-in-mind.md` item 2): `VacationRequest.Expire()` exists in the domain but nothing in `src/` ever calls it. A request an approver never acts on stays `Pending` forever. This gauge is currently the only thing that would make that visible.

**Absent when the queue is empty** — reporting 0 would be indistinguishable from "a request was created one second ago".

```promql
# In days, which is how humans think about it
novaleave_vacation_request_oldest_pending_age_seconds / 86400

# Anything waiting more than 5 days
novaleave_vacation_request_oldest_pending_age_seconds > 432000
```

### `novaleave_authentication_attempts_total`

**Type**: counter · **Labels**: `result` (`success` | `failure`)

> **`result` is intentionally the only label.** spec_004 requires that authentication failures never reveal whether an account exists, is inactive or is locked out. The audit trail records a reason; the metric must not, because `/metrics` has no authentication in front of it. A wrong password and a nonexistent account are indistinguishable here — verified by test.

```promql
sum by (result) (rate(novaleave_authentication_attempts_total[5m]))

# Failure ratio — a spike may indicate credential stuffing
sum(rate(novaleave_authentication_attempts_total{result="failure"}[5m]))
  / sum(rate(novaleave_authentication_attempts_total[5m]))
```

---

## Application metrics (built-in, ASP.NET Core)

Emitted natively by .NET; no instrumentation package involved.

| Metric | Type | Key labels |
|--------|------|-----------|
| `http_server_request_duration_seconds` | histogram | `http_route`, `http_request_method`, `http_response_status_code` |
| `http_server_active_requests` | gauge | `http_request_method` |
| `kestrel_active_connections` | gauge | — |
| `kestrel_connection_duration_seconds` | histogram | — |
| `kestrel_queued_connections` | gauge | — |
| `aspnetcore_routing_match_attempts_total` | counter | `aspnetcore_routing_match_status` |

> **`http_route` is a route TEMPLATE** (`{controller=Home}/{action=Index}/{id?}`), not a raw path. That is what keeps cardinality bounded. Rewriting it to raw paths would make cardinality unbounded — do not.

```promql
# Request rate by route
sum by (http_route) (rate(http_server_request_duration_seconds_count[5m]))

# p95 latency — Constitution §12.1 targets p95 < 300ms focused, < 500ms pages
histogram_quantile(0.95, sum by (le) (rate(http_server_request_duration_seconds_bucket[5m])))

# Server error rate
sum(rate(http_server_request_duration_seconds_count{http_response_status_code=~"5.."}[5m]))
```

> **Do not use this stack for §12.1 verification.** SQL Server runs under amd64 emulation on Apple Silicon (spec_005 R-002), so latency here is not representative.

`Microsoft.AspNetCore.Diagnostics` and `Microsoft.AspNetCore.RateLimiting` are also subscribed but emit nothing until an unhandled exception or a rate-limit rejection occurs. The rate-limiting meter is the first observability the limiter has ever had — its tests are disabled in the `Testing` environment (`docs/Keep-in-mind.md` item 14).

---

## Database metrics (EF Core)

| Metric | Type |
|--------|------|
| `microsoft_entityframeworkcore_queries_total` | counter |
| `microsoft_entityframeworkcore_savechanges_total` | counter |
| `microsoft_entityframeworkcore_active_dbcontexts` | gauge |
| `microsoft_entityframeworkcore_optimistic_concurrency_failures_total` | counter |
| `microsoft_entityframeworkcore_compiled_query_cache_hits_total` | counter |
| `microsoft_entityframeworkcore_compiled_query_cache_misses_total` | counter |
| `microsoft_entityframeworkcore_execution_strategy_operation_failures_total` | counter |

> **`optimistic_concurrency_failures_total` deserves attention.** The tests covering optimistic concurrency are **skipped** (`docs/Keep-in-mind.md` item 8), so this counter is currently the only live signal for a `RowVersion` invariant that has no passing test.

```promql
sum(rate(microsoft_entityframeworkcore_queries_total[5m]))
sum(increase(microsoft_entityframeworkcore_optimistic_concurrency_failures_total[1h]))
```

---

## Runtime metrics (.NET)

20 families under `dotnet_*`, native since .NET 9. Most useful:

| Metric | Meaning |
|--------|---------|
| `dotnet_process_memory_working_set_bytes` | Process memory |
| `dotnet_gc_collections_total` | GC collections by generation |
| `dotnet_gc_pause_time_seconds_total` | Total GC pause |
| `dotnet_gc_last_collection_heap_size_bytes` | Heap after last GC |
| `dotnet_thread_pool_queue_length_total` | Thread pool queue — sustained growth means starvation |
| `dotnet_exceptions_total` | Exceptions thrown |
| `dotnet_monitor_lock_contentions_total` | Lock contention |

---

## Scrape health

| Metric | Meaning |
|--------|---------|
| `up{job="novaleave-web"}` | `1` scrapeable, `0` down. **Distinguishes "app is down" from "app is idle"** |
| `scrape_duration_seconds` | How long the scrape took |
| `scrape_samples_scraped` | Series returned |

```promql
up{job="novaleave-web"}
```

---

## Prohibited labels

**Never add any of these to a metric.** Each is either personal data under Constitution §7.3, or unbounded (one new time series per distinct value, held in memory forever), and most are both.

| Prohibited | Why |
|-----------|-----|
| Employee id / identity id | Unbounded; identifies a person |
| Email | Unbounded; PII |
| Employee name | Unbounded; PII |
| Request id | Unbounded — one series per request, forever |
| **Request reason** | PII, classified Sensitive under §7.3 |
| Raw URL path | Unbounded (ids in paths) |
| Correlation id | Unbounded by definition |
| Authentication failure reason | Defeats spec_004 existence hiding on an unauthenticated endpoint |

Before adding a label, ask: *how many distinct values can this ever have?* If the answer is not a small fixed number, it does not belong in a label.

Current label budget:

| Metric | Label combinations |
|--------|-------------------|
| `novaleave_vacation_request_transitions_total` | ≤ 36 (6 statuses × 6); ~8 in practice |
| `novaleave_authentication_attempts_total` | 2 |
| Both gauges | 1 each |

---

## Measured footprint (2026-09-23)

| Measure | Value | Limit |
|---------|-------|-------|
| Metric families per scrape | 39 | — |
| Series per scrape | 207 | — |
| Series stored in Prometheus | 1,702 | 5,000 (NFR-003) |
| Largest contributor | `http_server_request_duration_seconds_bucket` (225) | — |

Roughly a third of stored series are Prometheus's own self-monitoring, not NovaLeave's.
