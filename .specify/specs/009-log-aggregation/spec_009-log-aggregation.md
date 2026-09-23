# Feature Specification: Log Aggregation with Loki

**Feature Branch**: `prometheus-implementation` *(continued; no new branch created)*
**Created**: 2026-09-23
**Status**: Implemented
**Constitution Reference**: NovaLeave — Constitution v4.0.0, §8 (Observability), §7.2, §7.3
**Depends on**: `005-docker-containerization`, `007-prometheus-observability`

---

## Overview

NovaLeave had Serilog with a **single console sink emitting plain text**, and no aggregation of any kind. Logs lived in `docker compose logs` and nowhere else. `CorrelationId` and `RequestId` were enriched into every line — but rendered into a string, so they could not be queried as fields.

This feature makes logs structured and queryable in Grafana, completing the correlation loop with spec_007's metrics: see a spike in a dashboard, then read the exact request's logs.

Two independent parts:

- **Step A — structured logs.** CLEF (Compact Log Event Format) JSON on stdout, configurable so non-container runs keep readable output.
- **Step B — aggregation.** Grafana Alloy tails container stdout via the Docker API and pushes to Loki; Grafana gains a provisioned Loki datasource and a logs panel.

**Out of scope**: log-based alerting, retention tuning for a deployed environment, shipping logs from anywhere other than this Compose project, and tracing (still GAP-007-1).

---

## Requirements

- **FR-001**: The application MUST emit one JSON object per log line (CLEF) when configured to do so.
- **FR-002**: Console format MUST be configurable, defaulting to human-readable text so running outside Docker is unchanged.
- **FR-003**: `CorrelationId` and `RequestId` MUST survive as queryable fields, not as substrings.
- **FR-004**: Logs MUST be shipped without the application knowing about the log backend.
- **FR-005**: Logs from **all** Compose services MUST be collected, not only the application.
- **FR-006**: Only this Compose project's containers may be collected.
- **FR-007**: Loki labels MUST be bounded. `CorrelationId`, `RequestId` and any request or user identifier are PROHIBITED as labels.
- **FR-008**: Loki and Alloy images MUST be pinned to explicit versions.
- **FR-009**: A Loki datasource MUST be provisioned automatically; Prometheus remains the default.
- **FR-010**: Log retention MUST match Prometheus's (7 days) so metrics and logs cover the same window.
- **FR-011**: The existing `docker compose up` / `down -v` workflow MUST be unchanged.
- **FR-012**: A logs panel MUST appear on the NovaLeave Overview dashboard.

## Success Criteria

- **SC-001**: The application emits CLEF JSON in the container. ✅
- **SC-002**: Loki holds logs from every service. ✅ `db, grafana, loki, prometheus, web` (and `alloy` once it has logged)
- **SC-003**: A request sent with a known `X-Correlation-ID` is findable in Loki by that field. ✅ **verified end to end**
- **SC-004**: Grafana's Loki datasource reports healthy and returns data through its proxy. ✅
- **SC-005**: Labels stay bounded — `service`, `container`, `job` only. ✅
- **SC-006**: `down -v && up` reproduces everything with no manual step. ✅ **55 s cold start**
- **SC-007**: Test suites unchanged. ✅ Domain 63/63, Application 49/49, Presentation at baseline

---

## How to use it

```logql
{service="web"}                                              # all application logs
{service="web"} | json | _l =~ `Warning|Error|Fatal`         # warnings and above
{service="web"} | json | CorrelationId=`<id>`                # one request, end to end
{service="db"}                                               # SQL Server
```

**Field names are not what you might expect.** LogQL's `| json` parser sanitises CLEF's `@`-prefixed keys:

| CLEF | After `| json` | Meaning |
|------|----------------|---------|
| `@t` | `_t` | timestamp |
| `@mt` | `_mt` | message template |
| `@l` | `_l` | level — **absent for Information**, which CLEF omits by convention |
| `@tr` | `_tr` | trace id |

Enriched properties (`CorrelationId`, `RequestId`, `RequestPath`, `StatusCode`, …) keep their names.

---

## Key decisions

### Agent, not a Serilog sink

`Serilog.Sinks.Grafana.Loki` would have been simpler — no agent, no Docker socket. It was rejected because it loses exactly the logs most worth having: **anything logged before the sink initialises, or during a crash**. This session hit such a case while implementing spec_007 — the app died with `Unable to configure HTTPS endpoint` before serving a request. An agent reading container stdout captures that; an in-process sink does not.

It also keeps the application ignorant of the log backend, matching spec_007's reasoning for using decorators instead of editing handlers.

### Labels are bounded, deliberately

Loki labels behave like Prometheus labels: each distinct combination is a stream held in memory. Labels are `service` (6 values), `container` (6) and `job` (1). `CorrelationId` is high-cardinality by definition and stays **inside the line**, queried with `| json` at read time. This mirrors `spec_007` research R-005.

### JSON on stdout, text outside the container

`Serilog:ConsoleFormat` defaults to `text`; compose sets it to `json`. Loki needs JSON to produce fields; a developer running outside Docker keeps the aligned output this project has always had. `docker compose logs -f web | jq` restores readability inside the container.

---

## Recorded Gaps

- **GAP-009-1** *(security)*: **Alloy mounts the Docker socket.** That is effectively root on the Docker daemon — it can start privileged containers and mount the host filesystem. The `:ro` flag restricts writes to the socket *file*, not to the API, so it is **not** a meaningful boundary. Accepted for a local development stack in exchange for capturing crash-time logs. A deployed environment must ship logs differently — a sidecar with a scoped service account, a platform log driver, or an in-process sink.
- **GAP-009-2** *(security)*: **Loki is published on 3100 with no authentication**, like Prometheus (GAP-007-6). Anyone who can reach localhost can read every log line, which now includes structured application logs. Acceptable bound to localhost; unacceptable deployed.
- **GAP-009-3**: No log-based alerting. Loki's ruler is not configured.
- **GAP-009-4**: Retention is a flat 7 days with no size cap. A deployed instance needs both sized deliberately.
- **GAP-009-5**: Request *reasons* are Sensitive/PII under Constitution §7.3. Nothing currently logs them, but there is **no test or lint preventing someone from adding one**, and logs are now centrally stored and queryable — which raises the consequence of that mistake.
