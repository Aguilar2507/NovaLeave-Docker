# Tasks: Metrics and Observability with Prometheus

**Input**: `.specify/specs/007-prometheus-observability/plan.md`, `spec_007-prometheus-observability.md`, `research.md`
**Prerequisites**: spec ✓, research ✓, plan ✓, `005-docker-containerization` delivered ✓
**Tests**: Decorator behavior and failure isolation are unit-tested. Stack behavior is verified by executed procedure.
**Organization**: Grouped by phase, each with an explicit exit criterion.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this serves (US1–US4)

---

## Phase 1: Instrumentation Foundation

**Purpose**: Get infrastructure metrics flowing before any business instrumentation exists, so a later failure is unambiguously a business-metric failure.

- [x] T101 [US1] Add OpenTelemetry packages. **Resolved to 2 packages, not the 5 planned, and they live in `NovaLeave.Presentation.Web`, not Infrastructure.** Three planned packages were dropped because their metrics extensions no longer exist — ASP.NET Core and .NET runtime meters are native now, so they are subscribed by name instead. Placement moved because OpenTelemetry wiring is a composition-root concern and the extension methods did not resolve transitively from Infrastructure. Full record: `research.md` → *R-001 Outcome*
- [x] T102 [US1] Create `src/NovaLeave.Infrastructure/Observability/NovaLeaveMetrics.cs` — meter and instruments. Names follow Prometheus conventions (FR-005). **Counters are deliberately NOT suffixed `_total` in code**: the Prometheus exporter appends that suffix itself, so naming them `..._total` would export `..._total_total`
- [x] T103 [US1] Wire OpenTelemetry in `Program.cs` — all meters subscribed by name, plus `ConfigureResource` setting the service name (without it the service reported as `unknown_service:NovaLeave.Presentation.Web`, which Grafana would key dashboards off)
- [x] T104 [US4] Map the Prometheus scraping endpoint at `/metrics`, before authentication (FR-018, US4-3)
- [x] T105 [US1] Verify `/metrics` returns exposition-format text — checked from inside the container network
- [x] T106 [US3] Verify the solution builds clean and existing suites are unchanged against the spec_005 baseline

**Exit criteria**: `/metrics` serves real data; build clean; no test result changed. ✅ **Met.**

### Phase 1 Verification Record (2026-09-22)

| Check | Result |
|-------|--------|
| Build | ✅ 0 warnings, 0 errors (`TreatWarningsAsErrors=true`) |
| `Domain.Tests` | ✅ 63/63 |
| `Application.Tests` | ✅ 41/41 |
| `Presentation.Tests` | ⚠️ 26 failed of 49 — within the documented 24–26 baseline; unchanged by this work |
| `/metrics` serves data | ✅ 33 metric families, 97 active series |
| Service identity | ✅ `service_name="novaleave-web"`, `service_version="1.0.0.0"` |
| Route-template cardinality | ✅ `http_route="{controller=Home}/{action=Index}/{id?}"` — templates, not raw paths |
| Series count | ✅ 97, far under the 5,000 ceiling (NFR-003) |
| PII in scrape | ✅ zero matches for seeded emails, names or GUIDs |

**Meters confirmed producing data**: `Microsoft.AspNetCore.Hosting`, `Microsoft.AspNetCore.Server.Kestrel`, `Microsoft.AspNetCore.Routing`, `System.Runtime` (20 `dotnet_*` families), `Microsoft.EntityFrameworkCore`.

`Microsoft.AspNetCore.Diagnostics` and `Microsoft.AspNetCore.RateLimiting` are subscribed but emitted nothing yet — expected, since no unhandled exception or rate-limit rejection had occurred.

**Amendment made during this phase**: FR-008's gauge changed from days to **seconds**, because "days" contradicted FR-005's own requirement to use base units. Grafana formats seconds as days for display. All artifacts updated.

**Note for Phase 3**: a plain `docker run` for builds now needs the NuGet cache volume mounted, or restore fails with `NETSDK1064`:
```bash
docker run --rm -v "$PWD":/src -v novaleave_novaleave-nuget:/root/.nuget/packages \
  -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet build NovaLeave.slnx
```

---

## Phase 2: Business Metrics

**Purpose**: Measure NovaLeave, not just servers. Depends on Phase 1.

- [x] T201 [US2] Create `MetricsStateTransitionAuditService.cs` — decorator over `IStateTransitionAuditService`. Audits first, then records the counter inside a guard, so a metrics failure cannot fail the audit call (FR-011)
- [x] T202 [US2] Create `MetricsAuthenticationAuditService.cs` — decorator over `IAuthenticationAuditService`. `result` is success/failure only; `reason` and `email` never leave the class (FR-009)
- [x] T203 [US2] Create `VacationRequestMetricsCollector.cs` — **implemented as a `BackgroundService`, not as a query-per-scrape gauge.** See the R-003 amendment: `ObservableGauge` callbacks are synchronous, so the original design required sync-over-async, which Constitution §2.VIII prohibits. Refreshes an immutable snapshot every 15 s with a 5 s `TimeProvider` timeout; callbacks read the snapshot, so the scrape path performs no I/O. Yields no measurement — not zero — on failure, timeout or an empty queue (FR-008, FR-012). *(Renamed from `VacationRequestGaugeCollector` to reflect what it is.)*
- [x] T204 [US2] Register both decorators and the collector in `Program.cs` — the concrete services are registered by type, the interfaces resolve to the decorators, and the collector is an `AddHostedService` so the host instantiates it (an `ObservableGauge` only reports while its registering object is alive)
- [x] T205 [P] [US3] Create `tests/NovaLeave.Application.Tests/MetricsDecoratorTests.cs` — **8 tests**: argument-for-argument delegation on both decorators; metrics failure isolated on both; **a failing audit still propagates** (guarding against over-broad exception handling); and label assertions proving exactly two bounded labels on transitions and one on auth
- [x] T206 [US2] Verify end to end against the running stack
- [x] T207 [US3] Verify no handler, validator, domain entity or authorization rule was modified (SC-006)

**Exit criteria**: business metrics move in response to real actions; business code untouched. ✅ **Met.**

### Phase 2 Verification Record (2026-09-22)

Driven through the real HTTP interface against the running stack, not through test doubles.

| Action | Observed metric |
|--------|-----------------|
| Log in as `user@novaleave.local` | `novaleave_authentication_attempts_total{result="success"} 1` |
| Wrong password on an existing account | `result="failure"` |
| Login for an account that does not exist | `result="failure"` — **identical**, so spec_004's existence hiding survives exposure as a metric |
| Create a vacation request (`POST /EmployeeRequests/Create` → 302) | `novaleave_vacation_request_transitions_total{from_status="Pending",to_status="Pending"} 1` |
| Same action | `novaleave_vacation_requests_pending` rose 1 → 2 |
| Seeded pending request | `novaleave_vacation_request_oldest_pending_age_seconds` ≈ 14158 (~3.9 h) |

| Check | Result |
|-------|--------|
| Build | ✅ 0 warnings, 0 errors |
| `Domain.Tests` | ✅ 63/63 |
| `Application.Tests` | ✅ **49/49** (41 existing + 8 new) |
| `Presentation.Tests` | ⚠️ 25 failed of 49 — within the 24–26 baseline, unchanged |
| SC-006 — business code untouched | ✅ `git diff` touches only 2 `.csproj` files, `Program.cs` and the test `.csproj`. No Domain file, handler, validator or authorization rule |

**Finding — creation records as `Pending → Pending`.** `CreateRequestHandler.cs:93` deliberately passes `fromStatus: Pending` for a newly created request, with the comment *"convención: primera creación usa Pending como 'from'"*. The counter therefore reports creation as `from_status="Pending",to_status="Pending"` rather than something like `None → Pending`.

This is faithful to the audit data and was left alone: the decorator must not invent semantics the audit trail does not have, and changing it would mean editing a handler (violating FR-010). It is unambiguous in practice — `Pending → Pending` is the only self-transition — but it is counter-intuitive and **must be documented in the metrics catalogue (T405)** so nobody writes a PromQL query expecting a `None` state.

---

## Phase 3: Prometheus and Grafana

**Purpose**: Collect and display. Depends on Phase 2.

- [x] T301 [US1] Create `docker/prometheus/prometheus.yml` — scrapes `web:8080/metrics` at 15 s, matching the collector's refresh interval. Also scrapes Prometheus itself, which distinguishes "the app stopped reporting" from "Prometheus stopped working"
- [x] T302 [P] [US1] Create `docker/grafana/provisioning/datasources/prometheus.yml` — **with a fixed `uid`**; without one Grafana generates a random uid and every provisioned panel resolves to "datasource not found"
- [x] T303 [P] [US1] Create `docker/grafana/provisioning/dashboards/dashboards.yml` — provider with `allowUiUpdates: false`, so the repository stays the source of truth
- [x] T304 [US1] Create `novaleave-overview.json` — 11 panels across three rows: vacation-request process, application health, database and runtime
- [x] T305 [US1] Extend `compose.yaml` — `prom/prometheus:v3.14.0` and `grafana/grafana:13.2.2`, both registry-verified, named volumes, provisioning mounted read-only
- [x] T306 [P] [US1] Extend `.env.example` — ports and Grafana credentials with the development-only warning (FR-019)
- [x] T307 [US1] Verify the target is UP and the dashboard is present with no manual configuration

**Exit criteria**: `docker compose up` produces a scraping Prometheus and a populated dashboard. ✅ **Met.**

### Addendum — community .NET dashboards (2026-09-23)

Added after spec_007 closed, completing the "Grafana + dashboard + datasource" item. Dashboards now provisioned: **4**.

- [x] T308 Vendor grafana.com **19924** (*ASP.NET Core*) and **19925** (*ASP.NET Core Endpoint*), both published by **the .NET team**. Verified before adopting: metric names compared against a live scrape, publisher confirmed, and **every panel query executed against live Prometheus**
- [x] T309 Build `dotnet-runtime.json` — 12 panels over the 19 `System.Runtime` instruments (CPU, memory, GC by generation, allocation rate, thread pool, lock contention, exceptions, JIT). Hand-built because **no community dashboard targets OpenTelemetry `dotnet_*` naming**; the popular ones assume `prometheus-net`
- [x] T310 Add empty `provisioning/plugins/` and `provisioning/alerting/` directories — Grafana logged an error on every start for each missing directory, recurring noise that could hide a real provisioning failure

**Panel query verification (all executed live, template variables substituted):**

| Dashboard | Panels with data | Without |
|-----------|------------------|---------|
| *ASP.NET Core* (19924) | 14 | 2 |
| *ASP.NET Core Endpoint* (19925) | 11 | 1 |
| *.NET Runtime* (ours) | 18 | **0** |

The three without data are the **Unhandled Exceptions** panels, and they are **correct**: they filter `error_type!=""`, and ASP.NET Core attaches `error_type` only when a request throws an unhandled exception. Verified — 4 `http_server` series exist, 0 carry the label. Nothing has thrown; the queries are right.

**Risk that was checked and did not materialise**: most .NET dashboards on grafana.com target `prometheus-net` naming, which would have produced a page of "No data". 19924/19925 use OTel naming and matched exactly. Full reasoning in `research.md` → R-011.

**Vendoring rule**: the two community dashboards are unmodified except for binding the datasource and removing `__inputs`/`__requires`. The provider sets `allowUiUpdates: false`, so UI edits are discarded on restart — re-download and re-transform instead.

### Phase 3 Verification Record (2026-09-22)

| Check | Result |
|-------|--------|
| All four services up | ✅ `db` (healthy), `web`, `prometheus`, `grafana` |
| Prometheus targets | ✅ `novaleave-web` **up** at `http://web:8080/metrics`; `prometheus` **up** |
| Business metrics in Prometheus | ✅ pending `2`, oldest-pending `14803s`, transitions `1`, auth `success=1 failure=2` |
| Grafana health | ✅ 13.2.2, database ok |
| Datasource provisioned | ✅ `Prometheus`, uid `novaleave-prometheus`, default |
| Dashboard provisioned | ✅ *NovaLeave — Overview* in the *NovaLeave* folder, no manual setup |
| **Every panel query executed** | ✅ p95 latency `0.0425s`, request rate by route, status classes, EF query rate, concurrency failures, working set — all return data, none error |

**Correction made**: `research.md` R-007 had guessed `prom/prometheus:v3.7.3` and `grafana/grafana:12.3.1`. The registry shows current releases are **v3.14.0** and **13.2.2** — the Grafana guess was a full major version behind. Corrected before pinning.

### ⚠️ Security caveat found in Phase 3 — carried into Phase 4

Publishing Prometheus on port 9090 means **the metric data is reachable from the host without authentication**, even though `/metrics` itself is not. Prometheus has no auth and holds every series it scraped, so `GET localhost:9090/api/v1/query` returns the same information.

SC-003 is therefore satisfied literally but weakened in spirit. Accepted for a local development stack — it binds to localhost, the data describes seeded records, and the expression browser is most of Prometheus's value — but it **must not** be carried into a deployed environment, where Grafana should be the only exposed entry point.

Recorded as **GAP-007-6**; must appear in ADR-002 (T406) and the documentation (T408). Anyone wanting the stricter posture locally can delete the `ports:` block from the `prometheus` service.

---

## Phase 4: Verification and Documentation

**Purpose**: Prove it, then write down what is true. Depends on Phase 3.

- [x] T401 [US4] **Negative test** — **FAILED ON FIRST RUN, DESIGN CHANGED.** See below
- [x] T402 [US2] **PII audit** — full scrape grepped for seeded emails, names, passwords, reason text and GUIDs
- [x] T403 [US1] **Cardinality check** — 1,702 series against the 5,000 ceiling
- [x] T404 [US1] `down -v && up` — working stack, empty metric history, cold start **40 s** against a 3-minute budget
- [x] T405 [P] Create `metrics-catalogue.md` (FR-022)
- [x] T406 [P] Create `docs/adr/ADR-002-prometheus-observability.md` (FR-021, FR-023)
- [x] T407 [P] Update `.specify/specs/common/architecture.md`
- [x] T408 [P] Update `README.md` and `docs/Keep-in-mind.md`
- [x] T409 Final Constitution Check

**Exit criteria**: every success criterion met or explicitly recorded as outstanding. ✅ **Met.**

---

### 🔴 T401 found a real security defect — and it was our own design

**First run, against the design as specified:**

```
curl http://localhost:8080/metrics   →   HTTP 200, 66,170 bytes, 160 metric lines
```

`/metrics` was fully readable from the host. Research R-006 had reasoned that "nothing forwards `/metrics` specifically" — that reasoning was simply wrong. **Publishing a port publishes every path on it.** FR-018 was violated and SC-003 failed.

This is precisely the exposure Constitution §7.2 prohibits — route names, traffic volumes and error patterns, readable by anyone who could reach the app. It existed because the control was *reasoned about* rather than *tested*.

**The design was changed, not documented around.** `/metrics` now binds to a dedicated port 9464 via `RequireHost`, and compose publishes only 8080 — the separate-port option R-006 had dismissed as unnecessary turned out to be the only thing that actually works.

**Re-verified four ways:**

| Check | Before | After |
|-------|--------|-------|
| `/metrics` from host on app port | ❌ HTTP 200, 160 metric lines | ✅ **HTTP 404** |
| Port 9464 from host | n/a | ✅ **connection refused** |
| `web:9464/metrics` from Prometheus container | ✅ | ✅ **succeeds** |
| Application from host | ✅ | ✅ **HTTP 200**, unaffected |

**Two incidental defects fixed while making this work:**

1. Removing `--urls` from the Dockerfile made `dotnet watch run` fall back to `launchSettings.json`'s **https** profile, which has no certificate in the container — the app crashed on startup with `Unable to configure HTTPS endpoint`. Fixed with `--no-launch-profile`: launch profiles are a developer-machine artifact and should never influence a container run.
2. `prometheus.yml` was bind-mounted as a **single file**. Editing it with `sed -i` replaced the inode, leaving the container pointing at a deleted file and failing config reload with `no such file or directory`. Fixed by mounting the **directory** instead.

---

### Phase 4 Verification Record (2026-09-23)

| Criterion | Method | Result |
|-----------|--------|--------|
| SC-001 | Exercise app, inspect Grafana | ✅ request rate, latency, errors visible; dashboard provisioned unaided |
| SC-002 | Create a request, watch counters | ✅ transitions counter and pending gauge both moved |
| SC-003 | Negative + positive exposure test | ✅ **after the fix above** |
| SC-004 | Grep a full scrape for PII | ✅ zero hits for `novaleave.local`, `Juan`, `María`, `Pérez`, `García`, `Test123`, reason text. Only GUID present is OpenTelemetry's generated `service_instance_id` |
| SC-005 | Run the suites | ✅ Domain 63/63, Application 49/49, Presentation within its 24–26 baseline |
| SC-006 | `git diff --stat` | ✅ no Domain file, handler, validator or authorization rule |
| SC-007 | Count series | ✅ **1,702** of 5,000. Largest: `http_server_request_duration_seconds_bucket` (225). ~⅓ is Prometheus self-monitoring |
| SC-008 | `down -v && up` | ✅ empty history, both targets up, Grafana re-provisioned, **40 s** cold start |

### T409 Final Constitution Check

| Gate | Status |
|------|--------|
| §8 Observability | ✅ Metrics delivered; tracing outstanding (GAP-007-1) |
| architecture.md — metrics baseline | ✅ Half delivered, document updated to say which half |
| §3.2 / §12.2 — infrastructure needs an approved decision | ✅ ADR-002 |
| §7.2 — diagnostics not publicly exposed | ✅ For `/metrics`, **proven by negative test after a design change**. ⚠️ Prometheus itself published locally (GAP-007-6) |
| §7.2 — explicit version pinning | ✅ `v3.14.0`, `13.2.2`, verified against the registry after the initial guesses proved a major version stale |
| §7.2 — secrets outside the repository | ✅ Grafana credentials from git-ignored `.env` |
| §7.3 — no sensitive data in metrics | ✅ Audited scrape, zero hits; failure reasons deliberately excluded |
| §2.I — Clean Architecture | ✅ Decorators in Infrastructure; Domain untouched |
| §2.II — simplicity before abstraction | ✅ Decorators over existing contracts, no eventing layer |
| §2.VI — testable time | ✅ `TimeProvider` for refresh interval and timeout |
| §2.VIII — async I/O end-to-end | ✅ The reason gauges use a `BackgroundService` rather than querying on scrape |
| §14 — ADR and documentation | ✅ ADR-002, metrics catalogue, architecture, README, Keep-in-mind |
| §16.1 — Git workflow | ✅ Branch `prometheus-implementation` |
| §12.1 — performance targets | ⚠️ Measurable now, not verified — and not verifiable here (GAP-007-4) |
| §9.3 — CI gate | ❌ Still unreachable, blocked by GAP-005-5, unrelated to this feature |

### Outstanding at feature close

1. **GAP-007-1 … GAP-007-6** carried forward; all recorded in `docs/Keep-in-mind.md`.
2. **No tracing** — the Observability baseline remains half met.
3. **§12.1 latency targets measurable but unverified**, and not verifiable while SQL Server runs emulated.

---

## Dependencies

```text
Phase 1 (T101–T106) → Phase 2 (T201–T207) → Phase 3 (T301–T307) → Phase 4 (T401–T409)
```

- T102 before T103 (instruments must exist before registration).
- T201–T203 before T204 (classes before registration).
- T301 before T307 (scrape config before verifying the target).
- T405–T408 after Phase 3 — documentation describes what was actually built.

## Parallelizable

- T302, T303, T306 (different files)
- T405, T406, T407, T408

---

## Notes carried from spec_005

- **Build and test only inside the container**: the host SDK is 7.0.304 against a `net10.0` target.
  ```bash
  docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet build NovaLeave.slnx
  ```
- **`NovaLeave.Presentation.Tests` fails 24–26 of 49 tests at baseline**, unrelated to this work (GAP-005-5, spec 006). Compare against that baseline rather than expecting green.
- **Do not take §12.1 performance measurements from this stack** — SQL Server runs emulated on arm64.
