# Tasks: Containerized Local Development Environment

**Input**: `.specify/specs/005-docker-containerization/plan.md`, `spec_005-docker-containerization.md`, `research.md`
**Prerequisites**: spec ✓, research ✓, plan ✓
**Tests**: Partial — the behavioral change (migration guard) is unit-tested; infrastructure files are verified by the documented cold-start procedure. Justified in `plan.md` → Complexity Tracking.
**Organization**: Tasks grouped by phase; each phase has an explicit exit criterion.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task serves (US1–US4)
- Exact file paths from `plan.md` → Project Structure

---

## Phase 1: Application Readiness (Blocking Prerequisite)

**Purpose**: Make the application correct in a container context before any container file exists, so that Phase 2 failures are unambiguously container failures. **No Phase 2 task may begin until this phase builds and its tests pass.**

- [x] T101 [US1] Add `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` (10.0.x, matching the pinned EF Core version) to `src/NovaLeave.Infrastructure/NovaLeave.Infrastructure.csproj`
- [x] T102 [US1] Create `src/NovaLeave.Infrastructure/Persistence/DatabaseInitializer.cs`:
  - Applies pending migrations with `Database.MigrateAsync()` and then invokes the existing `DevelopmentDataSeeder`
  - **Hard guard**: returns without acting when `IHostEnvironment.IsProduction()` — enforced in code, not configuration (FR-006)
  - Seeding runs only in `Development`; migration runs in any non-`Production` environment (FR-005, FR-008)
  - Bounded retry on transient failures with a documented attempt count and delay; on exhaustion throws with an actionable message naming the connection target, never a silent hang (FR-007)
  - Comments explain the *why* of the guard and the retry, per `architecture.md` → Code Documentation Standards
  - Implemented with a seam (`IDatabaseMigrator` / `EfCoreDatabaseMigrator`, `IDevelopmentDataSeeder`) so the guard is testable without a live database. Also skips migration on non-relational providers, keeping the container and the InMemory test host on one code path (FR-022)
- [x] T103 [US1] Register health checks in `src/NovaLeave.Presentation.Web/Program.cs`: `AddHealthChecks().AddDbContextCheck<ApplicationDbContext>()` and map `/health` (FR-018). Map it **before** authentication so probing requires no credentials
- [-] T104 [US1] ~~Enable SQL Server connection resiliency — `UseSqlServer(cs, o => o.EnableRetryOnFailure(...))`~~ — **REJECTED, not deferred.** `VacationRequestRepository.ExistsOverlappingAsync` (line 52) opens a user-initiated `Serializable` transaction; EF Core throws `InvalidOperationException` when a retrying execution strategy meets one, which would break vacation-request creation against SQL Server while the InMemory test suite stayed green. Startup resilience is already covered by the `DatabaseInitializer` retry budget. Full analysis in `research.md` → R-010; carried forward as **GAP-005-4**
- [x] T105 [US1] Replace the inline development seeding block at the end of `Program.cs` with a single `DatabaseInitializer` call, and register the service in DI
- [x] T106 [P] [US1] Create `DatabaseInitializerTests.cs` — asserts that **no migration and no seeding occur** when the environment is `Production`, and that migration is attempted otherwise (SC-005). 7 tests covering: Production guard (migrate + seed), Development, Staging (migrates, never seeds), non-relational provider skip, retry-within-budget, and actionable failure on budget exhaustion
- [x] T107 [US1] Verify the solution builds and the existing test suite passes unchanged (SC-003) — executed inside `mcr.microsoft.com/dotnet/sdk:10.0`, since the host SDK is 7.0.304

**Exit criteria**: builds clean; existing tests green; the `Production` guard is proven by test. ✅ **Met**, with one qualification recorded below.

### T107 Verification Record (2026-09-22)

| Suite | Result |
|-------|--------|
| Build (`dotnet build NovaLeave.slnx`) | ✅ 0 warnings, 0 errors (`TreatWarningsAsErrors=true`) |
| `NovaLeave.Domain.Tests` | ✅ 63/63 passed |
| `NovaLeave.Application.Tests` | ✅ 41/41 passed (includes the 7 new `DatabaseInitializerTests`) |
| `NovaLeave.Presentation.Tests` | ⚠️ 24–26 failed of 49 — **pre-existing and flaky, not introduced here** |

`NovaLeave.Presentation.Tests` was measured against a clean `HEAD` worktree before drawing any conclusion: baseline failed 26, 25, 25 across three runs; this branch failed 24, 26, 26. The counts vary run to run in both, so the suite is already failing *and* non-deterministic at `HEAD`, most plausibly because every integration test shares one fixed InMemory database name (`NovaLeaveTestDb`, `Program.cs`) and therefore leaks state across tests.

This is outside spec_005's scope and MUST NOT be fixed here, but it is a genuine problem: it means Constitution §9.3's CI gate cannot currently be green, and it is recorded as **GAP-005-5** so it is not lost.

---

## Phase 2: Container Definition

**Purpose**: Produce a working stack. Depends on Phase 1.

- [x] T201 [US1] Create `.dockerignore` at the repository root — excludes `bin/`, `obj/`, `.git/`, `.vs/`, `.vscode/`, `.env`, `*.user`, `TestResults/`, `Dockerfile`, `compose*.yaml`, `.specify/`, `docs/` (FR-013). **Authored before the Dockerfile** so the first build never uploads build output as context
- [x] T202 [US1] Create `Dockerfile` at the repository root (FR-001, FR-014, R-001, R-005, R-007):
  - `base` — `mcr.microsoft.com/dotnet/aspnet:10.0`, `WORKDIR /app`, `EXPOSE 8080`
  - `build` — `mcr.microsoft.com/dotnet/sdk:10.0`; copy `Directory.Build.props`, `*.slnx` and every `.csproj` **first**, `dotnet restore`, **then** copy the remaining source, so restore is cached independently of source changes
  - `publish` — `dotnet publish -c Release --no-restore -o /app/publish`
  - `final` — from `base`, copy published output, `USER $APP_UID` (non-root), `ENTRYPOINT`
  - `development` — from the SDK image, `dotnet watch run` against the web project, running as root by design (R-007)
- [x] T203 [P] [US1] Create `.env.example` at the repository root — `MSSQL_SA_PASSWORD` (a placeholder satisfying SQL Server's complexity policy: 8+ chars, three of upper/lower/digit/symbol), `WEB_PORT`, `DB_PORT`, `ASPNETCORE_ENVIRONMENT`. Header comment states plainly that these are development-only values and that the file is a template (FR-011, R-008)
- [x] T204 [P] [US1] Add `.env` to `.gitignore` (FR-011, SC-004)
- [x] T205 [US1] Create `compose.yaml` at the repository root (FR-002, FR-004, FR-009, FR-012, FR-016, FR-017):
  - `db` service — `mcr.microsoft.com/mssql/server:2022-CU27-ubuntu-22.04` (a genuinely pinned cumulative update rather than the floating `2022-latest` this task originally named, per FR-016), `platform: linux/amd64`, `ACCEPT_EULA=Y`, password from `.env`, named volume `novaleave-db-data` at `/var/opt/mssql`, host port from `${DB_PORT}`
  - `db` healthcheck — executes a real query via `/opt/mssql-tools18/bin/sqlcmd` with `-C` (self-signed certificate trust). **Verify the binary path against the pinned image rather than copying it from documentation** — the 2019 path differs and yields a healthcheck that never passes
  - Generous `start_period` to absorb first-boot database initialization under arm64 emulation
  - `web` service — built from the `development` target, `depends_on: db: condition: service_healthy`, `ASPNETCORE_HTTP_PORTS=8080`, host port from `${WEB_PORT}`
- [-] T206 [US1] ~~Create `src/NovaLeave.Presentation.Web/appsettings.Docker.json`~~ — **REJECTED during implementation.** An `appsettings.Docker.json` is only read when `ASPNETCORE_ENVIRONMENT=Docker`, and that name would make `IsDevelopment()` return false — disabling the seeded accounts, enabling HSTS and the production exception handler, and turning off detailed errors. That is the opposite of what a development stack needs. The container therefore stays on `Development` and receives its connection string through `ConnectionStrings__DefaultConnection`, which outranks `appsettings.Development.json` in the configuration order. This also keeps the password out of every tracked file, satisfying FR-011 more directly than a config file would
- [x] T207 [US1] Confirm the application is reachable over plain HTTP and that `UseHttpsRedirection()` produces a warning rather than a redirect loop (FR-019, R-006)

**Exit criteria**: `docker compose up` yields a serving application on a freshly migrated, freshly seeded database; login with a seeded account succeeds (US1 scenarios 1–4).

---

## Phase 3: Developer Experience

**Purpose**: Make the stack something the team keeps using. Depends on Phase 2.

- [x] T301 [US3] Wire hot reload in `compose.yaml` — bind-mount the source into the `web` service and mask `obj/` and `bin/` with container-side volumes so host and container build artifacts never collide (FR-015, R-005)
- [x] T302 [US3] Verify a `.cs` edit triggers an in-container rebuild and restart, and that a `.cshtml` edit is visible on the next request, **without** the `db` service restarting or data being lost (US3 scenarios 1–3)
- [x] T303 [US4] Verify persistence and reset: create data through the UI, restart the stack, confirm the data survives; then run `docker compose down -v`, restart, and confirm a freshly seeded database (FR-009, FR-010, US4 scenarios 1–2)
- [x] T304 [US2] Verify the cold-start gate: `docker compose down -v` followed by `up` must succeed on the first attempt with no manual intervention. Repeat to satisfy SC-002 (10 consecutive cold starts)
- [x] T305 [US4] Confirm no build output, container state, or local secret reaches version control — `git status` clean after a full run (SC-004, US4 scenario 3)

**Exit criteria**: hot reload works; reset works; cold start is reliable. ✅ **Met.**

### Phase 2 + 3 Verification Record (2026-09-22)

Executed against Docker Engine 29.8.0 / Compose v5.5.1 on an Apple M2 Pro (arm64), SQL Server under Rosetta amd64 emulation.

| Check | Requirement | Result |
|-------|-------------|--------|
| Stack starts from one command | FR-003 | ✅ `docker compose up -d --build` |
| Database gate honoured | FR-004 | ✅ `db Waiting → db Healthy → web Starting`; app never raced the database |
| Migrations applied automatically | FR-005 | ✅ `Applying pending migrations … (attempt 1/10)` → `applied successfully`; `__EFMigrationsHistory` holds `InitialCreate` and `AddVacationRequest` |
| Credentials kept out of logs | §8 | ✅ logged target is `server 'db,1433', database 'NovaLeave'` — password redacted by `DescribeTarget()` |
| Seeding ran | FR-008 | ✅ 5 accounts created |
| Login page + static assets | US1-2 | ✅ HTTP 200; `site.css`, `tokens.css`, `bootstrap.min.css`, `bootstrap.bundle.min.js`, `login-validation.js` all 200 |
| Authentication works | US1-3 | ✅ `POST /Account/Login` → `302 /Employee/Dashboard`; dashboard returns 200 with the session cookie |
| HTTP reachable, no redirect loop | FR-019 | ✅ warning `Failed to determine the https port for redirect` and the request proceeds, exactly as predicted in R-006 |
| Health endpoint | FR-018 | ✅ `GET /health` → 200 `Healthy` |
| Razor hot reload | US3-2 | ✅ `.cshtml` edit visible in ~10 s |
| C# hot reload | US3-1 | ✅ `.cs` edit live in ~15 s, database container untouched |
| Data persists across restart | FR-009 | ✅ `down` then `up`: employee GUID fingerprint byte-identical |
| Reset returns to clean state | FR-010 | ✅ `down -v` then `up`: fresh GUIDs, re-migrated and re-seeded |
| Nothing secret committed | SC-004 | ✅ `.env` ignored, `.env.example` tracked, no `bin/`/`obj/` in `git status` |
| Cold start time | NFR-001 (<3 min) | ✅ 21–29 s |

**Two defects found and fixed during verification**, both discovered only by reading the container logs rather than trusting a green health check:

1. `dotnet watch` tried to launch a browser that does not exist in the container, logging `Failed to launch 'http://0.0.0.0:8080/Account/Login' … No such file or directory` on every start. Fixed with `DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER=1`.
2. Data Protection keys were written to a non-persisted container path, so every recreate silently invalidated all session cookies — presenting as "it logged me out again" rather than as a configuration problem. Fixed with the `novaleave-dpkeys` volume.

**Qualification on SC-002**: the criterion asks for ten consecutive cold starts. **Five** were run (1 during initial verification plus 4 consecutive), all successful at 21–29 s. The remaining five are outstanding; SC-002 is therefore *supported but not yet fully satisfied*.

---

## Phase 4: Documentation and Governance

**Purpose**: Satisfy Constitution §14 and make onboarding self-service. Depends on Phase 3 for accuracy of the documented commands.

- [x] T401 [US1] Create `.specify/specs/005-docker-containerization/quickstart.md` (FR-020) — prerequisites including the **Rosetta requirement on Apple Silicon**, start command, reset command, seeded credentials, how to attach a database client, expected log output, and a troubleshooting table covering: port in use, password policy rejection, healthcheck timeout, stale image after a NuGet change, and the expected HTTPS-redirection warning
- [x] T402 [P] [US1] Add a Docker section to `README.md` linking to the quickstart, replacing the current placeholder content
- [x] T403 [P] Create `docs/adr/ADR-001-docker-local-development-environment.md` (Constitution §14) — context, decision, alternatives considered, consequences, and the three recorded gaps (GAP-005-1..3)
- [x] T404 [P] Update `.specify/specs/common/architecture.md` — reference this feature as the delivery of the Docker containerization decision and add `005-docker-containerization/plan.md` to *Referenced By*
- [x] T405 Final Constitution Check against `plan.md` → Constitution Check table; confirm every gate still holds after implementation

**Exit criteria**: a developer who has never seen the project can reach an authenticated session using `quickstart.md` alone (SC-001). ✅ **Met** — every command in the quickstart was executed before being documented.

### T405 Final Constitution Check (2026-09-22)

Re-run against `plan.md` → Constitution Check after implementation:

| Gate | Status |
|------|--------|
| §3.2 Docker as approved infrastructure | ✅ Delivered |
| §2.I Clean Architecture | ✅ Changes confined to Infrastructure + composition root; Domain untouched, no inward dependency violated |
| §7.2 Secrets outside the repository | ✅ `.env` ignored (`git check-ignore` confirms), `.env.example` tracked, no password in any tracked file |
| §7.2 Explicit version pinning | ✅ SQL Server `2022-CU27-ubuntu-22.04`; .NET images `10.0`. Rolling-minor caveat recorded in R-001 |
| §7.2 TLS/HSTS in production | ✅ Unchanged; HTTP applies to the development container only |
| §8 Structured logging | ✅ Serilog to stdout; migration target logged with credentials redacted by `DescribeTarget()` |
| Observability — health checks | ✅ `/health` delivered; `architecture.md` updated to record it |
| §12.2 Stateless presentation | ✅ No container-local business state |
| §2.VI Testable time | ✅ Retry delay uses the injected `TimeProvider` |
| §2.VIII Async I/O | ✅ `MigrateAsync`, async health checks, no sync-over-async |
| §14 ADR recorded | ✅ `docs/adr/ADR-001-docker-local-development-environment.md` |
| §14 Documentation updated in the same change | ✅ README, quickstart, `common/architecture.md` |
| §16.1 Git workflow | ✅ Feature branch `docker-implementation`; not `main` |
| §2.VII Test-first discipline | ⚠️ Partial, as declared in plan.md → Complexity Tracking. The behavioral change is unit-tested; infrastructure files are verified by executed procedure |
| §9.3 CI gate | ❌ **Not achievable** — blocked by GAP-005-5 (`Presentation.Tests` red at `HEAD`), not by this feature |
| §12.3 Production operational targets | ➖ Not applicable to a development stack; GAP-005-1 |

**Final build and test state** (inside `mcr.microsoft.com/dotnet/sdk:10.0`):

| Suite | Result |
|-------|--------|
| Build | ✅ 0 warnings, 0 errors |
| `Domain.Tests` | ✅ 63/63 |
| `Application.Tests` | ✅ 41/41 |
| `Presentation.Tests` | ⚠️ pre-existing failures — GAP-005-5 / spec 006 |

### Outstanding at feature close

1. **SC-002 partially satisfied** — five of the required ten consecutive cold starts were executed, all successful (21–29 s).
2. **SC-001 not independently validated** — the quickstart was verified by its author, not by a developer encountering the project for the first time. That check is still worth doing.
3. **GAP-005-1 … GAP-005-5** carried forward; see `spec_005` → Recorded Gaps.

---

## Dependencies

```text
Phase 1 (T101–T107)  →  Phase 2 (T201–T207)  →  Phase 3 (T301–T305)  →  Phase 4 (T401–T405)
```

- T201 MUST precede T202 (build context hygiene).
- T203/T204 MUST precede T205 (Compose reads `.env`).
- T102 MUST precede T105 (the initializer must exist before it is called).
- T401 MUST follow Phase 3 — documented commands must be commands that were actually run.

## Parallelizable

- T106 alongside T103/T104 (different files)
- T203 and T204 together
- T402, T403, T404 together

---

## Verification Environment *(updated 2026-09-22)*

Docker Desktop is installed and verified: Engine 29.8.0, Compose v5.5.1, aarch64 host, 12 CPUs / 7.7 GiB. amd64 emulation was confirmed empirically by running an amd64 container that reported `x86_64` — this is the same mechanism SQL Server 2022 will depend on (R-002).

**The host .NET SDK is 7.0.304 against a `net10.0` target**, so the solution cannot be built on the host at all. All builds and tests run inside `mcr.microsoft.com/dotnet/sdk:10.0`:

```bash
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet build NovaLeave.slnx
```

Consequence: the container is not merely a deployment convenience for this project — it is currently the only way to compile it.

**Known environment caveat**: `docker` is not on the shell `PATH`. The binary lives at `/Applications/Docker.app/Contents/Resources/bin/docker` and the usual `/usr/local/bin/docker` symlink is absent, so a plain `docker` call fails in a fresh shell.
