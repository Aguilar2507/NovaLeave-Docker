# Implementation Plan: Containerized Local Development Environment

**Branch**: `005-docker-containerization` | **Date**: 2026-09-22 | **Spec**: [spec_005-docker-containerization.md](./spec_005-docker-containerization.md)

**Input**: Feature specification from `.specify/specs/005-docker-containerization/spec_005-docker-containerization.md`

---

## Summary

Deliver a single-command, reproducible local development stack: a multi-stage Dockerfile for the ASP.NET Core MVC application, a Docker Compose definition orchestrating it with SQL Server 2022, a query-based database readiness gate, environment-guarded automatic EF Core migration on startup, hot reload via `dotnet watch`, a `/health` endpoint, and documentation covering prerequisites, start, reset, and the arm64 emulation requirement.

Scope is the development environment only. A production image, CI integration, and a production migration strategy are explicitly deferred and recorded as gaps.

---

## Technical Context

**Language/Version**: C# / .NET 10
**Primary Dependencies**: Docker Engine + Compose v2, ASP.NET Core MVC, EF Core (SQL Server provider), ASP.NET Core Identity, Serilog
**Storage**: SQL Server 2022 in a container, named volume for persistence
**Testing**: xUnit (existing suite must pass unchanged); manual cold-start verification per `quickstart.md`
**Target Platform**: Linux containers on arm64 (Apple Silicon, via amd64 emulation for the database) and amd64
**Project Type**: Web application (server-rendered MVC), Clean Architecture
**Performance Goals**: cold start < 3 min, warm start < 45 s, incremental rebuild < 30 s (NFR-001..003)
**Constraints**: No business behavior may differ inside the container (FR-022); no secret in version control (FR-011); automatic migration impossible in `Production` (FR-006)

---

## Common Artifacts (DO NOT DUPLICATE)

- **Architecture & Tech Stack**: [`specs/common/architecture.md`](../common/architecture.md) — Containerization: Docker; Observability: health checks; Clean Architecture layers
- **Security Requirements**: [`specs/common/security.md`](../common/security.md) — secret handling, TLS posture
- **Data Model**: [`specs/common/data-model.md`](../common/data-model.md) — unchanged by this feature

---

## Constitution Check

| Gate | Status |
|------|--------|
| Approved stack — Docker infrastructure (§3.2) | ✅ Implements an approved, previously undelivered decision |
| Clean Architecture (§2.I) | ✅ Changes confined to composition root + infrastructure files; no layer violation |
| Secrets outside the repository (§7.2) | ✅ `.env` git-ignored, `.env.example` committed |
| Explicit version pinning (§7.2) | ✅ All image tags pinned; rolling-minor caveat recorded in research R-001 |
| TLS/HSTS in production (§7.2) | ✅ Unchanged; HTTP applies to the development container only |
| Structured logging (§8) | ✅ Serilog to stdout, format unchanged |
| Observability — health checks (architecture.md) | ✅ `/health` delivered |
| Stateless presentation (§12.2) | ✅ No container-local business state |
| Async I/O (§2.VIII) | ✅ `MigrateAsync`, async health checks |
| ADR recorded (§14) | ✅ `docs/adr/ADR-001-docker-local-development-environment.md` |
| Test-first discipline (§2.VII) | ⚠️ Partial — see Complexity Tracking |
| Git workflow (§16.1) | ✅ Feature branch, Conventional Commits |
| Production operational targets (§12.3) | ⚠️ Not applicable to development; recorded as GAP-005-1 |

### Complexity Tracking

| Item | Justification |
|------|---------------|
| Test-first discipline (§2.VII) applied partially | Infrastructure definition files (Dockerfile, Compose) are not unit-testable in a meaningful sense; their verification is the documented cold-start procedure in `quickstart.md`. The one behavioral code change — the migration guard — **is** covered by a unit test asserting that migration does not run under `Production`. Accepted as proportionate rather than as a deviation. |
| Two-layer readiness (healthcheck + app retry) | Not redundancy: the healthcheck covers cold start, the retry covers a mid-session database restart. Justified in research R-003. |

---

## Project Structure

### Documentation (this feature)

```text
.specify/specs/005-docker-containerization/
├── spec_005-docker-containerization.md   # Feature specification
├── research.md                           # Phase 0: resolved unknowns (R-001..R-009)
├── plan.md                               # This file
├── tasks.md                              # Phase 2: ordered task breakdown
└── quickstart.md                         # Phase 1: developer-facing getting started

docs/adr/
└── ADR-001-docker-local-development-environment.md
```

### Source Changes

```text
/                                         # Repository root
├── Dockerfile                            # NEW — multi-stage: base → build → publish → final, plus development
├── compose.yaml                          # NEW — web + db services, volumes, healthcheck
├── .dockerignore                         # NEW — excludes bin/obj/.git/.env/IDE state
├── .env.example                          # NEW — committed template (dev placeholder values)
├── .env                                  # NEW — git-ignored, developer-local
├── .gitignore                            # MODIFIED — add .env
└── README.md                             # MODIFIED — Docker quickstart section

src/NovaLeave.Infrastructure/
├── NovaLeave.Infrastructure.csproj       # MODIFIED — add HealthChecks.EntityFrameworkCore
└── Persistence/
    └── DatabaseInitializer.cs            # NEW — guarded migrate + seed, with bounded retry

src/NovaLeave.Presentation.Web/
├── Program.cs                            # MODIFIED — health checks, DatabaseInitializer, SQL retry
└── appsettings.Docker.json               # NEW — container-specific configuration

tests/NovaLeave.Application.Tests/ (or Presentation.Tests)
└── DatabaseInitializerTests.cs           # NEW — asserts the Production guard
```

---

## Phase Breakdown

### Phase 0 — Research *(complete)*

All unknowns resolved and recorded in [`research.md`](./research.md). No outstanding questions block implementation.

### Phase 1 — Application Readiness (code)

Prepares the application to run correctly in a container *before* any container file exists, so failures during Phase 2 are unambiguously container failures rather than application failures.

1. Add the health-check package to Infrastructure.
2. Create `DatabaseInitializer` — an infrastructure service that applies pending migrations and runs the existing seeder, guarded against `Production`, with bounded retry (FR-005..008).
3. Register health checks (`/health`) including the EF Core `DbContext` check (FR-018).
4. ~~Enable SQL Server connection resiliency (`EnableRetryOnFailure`).~~ **Rejected during implementation** — incompatible with the existing user-initiated `Serializable` transaction in `VacationRequestRepository`. See `research.md` → R-010 and GAP-005-4.
5. Replace the inline seeding block in `Program.cs` with a call to `DatabaseInitializer`.
6. Unit-test the `Production` guard.

**Exit criteria**: solution builds; existing tests pass; migration is provably skipped under `Production`.

### Phase 2 — Container Definition

7. `.dockerignore` (FR-013) — authored **before** the Dockerfile so the first build never sends `bin/`/`obj/` as context.
8. Multi-stage `Dockerfile`: `base` → `build` → `publish` → `final`, plus a `development` target (FR-001, FR-014, R-005, R-007).
9. `.env.example` and `.gitignore` update (FR-011).
10. `compose.yaml`: `db` and `web` services, named volumes, `sqlcmd` healthcheck, `service_healthy` dependency, platform pin, port override (FR-002, FR-004, FR-009, FR-012, FR-016, FR-017).
11. `appsettings.Docker.json` — container connection string and logging.

**Exit criteria**: `docker compose up` produces a serving application against a freshly migrated, freshly seeded database.

### Phase 3 — Developer Experience

12. Wire hot reload (`dotnet watch` + bind mount + masked `obj`/`bin`) (FR-015).
13. Verify the reset path and document it (FR-010).

**Exit criteria**: a `.cs` edit is observable in the browser without a manual rebuild; reset returns to freshly seeded state.

### Phase 4 — Documentation and Governance

14. `quickstart.md` — prerequisites, start, reset, credentials, DB client connection, arm64 note, troubleshooting (FR-020).
15. `README.md` Docker section linking to the quickstart.
16. ADR-001 (Constitution §14).
17. Update `specs/common/architecture.md` to reference this feature as the delivery of the Docker decision.

**Exit criteria**: a developer who has never seen the project can follow `quickstart.md` unaided (SC-001).

---

## Verification Strategy

| Success Criterion | How it is verified |
|-------------------|--------------------|
| SC-001 — 10-minute onboarding | Walk `quickstart.md` verbatim on a clean clone |
| SC-002 — 10 consecutive cold starts | Scripted loop: `compose down -v` then `compose up`, assert HTTP 200 on `/health` |
| SC-003 — identical behavior | Existing `dotnet test` suite passes unchanged; grep confirms no container-conditional branch |
| SC-004 — nothing secret committed | `git status` clean after a full run; `.env` ignored |
| SC-005 — Production guard | Unit test plus a manual `ASPNETCORE_ENVIRONMENT=Production` start |

---

## Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Rosetta emulation unavailable or disabled on a developer machine | Database will not start | Documented prerequisite with the exact Docker Desktop setting; failure mode is a clear log line, not a hang |
| SQL Server under emulation is slow enough to exceed the healthcheck window | Spurious cold-start failure | Generous `start_period` on the healthcheck; tuned during Phase 2 verification |
| `sqlcmd` path differs across SQL Server image versions | Healthcheck silently never passes | Path verified against the pinned 2022 tag during implementation, not copied from documentation |
| Hot reload bind mount corrupts host build output | Confusing local build failures | `obj/` and `bin/` masked with container-side volumes (R-005) |
| Automatic migration reaches a deployed environment | Unreviewed schema change in production | Guarded in code, not configuration (FR-006); unit-tested; recorded as GAP-005-3 |
| Docker is not installed on the implementing machine | Cannot verify the stack end to end | Implementation proceeds; verification is explicitly reported as outstanding until Docker is available |

---

## Out of Scope

Production image hardening, container orchestration, image registry and signing, CI container builds, container-level secret management beyond local development, observability sidecars, and database backup/restore. Each requires its own specification.
