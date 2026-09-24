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

For editing with a working .NET 10 toolchain — IntelliSense, debugging, Test Explorer — use the [dev container](#dev-container-devpod) below.

> `NovaLeave.Presentation.Tests` currently fails 24–26 of its 49 tests for reasons unrelated to Docker — the fixtures share one in-memory database and reference roles the application does not define. Diagnosed in [spec 006](.specify/specs/006-integration-test-isolation/spec_006-integration-test-isolation.md). `Domain.Tests` (63) and `Application.Tests` (49) pass.

---

## Dev container (DevPod)

Your editor can run **inside** a container with the .NET 10 SDK, next to the stack: IntelliSense, debugging and tests work even though your machine's SDK is older. Defined in `.devcontainer/` using the open Dev Container standard; launched with [DevPod](https://devpod.sh).

```bash
# once — DevPod community fork (upstream is unmaintained, see ADR-004)
gh release download v0.26.1 --repo skevetter/devpod --pattern devpod-darwin-arm64
shasum -a 256 devpod-darwin-arm64        # 5a6b65646f62819bdd6822a2abfee792d9c7843f4cb6bc3633377c67c123a72d
install -m 755 devpod-darwin-arm64 /opt/homebrew/bin/devpod && rm devpod-darwin-arm64
devpod provider add docker
echo 'export DOCKER_HOST=unix://$HOME/.docker/run/docker.sock' >> ~/.zshrc   # macOS, see below

# daily
devpod up . --ide vscode      # starts the stack if needed, then the workspace, then VS Code
devpod stop .                 # stops the workspace; the stack keeps running
```

Inside the workspace terminal:

```bash
dotnet build NovaLeave.slnx
dotnet test tests/NovaLeave.Application.Tests
dotnet run --project src/NovaLeave.Presentation.Web --no-launch-profile   # http://localhost:5080
```

`web` keeps serving http://localhost:8080 with `dotnet watch` as always; the workspace is a separate container on the same network, reaching `db` by name.

> **macOS / Docker Desktop:** DevPod looks for `/var/run/docker.sock`, which Docker Desktop does not create by default. Export `DOCKER_HOST` as above, or enable *Settings → Advanced → Allow the default Docker socket to be used*. Without either, `devpod up` fails with *"failed to connect to the docker API"*.

No DevPod? The same files work with VS Code (*Dev Containers: Reopen in Container*) or `npx @devcontainers/cli up --workspace-folder .`. Details and trade-offs in [ADR-004](docs/adr/ADR-004-devpod-dev-containers.md).

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

## AI agents

Two optional [Docker Agent](https://docs.docker.com/ai/docker-agent/) teams are defined in `docker/agents/`. Each is a coordinator that hands work to specialist sub-agents.

| Team | Ask it to… | Runs in | Can change |
|---|---|---|---|
| **dev** — planner, reviewer, tester | write a spec or ADR, review your diff, build, run or write tests | a Docker Sandbox (microVM) | specs/docs and `tests/` only — never `src/` |
| **ops** — devenv, observability | explain what is running or failing, query metrics and logs | a container on the stack's network | nothing — read-only |

Both run on Google's Gemini cloud model (needs `GOOGLE_API_KEY`). The ops team can also run on a local model through Docker Model Runner (free, nothing leaves your machine, slower):

```bash
# Ops team — against the running stack
docker compose up -d
docker compose run --rm ops-agent          # cloud: set GOOGLE_API_KEY in .env
docker compose run --rm ops-agent-local    # local: docker desktop enable model-runner, once

# Dev team — always through the launcher, which runs it in a Docker Sandbox
sbx login && sbx policy init balanced                      # once
sbx secret set google --command "grep '^GOOGLE_API_KEY=' $PWD/.env | cut -d= -f2-"   # once
docker/agents/dev-team.sh                                  # Gemini
# (the local model is not available to the sandboxed dev team yet -- spec_010 GAP-010-8)
```

The sandbox never sees your real key: inside it, `GOOGLE_API_KEY` is a placeholder, and the sandbox proxy substitutes the stored secret on the way out. The `--command` form reads it from `.env` each time, so rotating the key means editing `.env` only.

Try *"Is everything healthy?"*, *"What is p95 latency over the last 15 minutes?"*, or *"Review my uncommitted changes and run the Application tests."*

> Use the launcher, not `docker agent run docker/agents/dev-team.yaml` directly — run that way, the team is **not** sandboxed (its file restrictions still apply, but builds use your host's Docker). The free Gemini tier allows only a few requests per minute and per day; one question makes several, so `HTTP 429` means wait (for the ops team, or use `ops-agent-local`). With the cloud model, everything the agents read is sent to Google — and on the free tier may be used to improve its products; do not point them at real data. Details in [ADR-003](docs/adr/ADR-003-docker-agents.md).

---

## Project structure

```text
src/
  NovaLeave.Domain/            Entities, value objects, invariants — no outward dependencies
  NovaLeave.Application/       Use cases, contracts, input validation
  NovaLeave.Infrastructure/    EF Core, SQL Server, Identity, repositories
  NovaLeave.Presentation.Web/  Controllers, Razor views, composition root

tests/                         Domain, Application, Presentation (integration), E2E
docker/                        Prometheus, Grafana, Loki and Alloy config; agents/ for AI agent teams
.devcontainer/                 Dev container (DevPod / VS Code): .NET 10 workspace joined to the stack
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
