# NovaLeave

Vacation request management for NovaComp. Employees submit leave requests; assigned approvers approve or reject them, with balances, overlap rules and an audit trail enforced by the domain.

ASP.NET Core MVC on .NET 10, Clean Architecture, EF Core + SQL Server, ASP.NET Core Identity.

---

## Quick start

```bash
cp .env.example .env
docker compose up -d --build
```

Then open **http://localhost:8080** and sign in with `user@novaleave.local` / `Test123!@#`.

No .NET SDK needed — the container builds and runs everything.

**On Apple Silicon**, enable *Use Rosetta for x86/amd64 emulation* in Docker Desktop → Settings → General. SQL Server publishes no arm64 image.

📖 **[Full Docker guide](.specify/specs/005-docker-containerization/quickstart.md)** — commands, database access, log reference and troubleshooting.

---

## Everyday commands

```bash
docker compose logs -f web    # follow application logs
docker compose ps             # container status
docker compose down           # stop, keeping the database
docker compose down -v        # stop and reset the database
```

Source is mounted into the container with `dotnet watch` running, so edits apply without rebuilding: Razor views on the next request, C# after a short in-container restart.

## Building and testing

The container is currently the **only** way to build this project — the solution targets `net10.0`:

```bash
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet build NovaLeave.slnx

docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet test tests/NovaLeave.Domain.Tests/NovaLeave.Domain.Tests.csproj
```

> `NovaLeave.Presentation.Tests` currently fails 24–26 of its 49 tests for reasons unrelated to Docker — the fixtures share one in-memory database and reference roles the application does not define. Diagnosed in [spec 006](.specify/specs/006-integration-test-isolation/spec_006-integration-test-isolation.md). `Domain.Tests` (63) and `Application.Tests` (41) pass.

---

## Observability

The stack ships with metrics collection and dashboards:

| | |
|---|---|
| **Grafana** | http://localhost:3000 — dashboards and logs (credentials in `.env`) |
| **Prometheus** | http://localhost:9090 — metrics query engine and scrape status |
| **Loki** | http://localhost:3100 — log store (API only; read logs through Grafana) |

Use **Grafana to look at things** and **Prometheus to ask questions**. Prometheus opens on an empty query box by design — it is a database, not a dashboard, and shows nothing until you type a query. Its `/targets` page is where you check that collection is healthy.

Four dashboards are provisioned automatically into the **NovaLeave** folder:

| Dashboard | Source | Covers |
|---|---|---|
| **NovaLeave — Overview** | ours | Vacation-request transitions, pending queue depth, oldest waiting request, request rate, latency, errors, EF Core, memory |
| **ASP.NET Core** | .NET team ([19924](https://grafana.com/grafana/dashboards/19924)) | HTTP depth: connections, protocol, TLS, top endpoints |
| **ASP.NET Core Endpoint** | .NET team ([19925](https://grafana.com/grafana/dashboards/19925)) | Per-route drill-down by method and route |
| **.NET Runtime** | ours | CPU, memory, GC by generation, allocation rate, thread pool, lock contention, exceptions, JIT |

Dashboards are provisioned from files, so **edits made in the Grafana UI are discarded on restart** — change the JSON in `docker/grafana/provisioning/dashboards/` instead.

📖 **[Metrics catalogue](.specify/specs/007-prometheus-observability/metrics-catalogue.md)** — every metric, its labels, example queries, and the labels that must never be added.

### Logs

All container logs are shipped to Loki by Grafana Alloy and are queryable in Grafana under **Explore → Loki**:

```logql
{service="web"}                                        # application logs
{service="web"} | json | _l =~ `Warning|Error|Fatal`   # warnings and above
{service="web"} | json | CorrelationId=`<id>`          # one request, end to end
```

That last query is the point: every request carries an `X-Correlation-ID`, so you can go from a latency spike on a dashboard to the exact request's log lines.

The application logs **CLEF JSON** inside the container so fields stay queryable. `docker compose logs -f web` still works — pipe it through `jq` for readability. Outside Docker the output stays plain text.

> Note: `| json` renames CLEF's `@`-prefixed keys — `@l` becomes `_l`, `@t` becomes `_t`, `@mt` becomes `_mt`. Enriched properties like `CorrelationId` keep their names. Information-level lines have no `_l` at all.

The application's own `/metrics` endpoint is served on an unpublished port and is deliberately **not** reachable from your machine; only Prometheus can scrape it. See [ADR-002](docs/adr/ADR-002-prometheus-observability.md).

---

## Project structure

```text
src/
  NovaLeave.Domain/            Entities, value objects, invariants — no outward dependencies
  NovaLeave.Application/       Use cases, contracts, input validation
  NovaLeave.Infrastructure/    EF Core, SQL Server, Identity, repositories
  NovaLeave.Presentation.Web/  Controllers, Razor views, composition root

tests/                         Domain, Application, Presentation (integration), E2E
.specify/                      Spec-kit specifications, plans and tasks
docs/adr/                      Architecture decision records
```

Dependencies point inward: Presentation → Application → Domain.

## Development accounts

Seeded automatically in `Development`. Password for all: `Test123!@#`.

| Email | Roles |
|-------|-------|
| `user@novaleave.local` | User |
| `approver@novaleave.local` | User + Approver |
| `employee@novaleave.local` | User |
| `manager@novaleave.local` | User + Approver |
| `inactive@novaleave.local` | User (Inactive — for rejected sign-in tests) |

Development-only credentials. They exist nowhere but a disposable local container.

### Seeded vacation requests

**13 requests across all six statuses** are seeded alongside the accounts, so the approver queue, request history and the Grafana business panels all have content on a fresh database.

| Owner | Initial balance | Requests | Final balance |
|---|---|---|---|
| Juan Pérez (`user@`) | 15 | 2 pending, 1 approved, 1 rejected, 1 cancelled | 3 |
| Pedro López (`employee@`) | 12 | 1 pending, 1 approved, 1 expired, 1 voided | 5 |
| Carlos Rodríguez (`manager@`) | 20 | 1 pending, 1 approved, 1 rejected, 1 cancelled | 12 |
| María García (`approver@`) | 10 | — (she approves) | 10 |
| Ana Martínez (`inactive@`) | 10 | — (inactive accounts cannot reserve days) | 10 |

Balances reconcile: `balance + days held by pending/approved requests = initial balance`.

Seeding is idempotent — restarting will not duplicate data. `docker compose down -v` resets and re-seeds. Details in [spec 008](.specify/specs/008-development-seed-data/spec_008-development-seed-data.md).

---

## Documentation

| Document | Purpose |
|----------|---------|
| **[Keep in mind](docs/Keep-in-mind.md)** | **Open work, known issues and decisions not to re-litigate — read this before picking up a task** |
| [Constitution](.specify/memory/constitution.md) | Binding architecture, security and process rules |
| [Architecture](.specify/specs/common/architecture.md) | Shared technical context and stack |
| [Docker quickstart](.specify/specs/005-docker-containerization/quickstart.md) | Running the stack locally |
| [Metrics catalogue](.specify/specs/007-prometheus-observability/metrics-catalogue.md) | Every exported metric, with example queries |
| [ADR index](docs/adr/) | Recorded architectural decisions |

Specifications live under `.specify/specs/`, numbered by feature.
