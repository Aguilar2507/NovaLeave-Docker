# ADR-001: Containerized Local Development Environment

**Status**: Accepted
**Date**: 2026-09-22
**Deciders**: Product Owner, Development Team
**Constitution Reference**: v4.0.0 — §3.2 (Approved Stack), §7.2 (Secure Configuration), §14 (Documentation and ADRs)
**Specification**: [spec_005-docker-containerization](../../.specify/specs/005-docker-containerization/spec_005-docker-containerization.md)

---

## Context

Constitution §3.2 lists Docker under the approved infrastructure stack, and `specs/common/architecture.md` repeats it. No specification had delivered it. Running NovaLeave therefore required each developer to install the .NET 10 SDK, provision SQL Server LocalDB — which is Windows-only — and apply EF Core migrations by hand.

Three consequences made this urgent rather than cosmetic:

1. **Onboarding was blocked on non-Windows machines.** LocalDB does not exist on macOS or Linux.
2. **Schema creation was an undocumented manual step.** The codebase called neither `Database.Migrate()` nor `EnsureCreated()` anywhere; migrations had only ever been applied by hand. Any fresh database was empty, and the development seeder failed against it.
3. **The project could not be built at all on at least one developer machine**, where the installed SDK is 7.0.304 against a `net10.0` target.

The third point inverts the usual justification. Containerization here is not a convenience layered over a working setup — it is the only way to compile the project on that machine.

## Decision

Adopt a Docker Compose development stack composed of the ASP.NET Core application and SQL Server 2022, started with a single command, with these specific choices:

1. **Multi-stage `Dockerfile`** with `base` → `build` → `publish` → `final`, plus a separate `development` target. `final` is kept production-shaped (non-root, runtime-only image) even though nothing deploys it yet, so the deferred production specification inherits a working image rather than a rewrite.
2. **SQL Server 2022 in a container**, pinned to `2022-CU27-ubuntu-22.04` and to `platform: linux/amd64`, running under Rosetta emulation on Apple Silicon.
3. **A query-based readiness gate**: the `db` service healthcheck executes `SELECT 1` through `sqlcmd`, and the `web` service declares `depends_on: condition: service_healthy`.
4. **Automatic EF Core migration on startup**, performed by a new `DatabaseInitializer` and **refused in `Production` by a guard in code**, not by configuration.
5. **`dotnet watch` with bind-mounted source** in the `development` target, with container-side volumes masking every `bin/` and `obj/` directory.
6. **HTTP only inside the container**; TLS terminates at a proxy in production.
7. **A `/health` endpoint** including an EF Core `DbContext` check.
8. **The database password in a git-ignored `.env`**, with a committed `.env.example` template.

## Alternatives Considered

| Decision | Alternatives rejected | Reason |
|----------|----------------------|--------|
| SQL Server 2022 under emulation | **Azure SQL Edge** | Retired by Microsoft in 2025, and missing features the project uses |
| | **PostgreSQL for development** | Development and production must share a database engine, or migrations and provider behavior diverge — the exact defect class this feature exists to remove |
| | **Developer-supplied external SQL Server** | Defeats the reproducibility goal |
| Migrate on startup, guarded | **One-shot migrator service** | Cleaner separation, but requires the SDK and EF tooling in the stack and slows first boot |
| | **`EnsureCreated()`** | Bypasses migration history and cannot evolve a schema |
| | **Manual, documented** | Contradicts the single-command requirement |
| Query-based healthcheck | **`depends_on` without a condition** | Waits for process start, not query readiness — the standard cause of intermittent cold-start failure |
| HTTP inside the container | **Mounted development certificate** | Reintroduces a per-developer manual step this feature exists to remove |
| | **Removing `UseHttpsRedirection()`** | Weakens production behavior to serve a development convenience |

## Consequences

### Positive

- One command produces a working environment on any machine with Docker; no .NET SDK, no LocalDB, no manual migration.
- Development and production share a database engine, so provider-specific behavior is exercised locally.
- Schema creation became explicit, testable code instead of tribal knowledge.
- The project regained the ability to be built on a machine whose host SDK is too old.
- A `/health` endpoint now exists — an Observability baseline `architecture.md` required and no feature had delivered.

### Negative

- SQL Server runs emulated on Apple Silicon and is measurably slower. **Performance measurements under Constitution §12.1 must not be taken from this stack.**
- Rosetta emulation is a prerequisite the stack cannot enforce; it can only be documented.
- Automatic migration is safe for a single replica only. Two application instances could migrate concurrently — acceptable in development, unacceptable anywhere else, hence the Production guard.
- One more moving part for developers to understand.

### Neutral

- Developers who prefer LocalDB may continue using it; the container stack is additive.

## Compliance

| Rule | Status |
|------|--------|
| §3.2 — Docker as approved infrastructure | Implements a previously undelivered approved decision |
| §7.2 — Secrets outside the repository | `.env` git-ignored; `.env.example` committed with development-only values |
| §7.2 — Explicit version pinning | SQL Server pinned to an exact cumulative update; .NET images pinned to `10.0` |
| §7.2 — TLS and HSTS in production | Unchanged; HTTP applies only to the development container |
| §8 — Structured logging | Serilog to stdout, format unchanged; migration target logged with credentials redacted |
| §12.2 — Stateless presentation | No container-local business state introduced |
| §2.VI — Testable time | Retry delays use the injected `TimeProvider` |
| §2.VII — Test-first for critical rules | The Production guard is unit-tested; infrastructure files are verified by a documented cold-start procedure |

### Deviations

None. This implements an approved decision within its existing constitutional bounds.

### Gaps Carried Forward

| ID | Gap |
|----|-----|
| GAP-005-1 | No production image. §12.3 operational targets (availability, RTO, RPO, backups) remain unaddressed |
| GAP-005-2 | No CI pipeline builds or tests the image; the §9.3 gate is not exercised against the container |
| GAP-005-3 | Automatic startup migration is development-only. A reviewed, reversible production migration strategy is required before any deployed environment |
| GAP-005-4 | EF Core connection resiliency cannot be enabled while `VacationRequestRepository.ExistsOverlappingAsync` uses a user-initiated `Serializable` transaction. Requires migrating that call to an explicit execution strategy |
| GAP-005-5 | `NovaLeave.Presentation.Tests` fails 24–26 of 49 tests at `HEAD`, independently of this work. See [spec 006](../../.specify/specs/006-integration-test-isolation/spec_006-integration-test-isolation.md) |

## Verification

Executed 2026-09-22 on Docker Engine 29.8.0 / Compose v5.5.1, Apple M2 Pro (arm64):

- Readiness gate observed holding the application back until the database reported healthy.
- Migrations applied on the first attempt; `__EFMigrationsHistory` contains both migrations.
- Authentication succeeded end to end against a seeded account.
- Hot reload confirmed for both `.cshtml` (~10 s) and `.cs` (~15 s).
- Data persisted across `down`/`up`; `down -v` produced a freshly migrated and seeded database.
- Five consecutive cold starts succeeded in 21–29 s. SC-002 asks for ten; five remain outstanding.

Full record in [tasks.md](../../.specify/specs/005-docker-containerization/tasks.md).

## References

- [spec_005-docker-containerization](../../.specify/specs/005-docker-containerization/spec_005-docker-containerization.md)
- [research.md](../../.specify/specs/005-docker-containerization/research.md) — R-001 … R-010, decisions and rejected options
- [quickstart.md](../../.specify/specs/005-docker-containerization/quickstart.md)
