# Phase 0 Research: Containerized Local Development Environment

**Feature**: `005-docker-containerization`
**Date**: 2026-09-22
**Spec**: [spec_005-docker-containerization.md](./spec_005-docker-containerization.md)

---

## Purpose

This document records the technical unknowns resolved before implementation, the options considered for each, and the rationale for the decision taken. Decisions recorded here are binding on `plan.md` and `tasks.md`.

---

## R-001: Application Base Images

**Question**: Which base images should build and run the ASP.NET Core application?

**Options considered**:

| Option | Assessment |
|--------|------------|
| `mcr.microsoft.com/dotnet/sdk:10.0` (build) + `mcr.microsoft.com/dotnet/aspnet:10.0` (runtime) | Official, standard split; runtime image excludes the SDK |
| SDK image for both build and run | Simpler, but ships a full SDK at runtime — unacceptable for a forward-compatible production path |
| Alpine variants | Smaller, but adds musl/ICU considerations that buy nothing in a development stack |
| Chiseled / distroless variants | Correct target for production hardening; premature here, and lacks the shell the dev workflow uses |

**Decision**: Debian-based `sdk:10.0` for build and `aspnet:10.0` for runtime, pinned to the 10.0 major tag.

**Rationale**: The two-stage split is what the production image will also use, so the structure written now carries forward unchanged. Debian variants avoid globalization surprises with `DateOnly`/`TimeProvider`-heavy code and EF Core collations. Image size is not a development constraint.

**Note on pinning**: `10.0` is a rolling minor tag. For development this is the right trade-off — patch updates arrive without churn. The production specification MUST pin to a digest or a full patch tag; recorded as a forward constraint, not applied here.

---

## R-002: SQL Server on arm64 (Apple Silicon)

**Question**: How does SQL Server run on the team's Apple Silicon hardware?

**Context**: The official `mcr.microsoft.com/mssql/server` image publishes **amd64 manifests only**. On an arm64 host, Docker either fails to resolve a manifest or silently selects an unexpected platform.

**Options considered**:

| Option | Assessment |
|--------|------------|
| `mssql/server:2022-latest` with `platform: linux/amd64` | Full SQL Server feature parity; requires Rosetta emulation; performance penalty acceptable for development |
| Azure SQL Edge | Previously the common arm64 workaround, but **retired by Microsoft in 2025** and missing features the project relies on. Rejected |
| PostgreSQL for development | Different provider than production; would invalidate EF Core migrations and violate Constitution §3.2. Rejected outright |
| Developer-supplied external SQL Server | Defeats the reproducibility goal of the feature |

**Decision**: `mcr.microsoft.com/mssql/server:2022-CU27-ubuntu-22.04` with `platform: linux/amd64` declared explicitly, and Rosetta emulation documented as a prerequisite.

**Rationale**: Development and production must use the same database engine, or migrations and provider-specific behavior diverge — precisely the defect class this feature exists to eliminate. Explicit platform pinning converts an obscure manifest error into a documented, understood emulation step.

**Consequence**: Database operations run measurably slower under emulation. Acceptable for development; noted so nobody benchmarks against this stack (Constitution §12.1 performance measurement must not be performed here).

---

## R-003: Database Readiness Gate

**Question**: How does the application container know the database is ready?

**Context**: SQL Server accepts TCP connections well before it can answer queries. `depends_on` alone only waits for container start, which is the wrong signal.

**Options considered**:

| Option | Assessment |
|--------|------------|
| `depends_on` without a condition | Waits for process start only. Fails cold starts intermittently. Rejected |
| `depends_on` with `condition: service_healthy` + a query-based healthcheck | Compose gates startup on an actual executed query. Correct signal |
| Application-side retry only | Works, but the first run floods logs with connection exceptions and obscures genuine failures |
| A wait-for-it style entrypoint script | Reimplements what Compose healthchecks already provide |

**Decision**: A healthcheck on the database service that executes a trivial query via `sqlcmd`, combined with `depends_on: condition: service_healthy` on the application service, **and** a bounded application-side retry as a second layer.

**Rationale**: Two layers are justified rather than redundant. The healthcheck handles cold start; the retry handles the narrower window where the database restarts while the application is already running. `sqlcmd` lives at `/opt/mssql-tools18/bin/sqlcmd` in the 2022 image and requires `-C` to trust the self-signed certificate — a detail that silently breaks healthchecks copied from older 2019-era examples.

---

## R-004: Schema Creation Strategy

**Question**: How is the database schema created inside the container?

**Context**: The codebase currently calls neither `Database.Migrate()` nor `EnsureCreated()` anywhere. Migrations exist under `src/NovaLeave.Infrastructure/Identity/Migrations/` but have only ever been applied by hand against LocalDB. A fresh container database is therefore empty, and `DevelopmentDataSeeder` would fail on first run.

**Options considered**:

| Option | Assessment |
|--------|------------|
| `MigrateAsync()` on startup, environment-guarded | Single command start; one code change in the composition root; standard for development |
| A one-shot `dotnet ef database update` migrator service in Compose | Cleaner separation of concerns, but requires the SDK and the EF tool in the stack and slows first boot |
| `EnsureCreated()` | Bypasses the migration history entirely and cannot evolve a schema. Actively harmful. Rejected |
| Manual, documented | Zero code change, but contradicts FR-003 (single command) |

**Decision**: `MigrateAsync()` invoked at startup, guarded so it cannot execute when the environment is `Production`.

**Rationale**: The guard is the load-bearing part of this decision. Automatic migration in a deployed environment is a genuine operational hazard — it couples schema change to deployment, executes without review, and has no rollback path. Guarding in code rather than by configuration convention means a misconfigured environment variable cannot cause it (FR-006).

**Consequence**: Recorded as **GAP-005-3**. A production migration and rollback strategy must exist before any deployed environment is introduced.

---

## R-005: Inner Development Loop

**Question**: How do source changes reach the running container?

**Options considered**:

| Option | Assessment |
|--------|------------|
| Rebuild the image on every change | Correct but far too slow; developers would abandon the stack |
| Bind-mount source + `dotnet watch` in an SDK-based development stage | Fast feedback; keeps the production-shaped Dockerfile intact via a separate target |
| Compose Watch (`develop.watch`) with `sync` + `rebuild` actions | Native Compose feature, declarative, no bind-mount permission issues |
| Volume-mount compiled output from the host | Requires a local .NET 10 SDK — defeats the entire purpose of the feature |

**Decision**: A dedicated `development` build stage on the SDK image running `dotnet watch`, with source bind-mounted, plus a named volume for `obj/`/`bin/` so host artifacts never collide with container artifacts.

**Rationale**: Hot reload is what makes the environment survive contact with daily use (User Story 3 rationale). Mounting host `obj/`/`bin/` into a Linux container while the host may also build is a well-known source of corrupt intermediate state; masking those paths with anonymous/named volumes prevents it. The `final` runtime stage remains untouched and production-shaped, so the deferred production spec inherits a working Dockerfile rather than a rewrite.

---

## R-006: HTTPS Inside the Container

**Question**: `Program.cs` calls `UseHttpsRedirection()`. What happens in a container serving plain HTTP?

**Finding**: When no HTTPS port is configured, ASP.NET Core's HTTPS redirection middleware logs a warning and **forwards the request without redirecting**. It does not produce a redirect loop and does not make the application unreachable.

**Options considered**:

| Option | Assessment |
|--------|------------|
| HTTP only in the development container | Simplest; middleware degrades safely; matches how the app will sit behind a TLS-terminating proxy in production |
| Mount a development certificate and serve HTTPS | Adds per-developer certificate generation — a manual step this feature exists to remove |
| Remove `UseHttpsRedirection()` | Would weaken production behavior to serve a development convenience. Rejected |

**Decision**: HTTP only inside the container, `ASPNETCORE_HTTP_PORTS=8080`, no code change to the middleware pipeline.

**Rationale**: Constitution §7.2 mandates TLS 1.2+ and HSTS **in production**, and `Program.cs` already applies HSTS only outside Development. Terminating TLS at a proxy is the intended production topology, so HTTP inside the container is architecturally correct rather than a shortcut. The startup warning is expected; it is documented so it is not mistaken for a defect.

---

## R-007: Container User

**Question**: Should the container run as a non-root user?

**Decision**: Yes for the runtime stage, using the `APP_UID` non-root user the .NET 10 images provide by default. The development stage runs as root.

**Rationale**: The .NET 8+ images ship a non-root user and honour `USER $APP_UID` without additional setup, so there is no cost to doing this correctly now. The development stage is exempted deliberately: `dotnet watch` writes to bind-mounted source, and non-root ownership across a host bind mount is a recurring source of permission failures that would cost more than the threat model of a local development container justifies.

---

## R-008: Secret Handling

**Question**: Where does the SQL Server password live?

**Decision**: In a `.env` file excluded from version control, with a committed `.env.example` template carrying a development-only placeholder. Compose reads `.env` automatically from the project root.

**Rationale**: Constitution §7.2 requires secrets outside the repository. The value is development-only and grants access to nothing but a disposable local database, but committing it would establish exactly the habit the rule exists to prevent, and would be indistinguishable from a real leak to any secret scanner.

**Constraint discovered**: SQL Server refuses to start if `MSSQL_SA_PASSWORD` fails its complexity policy (8+ characters across three of: uppercase, lowercase, digits, symbols). The failure appears in the database container log and is easily misread as a corrupt image, so the template value satisfies the policy and the requirement is documented.

---

## R-009: Health Endpoint

**Question**: How is application health exposed?

**Decision**: ASP.NET Core health checks at `/health`, including an EF Core `DbContext` check, registered in the composition root.

**Rationale**: `common/architecture.md` already lists health checks under Observability as a baseline, not an option, and no specification has delivered them. Container health is otherwise inferred from "the process is running", which is untrue often enough to matter. This also gives the deferred production specification a readiness probe it would otherwise have to add.

**Package**: `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`.

---

## R-010: Connection Resiliency (`EnableRetryOnFailure`)

**Question**: Should the SQL Server provider be configured with `EnableRetryOnFailure` to absorb transient container-network faults?

**Plan's original intent**: Yes (task T104).

**Finding during implementation**: `VacationRequestRepository.ExistsOverlappingAsync` opens a **user-initiated `Serializable` transaction** via `BeginTransactionAsync` (`src/NovaLeave.Infrastructure/Persistence/VacationRequestRepository.cs:52`), which spec 001 introduced deliberately to close the overlap race condition (D-003).

EF Core throws `InvalidOperationException` — *"The configured execution strategy 'SqlServerRetryingExecutionStrategy' does not support user-initiated transactions"* — whenever a retrying execution strategy meets a manually started transaction. Enabling retry would therefore break vacation-request creation at runtime, in the application's central workflow, and only against SQL Server — never against the InMemory provider the test suite uses. The test suite would stay green while the feature was broken.

**Options considered**:

| Option | Assessment |
|--------|------------|
| Do not enable `EnableRetryOnFailure` | Startup resilience is already provided by the `DatabaseInitializer` retry budget, which is what the cold-start problem actually requires |
| Enable it and wrap the transaction in `CreateExecutionStrategy().ExecuteAsync(...)` | The correct fix in the long run, but it rewrites spec 001 concurrency code from inside an infrastructure feature, with no test able to catch a regression |
| Enable it and drop the `Serializable` transaction | Reintroduces the overlap race condition spec 001 exists to prevent. Rejected outright |

**Decision**: Do **not** enable `EnableRetryOnFailure`. T104 is recorded as rejected rather than pending.

**Rationale**: The problem this feature must solve is a *cold start* — the database is unreachable for the first few seconds of `compose up`. That is handled precisely by the bounded retry in `DatabaseInitializer`. Steady-state transient resiliency is a production concern, and buying it here would trade a real, working concurrency guarantee for a benefit the development stack does not need.

**Consequence**: Recorded as **GAP-005-4**. Adopting connection resiliency requires first migrating `ExistsOverlappingAsync` to an explicit execution strategy, which belongs to the production containerization specification alongside its own concurrency tests.

---

## Resolved Unknowns Summary

| ID | Unknown | Resolution |
|----|---------|------------|
| R-001 | Base images | `sdk:10.0` build, `aspnet:10.0` runtime |
| R-002 | SQL Server on arm64 | `mssql/server:2022-CU27-ubuntu-22.04`, `platform: linux/amd64`, Rosetta documented |
| R-003 | Readiness gate | `sqlcmd` healthcheck + `service_healthy` + bounded app-side retry |
| R-004 | Schema creation | `MigrateAsync()` on startup, hard-guarded against `Production` |
| R-005 | Inner loop | SDK `development` stage with `dotnet watch`, masked `obj`/`bin` volumes |
| R-006 | HTTPS | HTTP only in container; middleware degrades safely; no code change |
| R-007 | Container user | Non-root in runtime stage; root in development stage, deliberately |
| R-008 | Secrets | `.env` ignored, `.env.example` committed |
| R-009 | Health | `/health` with EF Core DbContext check |
| R-010 | Connection resiliency | **Rejected** — incompatible with the existing `Serializable` transaction (GAP-005-4) |

---

## Forward Constraints for the Production Specification

Recorded here so the deferred production image does not have to rediscover them:

1. Pin runtime base image to a digest or full patch tag, not a rolling minor tag.
2. Replace automatic startup migration with a reviewed, reversible migration step (GAP-005-3).
3. Terminate TLS at a proxy or ingress; the application continues to serve HTTP internally.
4. Add the container build and test to CI (GAP-005-2).
5. Consider chiseled base images once the shell is no longer needed for diagnostics.
6. Supply the database password from a managed secret store, never from `.env`.
