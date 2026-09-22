# Feature Specification: Containerized Local Development Environment — MVP

**Feature Branch**: `005-docker-containerization`
**Created**: 2026-09-22
**Last Updated**: 2026-09-22
**Status**: Draft — Ready for Implementation
**Constitution Reference**: NovaLeave — Constitution v4.0.0
**Architecture Reference**: `specs/common/architecture.md`

---

## Overview

This specification covers the containerization of the NovaLeave local development environment: a reproducible, single-command stack composed of the ASP.NET Core MVC web application and a SQL Server database, orchestrated with Docker Compose.

Containerization is already an approved infrastructure decision (Constitution §3.2, `common/architecture.md` → *Containerization: Docker*). This spec does not introduce a new technology choice; it implements one that the Constitution already mandates and that no previous specification delivered.

The driving problem is **environment reproducibility**. Today, running NovaLeave requires a developer to independently install the .NET 10 SDK, install or provision SQL Server LocalDB (Windows-only), and manually apply EF Core migrations. This blocks onboarding on non-Windows machines and produces the class of defect the Constitution's CI gate (§9.3) exists to prevent: "works on my machine".

**Out of scope for this spec**: a hardened production image, container orchestration (Kubernetes, Azure Container Apps), a GitHub Actions pipeline that builds or tests inside containers, image registries and image signing, container-level secret management beyond local development, and observability sidecars. Each of these requires its own specification. This spec explicitly establishes the *development* environment only, and Phase 0 research records the forward-compatibility constraints so the production image can be added later without rewriting what is built here.

---

## Key Decisions (Documented)

The following decisions were made with the Product Owner before drafting and are incorporated into this specification:

- **Scope is the development environment only.** A production image is deliberately deferred to a follow-up specification.
- **SQL Server runs as a container.** The stack is fully self-contained; no external database instance is required. Data persists across container restarts via a named Docker volume.
- **Schema is created by automatic migration on startup**, guarded so that it can never run in the `Production` environment. This makes the stack usable with a single command.
- **The `Development` environment keeps its existing seeded test accounts.** The container does not introduce a separate seeding mechanism; it reuses `DevelopmentDataSeeder`.
- **No secret in the repository is a real secret.** The SQL Server password used locally is a development-only credential supplied through an environment file that is excluded from version control, with a committed template (Constitution §7.2: "Secrets and keys are stored outside the repository").

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Developer Starts the Full Stack with One Command (Priority: P1)

As a **developer joining or returning to the project**, I want to start the entire NovaLeave application and its database with a single command, so that I can run the system without installing the .NET SDK or SQL Server on my machine.

**Why this priority**: This is the feature. Every other story in this spec is a refinement of it, and no other story delivers value without it.

**Independent Test**: On a machine with only Docker installed, clone the repository, copy the environment template, run the documented start command, and reach the login page in a browser with a working test account.

**Acceptance Scenarios**:

1. **Given** a machine with Docker Desktop installed and no .NET SDK, **When** the developer runs the documented single start command from the repository root, **Then** the system shall build the application image, start the database, apply all pending EF Core migrations, seed development accounts, and serve the application on the documented local port.
2. **Given** the stack is running, **When** the developer navigates to the application URL in a browser, **Then** the login page shall render with its Bootstrap styles and static assets intact.
3. **Given** the stack is running, **When** the developer authenticates with a seeded development account, **Then** authentication shall succeed and redirect to the role-appropriate dashboard, exactly as it does when running outside a container.
4. **Given** the developer has never run the stack before, **When** the start command is issued, **Then** no manual database creation, manual migration command, or manual user provisioning shall be required.
5. **Given** a developer on an arm64 machine (Apple Silicon), **When** the stack is started, **Then** the database container shall start successfully under platform emulation, and the documentation shall state the prerequisite that enables it.

---

### User Story 2 - Application Waits for a Genuinely Ready Database (Priority: P1)

As a **developer starting the stack**, I want the application to start only once the database is able to accept queries, so that the first run does not fail with a connection error that a second run silently fixes.

**Why this priority**: SQL Server takes appreciably longer to become query-ready than to accept a TCP connection. Without an actual readiness gate, the first `up` on a cold machine fails intermittently — the single most common and most confusing failure in a containerized .NET stack, and the one most likely to make a developer conclude the environment is broken.

**Independent Test**: Remove the database volume to force a cold start, run the start command, and observe the application reaching a healthy state without manual intervention or restart.

**Acceptance Scenarios**:

1. **Given** a cold start with no existing database volume, **When** the stack starts, **Then** the application container shall not attempt to connect until the database container reports healthy.
2. **Given** the database container is still initializing, **When** its health is evaluated, **Then** health shall be determined by a query the database actually answers, not by port availability alone.
3. **Given** a transient connection failure during migration, **When** the application attempts to apply migrations, **Then** the attempt shall be retried within a bounded window before the container is allowed to fail.
4. **Given** the database never becomes healthy within the configured window, **When** that window elapses, **Then** the failure shall surface as a clear, actionable message in the container logs rather than a silent hang.

---

### User Story 3 - Developer Sees Code Changes Without Rebuilding the Image (Priority: P2)

As a **developer actively writing code**, I want my source changes to be reflected in the running container without a full image rebuild, so that the container environment does not make my edit-run cycle slower than working locally.

**Why this priority**: A containerized environment that imposes a multi-minute rebuild on every edit will be abandoned by the team, and the reproducibility benefit of Story 1 will be lost with it. This story is what makes the environment something developers actually keep using.

**Independent Test**: With the stack running, modify a Razor view or a controller, and observe the change take effect in the browser without running a build or rebuild command by hand.

**Acceptance Scenarios**:

1. **Given** the stack is running in development mode, **When** the developer edits a `.cs` file, **Then** the application shall rebuild and restart inside the container automatically, and the change shall be observable in the browser.
2. **Given** the stack is running, **When** the developer edits a `.cshtml` view, **Then** the change shall be observable on the next request.
3. **Given** a rebuild is triggered by a file change, **When** the application restarts, **Then** the database container shall not restart and existing data shall survive.
4. **Given** the developer stops the stack and starts it again, **When** no source file changed, **Then** the start shall reuse cached image layers rather than restoring NuGet packages from scratch.

---

### User Story 4 - Stack State Is Predictable and Resettable (Priority: P2)

As a **developer**, I want explicit, documented control over whether my database data survives a restart, so that I can both keep my working data and deliberately return to a clean state when I need one.

**Why this priority**: Without a documented reset path, developers resort to guesswork against half-migrated databases, and bug reports become unreproducible because no two developers share the same database state.

**Independent Test**: Create data through the UI, restart the stack, confirm the data is still present; then run the documented reset command and confirm the database returns to freshly seeded state.

**Acceptance Scenarios**:

1. **Given** data was created through the application, **When** the stack is stopped and started again, **Then** the data shall still be present.
2. **Given** the developer wants a clean environment, **When** the documented reset command is run, **Then** the database volume shall be removed and the next start shall produce a freshly migrated and freshly seeded database.
3. **Given** the stack is stopped, **When** the developer inspects the repository working tree, **Then** no build output, container state, or local secret shall have been added to version control.

---

### Edge Cases

- **Port already in use**: the documented host port collides with another process. The port must be overridable through configuration without editing a committed file.
- **arm64 host**: the official SQL Server image publishes no arm64 variant. The platform must be pinned explicitly so the failure mode is emulation rather than an unclear "no matching manifest" error.
- **Password policy rejection**: SQL Server refuses to start if the supplied password fails its complexity policy. The template value must satisfy the policy and the requirement must be documented.
- **Stale image after dependency change**: a developer adds a NuGet package; the running container must not silently continue with the previous restore. The rebuild trigger must be documented.
- **HTTPS redirection inside the container**: the application calls `UseHttpsRedirection()`. In a container serving plain HTTP, this must not produce a redirect loop or an unreachable application.
- **Migration applied against a non-empty legacy database**: a developer who previously used LocalDB must not have container migrations applied to their LocalDB instance.
- **Concurrent migration**: if the application container is ever scaled beyond one replica, two instances must not apply migrations simultaneously. Development runs a single replica; the constraint is recorded rather than engineered around.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a container image definition for the ASP.NET Core web application, built from official Microsoft .NET 10 base images.
- **FR-002**: The system MUST provide a Docker Compose definition that orchestrates the web application and a SQL Server database as a single stack.
- **FR-003**: The stack MUST start with a single documented command from the repository root, requiring no locally installed .NET SDK or SQL Server.
- **FR-004**: The application container MUST NOT begin serving traffic until the database container reports a healthy state determined by an executed query.
- **FR-005**: The system MUST apply all pending EF Core migrations automatically on startup when the environment is not `Production`.
- **FR-006**: Automatic migration MUST be impossible to trigger in the `Production` environment, enforced in code rather than by configuration convention alone.
- **FR-007**: Migration on startup MUST retry on transient database failures within a bounded window and MUST fail loudly with an actionable log message when that window is exhausted.
- **FR-008**: The existing `DevelopmentDataSeeder` MUST run after migrations complete, preserving the current seeded accounts unchanged.
- **FR-009**: Database data MUST persist across container stop and start via a named Docker volume.
- **FR-010**: The system MUST provide a documented command that discards the database volume and returns the stack to a freshly migrated, freshly seeded state.
- **FR-011**: The database password MUST be supplied through an environment file that is excluded from version control, accompanied by a committed template file with a development-only placeholder value.
- **FR-012**: The host port serving the application MUST be overridable through environment configuration without modifying a version-controlled file.
- **FR-013**: The build context MUST exclude build output, version-control metadata, local environment files, and IDE state via a `.dockerignore` file.
- **FR-014**: The application image MUST be built in discrete stages so that NuGet restore is cached independently of source compilation.
- **FR-015**: In development mode, source changes MUST propagate to the running container without a manual image rebuild.
- **FR-016**: The database container image MUST be pinned to an explicit version tag, never a floating `latest` tag (Constitution §7.2, version pinning).
- **FR-017**: The database container platform MUST be pinned explicitly so that arm64 hosts resolve the image deterministically.
- **FR-018**: The application MUST expose a health endpoint that reports whether it can reach the database, so that container health is observable rather than inferred.
- **FR-019**: HTTPS redirection MUST NOT prevent the application from being reachable over plain HTTP inside the development container.
- **FR-020**: Documentation MUST record prerequisites, the start command, the reset command, seeded credentials, the connection details for attaching a database client, and the arm64 emulation prerequisite.
- **FR-021**: The containerized application MUST log to standard output in the structured Serilog format already configured, so that `docker compose logs` is the single observability surface (Constitution §8).
- **FR-022**: The containerized environment MUST NOT alter application behavior relative to running outside a container; no business rule, authorization check, or validation may be conditioned on running in a container.

### Non-Functional Requirements

- **NFR-001**: A cold start on a machine with the base images already pulled SHOULD reach a serving application in under 3 minutes.
- **NFR-002**: A warm start with no source changes SHOULD reach a serving application in under 45 seconds.
- **NFR-003**: An incremental rebuild triggered by a single source file change SHOULD complete in under 30 seconds.
- **NFR-004**: The stack MUST function on arm64 (Apple Silicon) and amd64 hosts.

### Key Entities

This feature introduces no domain entities and modifies no existing entity. It alters composition-root startup behavior and adds infrastructure definition files only.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer with only Docker installed reaches an authenticated session in the browser, on a clean clone, in under 10 minutes of wall-clock time and with no step outside the documented quickstart.
- **SC-002**: Ten consecutive cold starts (volume removed each time) succeed without manual intervention — no restart, no retry, no manual migration.
- **SC-003**: The application behaves identically inside and outside the container: the existing test suite passes unchanged, with no test conditioned on container detection.
- **SC-004**: No credential, build output, or local environment file is added to version control by this feature.
- **SC-005**: Attempting to start the stack with `ASPNETCORE_ENVIRONMENT=Production` does not apply migrations automatically.

---

## Assumptions

- Docker Desktop (or an equivalent providing Docker Engine and Compose v2) is installed by the developer; provisioning it is outside the system's control.
- On Apple Silicon, Rosetta-based amd64 emulation is available and enabled in Docker Desktop. This is a documented prerequisite, not something the stack can enforce.
- The development database is disposable. No backup, restore, or retention requirement from Constitution §12.3 or §13 applies to it; those targets apply to production and remain recorded as gaps.
- Developers who currently use LocalDB may continue to do so. The container stack is additive and does not remove the existing LocalDB configuration.
- The seeded development credentials remain development-only and are never valid in any deployed environment.

---

## Constitution Compliance Notes

| Rule | Relevance | Compliance |
|------|-----------|------------|
| §3.2 Approved Stack — *Infrastructure: Docker* | Directly implements an approved, previously undelivered decision | Implements |
| §7.2 Secrets outside the repository | Local database password | Env file excluded from VCS; template committed |
| §7.2 Explicit version pinning | Base and database images | All image tags pinned |
| §8 Observability | Container logging | Serilog to stdout, unchanged format |
| §12.2 Stateless presentation | Horizontal scaling readiness | No container-local business state introduced |
| §14 ADR required | Containerization approach | ADR authored with this feature |
| §16.1 Git workflow | Branch and commits | Feature branch, Conventional Commits |

### Recorded Deviations

None. This feature implements an approved decision within its existing constitutional bounds.

### Recorded Gaps (carried forward)

- **GAP-005-1**: No production container image exists. Constitution §12.3 operational targets (availability, RTO, RPO, backups) remain unaddressed and are deferred to the production containerization specification.
- **GAP-005-2**: No CI pipeline builds or tests the image. The Constitution §9.3 CI gate is not yet exercised against the container.
- **GAP-005-3**: Automatic migration on startup is acceptable for a single-replica development stack only. A production migration and rollback strategy (Constitution §16.2) must be defined before any deployed environment uses this mechanism.
- **GAP-005-4** *(found during implementation)*: EF Core connection resiliency (`EnableRetryOnFailure`) cannot be enabled while `VacationRequestRepository.ExistsOverlappingAsync` uses a user-initiated `Serializable` transaction. Adopting it requires migrating that call to an explicit execution strategy, with concurrency tests. See `research.md` → R-010.
- **GAP-005-5** *(found during implementation)*: `NovaLeave.Presentation.Tests` fails 24–26 of 49 tests at `HEAD`, non-deterministically and independently of this feature. Two root causes, both in test code, neither in the application: (a) `Program.cs` uses a single fixed InMemory database name, which EF Core backs with a process-wide static root, so `AuthTestFixture` and `VacationTestFixture` share one store; (b) the fixtures seed `Employee` and `Admin` roles, while the application uses `User` and `Approver` — and Constitution §4 permits exactly those two. Constitution §9.3's CI gate cannot be green until this is fixed. Deferred to spec 006 by PO decision on 2026-09-22.
