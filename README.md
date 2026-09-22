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

---

## Documentation

| Document | Purpose |
|----------|---------|
| **[Keep in mind](docs/Keep-in-mind.md)** | **Open work, known issues and decisions not to re-litigate — read this before picking up a task** |
| [Constitution](.specify/memory/constitution.md) | Binding architecture, security and process rules |
| [Architecture](.specify/specs/common/architecture.md) | Shared technical context and stack |
| [Docker quickstart](.specify/specs/005-docker-containerization/quickstart.md) | Running the stack locally |
| [ADR index](docs/adr/) | Recorded architectural decisions |

Specifications live under `.specify/specs/`, numbered by feature.
