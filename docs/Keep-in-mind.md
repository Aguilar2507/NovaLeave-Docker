# Keep in mind

**Last updated**: 2026-09-24
**Context**: compiled at the close of `spec_005` (Docker), updated at the close of `spec_007` (metrics), `spec_008` (seed data), `spec_009` (logs), `spec_010` (AI agents) and `spec_011` (dev containers)

Open work, known limitations, and decisions worth not re-litigating. Read this before picking up a task.

---

## How this was compiled, and what it does not cover

This is the result of a deliberate sweep, not just notes from the Docker work:

- every unchecked task in `specs/001` and `specs/004` (**61 total**);
- `grep` for `TODO` / `FIXME` / `NotImplementedException` in `src/` and `tests/` — **none found**, the code carries no inline debt markers;
- every `[Fact(Skip = ...)]` in the test suite;
- Constitution requirements cross-checked against what exists on disk;
- live inspection of the running application's HTTP responses;
- the open items in `spec-pending-clarifications.md`.

**What this does not cover**: nobody has audited whether the *implemented* business rules match the specifications. Every item below is something demonstrably missing, skipped, or undecided — not a claim that what exists is correct. A functional review against `spec_001` acceptance scenarios has never been recorded.

**Important caveat on the checkbox counts**: some unchecked tasks are **stale, not undone**. T513 (history views) is marked unchecked, but `Views/EmployeeRequests/Index.cshtml` and `Detail.cshtml` both exist. Everything called out below was verified on disk individually — but the 61 figure itself needs triage before it is used for planning.

---

## Legend

| Mark | Meaning |
|------|---------|
| 🔴 | Blocking — pick these up first |
| 🟠 | Business functionality that is missing |
| 🔒 | Security finding |
| 🟡 | Quality or verification debt |
| ⚪ | Decision already made — read before changing |
| ❓ | Needs a Product Owner answer |

---

# 🔴 Blocking

## 1. `NovaLeave.Presentation.Tests` is red — and was before Docker

**24–26 of 49 tests fail, non-deterministically, on a clean `main`.** Counts vary between identical runs.

Two causes, **both in test code, neither in the application**:

- **One shared in-memory database.** `Program.cs` uses a fixed name, `UseInMemoryDatabase("NovaLeaveTestDb")`. EF Core backs that with a process-wide static root, so `AuthTestFixture` and `VacationTestFixture` write into the same store and corrupt each other. Symptoms: `Sequence contains more than one element`, `Failed to create test admin`.
- **The fixtures use roles that do not exist.** The app uses `User` and `Approver`. The fixtures seed `Employee`, `Approver` and `Admin` — and Constitution §4 permits exactly two roles, with no admin role. Symptom: `Role EMPLOYEE does not exist.`

**Why it blocks**: Constitution §9.3's CI gate cannot pass, and the main defense against regressions in authentication and vacation-request flows is inert.

Full diagnosis, requirements and success criteria: [spec 006](../.specify/specs/006-integration-test-isolation/spec_006-integration-test-isolation.md). Not scheduled; needs a branch and an owner.

---

# 🟠 Missing business functionality

## 2. Auto-expiry does not exist — pending requests never expire

**The most significant gap in the project.**

`VacationRequest.Expire()` exists in the domain and has unit tests. But:

- `grep` confirms **`.Expire()` is never invoked anywhere in `src/`**;
- `src/NovaLeave.Infrastructure/BackgroundJobs/` **does not exist**;
- no `IHostedService` is registered in `Program.cs`;
- there is no `Expiry` folder under `Application/Features/VacationRequest/` (there are folders for Approve, Cancel, Create, Edit, List, Reject, Void — but not expiry);
- `EXPIRY_DAYS` appears in `architecture.md`'s configuration table but is bound nowhere.

So a `Pending` request stays `Pending` forever. The state machine supports expiry; nothing triggers it.

This matters beyond a missing feature: `spec-pending-clarifications.md` records that the PO **replaced auto-escalation with auto-expiry**. Auto-expiry is the mechanism that stops requests stalling on an unresponsive approver — and it is not there.

*Spec 001 tasks T611 (test), T612 (`AutoExpiryJob`), T613 (registration), T614 (E2E), T647 (config robustness) — all unchecked, all genuinely undone.*

## 2b. Observability is half delivered — metrics yes, tracing no

`spec_007` delivered metrics: OpenTelemetry → Prometheus → Grafana, with a provisioned dashboard. **Distributed tracing is still missing**, so `architecture.md`'s *"Metrics and tracing (baseline, not optional)"* is only half met.

Tracing reuses the same OpenTelemetry setup — add tracing instrumentation and an OTLP exporter. **Do not introduce a second framework.** *(GAP-007-1)*

| ID | Gap from spec_007 |
|----|-------------------|
| GAP-007-2 | **No alerting.** A stalled approval queue is visible only if somebody looks at the dashboard. Build alerts on `novaleave_vacation_request_oldest_pending_age_seconds` — it is the metric that detects a stalled process |
| GAP-007-3 | Prometheus retention and storage sizing unconfigured; a deployed instance needs both |
| GAP-007-4 | §12.1 latency targets are now **measurable but not verified** — and cannot be verified on this stack, because SQL Server runs emulated on arm64 (see item 18) |
| GAP-007-5 | Approval-duration histogram deferred. Needs either a widened `IStateTransitionAuditService` or an extra database read per transition |
| GAP-007-6 | **Prometheus is published on port 9090 with no authentication.** It holds every scraped series, so the metric data is readable from the host even though `/metrics` is not. Fine on a localhost dev machine; unacceptable deployed, where Grafana should be the only exposed entry point. Deleting the `ports:` block from the `prometheus` service gives the stricter posture locally |

**Two things these metrics make visible that nothing else did:**

- `novaleave_vacation_request_oldest_pending_age_seconds` will climb without bound, because auto-expiry does not exist (item 2 above). The dashboard now shows that happening instead of it being silent.
- `microsoft_entityframeworkcore_optimistic_concurrency_failures_total` is currently the **only** live signal for an invariant whose tests are skipped (item 8 below).

## 2d. Logs are aggregated — with a Docker-socket caveat

`spec_009` delivered log aggregation: the app emits CLEF JSON, Grafana Alloy ships every container's stdout to Loki, and Grafana has a provisioned Loki datasource plus a logs panel. Correlating a metric spike with the exact request's logs now works:

```logql
{service="web"} | json | CorrelationId=`<id>`
```

| ID | Gap from spec_009 |
|----|-------------------|
| **GAP-009-1** 🔒 | **Alloy mounts `/var/run/docker.sock`.** That is effectively root on the Docker daemon — it can start privileged containers and mount the host filesystem. `:ro` restricts writes to the socket *file*, not the API, so it is **not** a real boundary. Accepted locally in exchange for capturing crash-time logs an in-process sink would miss. A deployed environment must ship logs another way |
| **GAP-009-2** 🔒 | Loki is published on 3100 **with no authentication**, same posture as Prometheus (GAP-007-6). Anyone reaching localhost can read every log line |
| GAP-009-3 | No log-based alerting; Loki's ruler is unconfigured |
| GAP-009-4 | Retention is a flat 7 days with no size cap |
| **GAP-009-5** 🔒 | Request **reasons** are Sensitive/PII (§7.3). Nothing logs them today, but **no test or lint prevents someone adding one** — and logs are now centrally stored and queryable, which raises the cost of that mistake |

**Field-name gotcha**: LogQL's `| json` sanitises CLEF's `@`-prefixed keys — `@l`→`_l`, `@t`→`_t`, `@mt`→`_mt`. Information-level lines have no `_l` at all (CLEF omits it). Enriched properties keep their names.

## 2e. AI agent teams — delivered, with docker-agent caveats

`spec_010` added two optional Docker Agent teams (development tooling, not application code): an **ops team** that reads the running stack (`docker compose run --rm ops-agent` / `ops-agent-local`) and a **dev team** that plans, reviews and tests in a Docker Sandbox (`docker/agents/dev-team.sh`). Cloud model is Gemini; the ops team also runs on a local Model Runner model. See [ADR-003](adr/ADR-003-docker-agents.md).

| ID | Gap from spec_010 |
|----|-------------------|
| **GAP-010-7** ⚪ | **Always start the dev team with `docker/agents/dev-team.sh`.** `docker agent run docker/agents/dev-team.yaml` works but is **not sandboxed** — docker-agent 1.140 breaks on `runtime.sandbox: true`, so the launcher passes `--sandbox` instead. Restore the YAML setting when a fixed release ships |
| GAP-010-8 | The sandboxed dev team **cannot use the local model**: docker-agent forwards `--flavor local` as `[local]` and silently falls back to Gemini. The launcher refuses `--flavor` for that reason |
| GAP-010-9 | The **Gemini free tier** returns `HTTP 429` after a few multi-agent questions. A billed key is needed for regular use |
| **GAP-010-2** 🔒 | `ops-agent` mounts the Docker socket, same exposure as Alloy (GAP-009-1). Bounded by having no shell — three fixed read-only commands only |
| **GAP-010-3** 🔒 | With Gemini, **everything the agents read is sent to Google**, and on the free tier may be used to improve its products. Seeded data only — never point the agents at real data |
| GAP-010-5 | No automated evaluation of agent behavior (`docker agent eval`) |
| GAP-010-6 | Agent prompts embed metric names and test baselines — **update `docker/agents/*.yaml` when the metrics catalogue or the Presentation.Tests baseline changes** |
| T309 | Still to verify: a permitted sandbox write under `tests/` reaching the host, and `dotnet_test` inside the sandbox (blocked by GAP-010-9 on 2026-09-23) |

**Per-machine setup — not in the repository.** Each developer runs once: `sbx login`, `sbx policy init balanced`, and `sbx secret set google --command "grep '^GOOGLE_API_KEY=' $PWD/.env | cut -d= -f2-"`. Without the stored secret, Gemini answers **HTTP 400** from inside the sandbox — it looks like a config error but is a missing credential.

**Script-tool gotcha**: docker-agent reads every `$NAME` or `${...}` in a script tool's `cmd` as a tool argument and **silently drops the whole toolset** if one is undeclared — `$PWD` and `${x:-default}` included. Run `docker agent debug toolsets <file>` after every edit to a team file.

## 2f. Dev container — DevPod upstream is unmaintained

`spec_011` added `.devcontainer/`: a `workspace` container with the .NET 10 SDK that joins the running stack, launched with DevPod (`devpod up . --ide vscode`) or any dev-container tool. See [ADR-004](adr/ADR-004-devpod-dev-containers.md).

| ID | Gap from spec_011 |
|----|-------------------|
| **GAP-011-1** ⚪ | **Keep `devcontainer.json` to standard keys only.** DevPod upstream has been unmaintained since 2025; the pinned community fork (v0.26.1) has one maintainer moving to a successor. The standard file is what survives if DevPod does not |
| GAP-011-2 | macOS Docker Desktop: DevPod needs `DOCKER_HOST=unix://$HOME/.docker/run/docker.sock` (or Docker Desktop's default-socket setting) — it swaps `DOCKER_CONFIG`, hiding the Desktop context |
| **GAP-011-3** ⚪ | **Do not let the dev-container tool start the stack.** DevPod names Compose projects itself: it either reuses the running project without the workspace, or starts a second stack whose ports collide. The workspace joins `novaleave_default`; `initializeCommand` starts the stack |
| GAP-011-4 | SQL Server still emulated on Apple Silicon; DevPod SSH/cloud providers could fix it — not configured |
| GAP-011-5 | Playwright browsers not in the image — E2E cannot run in the workspace |
| T404 | VS Code attach (IntelliSense on .NET 10, breakpoint) awaiting user confirmation |

## 2c. Approving a request charges the employee TWICE

**Found 2026-09-23 while building seed data. This is a live business-logic defect.**

- `CreateRequestHandler.cs:76` calls `employee.ReserveDays(workingDays)`
- `ApproveRequestHandler.cs:41` then calls `employee.DeductDays(request.RequestedDays)`
- `Employee.ReserveDays` and `Employee.DeductDays` are **identical**: both do `Balance -= days`

There is no restore between them. So an employee with 15 days who submits a 3-day request and has it approved ends with **9 days, not 12**. The days are deducted on creation and again on approval.

Reject / Cancel / Void each restore once, which correctly undoes a single reservation — so only the approval path is wrong.

**Likely intended design**: `ReserveDays` holds days while Pending; approval converts the hold into a permanent deduction without charging again. Either `ApproveRequestHandler` should not deduct, or `Approve` should restore-then-deduct.

**Why it has gone unnoticed**: no test covers the balance across the full create→approve sequence, and the approver UI does not display the requester's remaining balance.

**Note**: `docs/../spec_008` seed data deliberately does NOT reproduce this — seeded balances are internally consistent (`Balance + held = initial`). Fixing the bug will not require re-seeding. *(GAP-008-1)*

## 3. Audit immutability is not enforced by the database

Constitution §6 and the `AuditRecord` entity both require audit records to be immutable — no updates, no deletes. That invariant currently lives **only in application code**.

Spec 001 T640 calls for a migration named `ProtectAuditRecordImmutability` adding a database-level guard. Only two migrations exist (`InitialCreate`, `AddVacationRequest`); it was never written. Anything with a database connection can currently rewrite audit history.

## 4. Audit history cannot be viewed

`ListAuditQuery`, `ListAuditQueryHandler` and the `AuditHistory` action/view (spec 001 T641, T642) do not exist — verified by grep across `src/`. Audit records are written but there is no way to read them through the application.

Related and also missing: T643 (retention test asserting 7-year persistence, Constitution §13) and T644 (PII masking test asserting `Reason` is not leaked into audit `Details`, §7.3).

## 5. Confirmation modal missing for destructive actions

`Views/Shared/_ConfirmationModal.cshtml` does not exist, though spec 001 T312 requires it for cancellation and the PO confirmed *"explicit confirmation is required for all final actions"* (`spec-pending-clarifications.md`, P13).

Cancel, void and reject are irreversible state transitions. Verify whether confirmation is implemented some other way before assuming it is simply absent.

---

# 🔒 Security findings

All pre-existing. None introduced by the Docker work. All verified against the running application.

## 6. CDN scripts with no Subresource Integrity

`Views/Shared/_ValidationScriptsPartial.cshtml` loads three scripts from `cdn.jsdelivr.net` — jQuery 3.6.0, jquery-validation 1.19.5, jquery-validation-unobtrusive 4.0.0 — with **no `integrity=` attribute**.

Constitution §7.2: *"A CDN MAY be used only with approved CSP configuration and Subresource Integrity where supported."* A compromised CDN response executes in every user's browser.

**Spec 004's own checklist already contains the line "No CDN references in any `.cshtml` file — all assets served from `wwwroot/lib/` or `wwwroot/css/`", unchecked.** This is a known, accepted-and-forgotten requirement, not a new discovery. Bootstrap is already hosted locally; these three were missed.

## 7. No security headers are sent

Verified against live responses: no `Content-Security-Policy`, no `X-Content-Type-Options`, no framing protection, no `Referrer-Policy`. Constitution §7.2 requires all four in production.

Not exploitable today — nothing is deployed — but it must exist before anything is. It also interacts with item 6: a strict CSP would block those three CDN scripts, so the CDN decision must come first.

## 8. Concurrency protection is untested

Two tests in `ConcurrencyTests.cs` are **skipped**:

- `[Fact(Skip = "Integration concurrency test - requires multi-threaded scenario with real DB")]`
- `[Fact(Skip = "Optimistic concurrency test - requires real RowVersion tracking")]`

Plus spec 001 T404 (`DoubleVoid_OnlyOneSucceeds`) is unchecked.

These cover the `Serializable` transaction that prevents overlapping requests and the `RowVersion` optimistic concurrency on state transitions — two of the project's most important invariants (Constitution §6). They are skipped precisely because the InMemory provider cannot exercise them, and **the containerized SQL Server now available makes them runnable for the first time.**

## 9. Authorization/IDOR test coverage is incomplete

Spec 001 T405 (`VoidIDOR` — non-owner attempts to void another employee's request) and T622 (over-posting and session revalidation) are unchecked. Constitution §7.4 names OWASP A01 (broken access control) as a primary baseline.

---

# 🟡 Quality and verification debt

## 10. The E2E suite is effectively non-functional

`tests/NovaLeave.E2E.Tests/` contains two test files. `SubmitRequestE2ETests.cs` is a 24-line stub, skipped with `"requires browser install: playwright.ps1 install"`. Playwright browsers have never been installed or run in this environment.

Spec 001 lists eight unwritten E2E tests (T206, T304, T403, T502, T601b, T614, T646). Spec 004 lists four E2E acceptance checks. Constitution §3.2 mandates *"Playwright or an approved equivalent for critical browser journeys."*

## 11. Accessibility has never been verified

Spec 004 leaves all of these unchecked: responsive behavior at 360/390/768/1024/1440 px, keyboard navigation and focus indicators, WCAG AA contrast, axe-core scan with 0 A/AA violations, `prefers-reduced-motion` support, touch targets ≥ 44 px.

Constitution §11.5 requires accessibility; `spec_002` defines the design system these checks belong to.

## 12. Performance targets have never been measured

Constitution §12.1 sets p95 < 300 ms for focused operations and < 500 ms for standard MVC pages, and §12.1 also requires *"RPS and concurrency targets MUST be documented for each production release."* Spec 004 leaves the `GET`/`POST /Account/Login` measurements unchecked. No load test exists.

**Do not measure on the Docker stack** — see item 18.

## 13. Code coverage has never been measured

Constitution §9.2 requires ≥ 80% line coverage for Domain and Application, ≥ 60% measured for Infrastructure and Presentation. Spec 001 T706 is unchecked; no coverage run is recorded anywhere.

## 14. Rate limiting is disabled in tests and therefore unverified

`Program.cs` skips rate limiting when the environment is `Testing`, so spec 004's rate-limit tasks (T031, plus two acceptance checks) are unchecked with the note *"rate limiting deshabilitado en Testing"*. Constitution §7.2 requires rate limiting on sensitive endpoints, and FR-017 specifies it — the behavior exists but nothing proves it works.

## 15. `NovaLeave.Infrastructure.Tests` does not exist

`specs/common/architecture.md` lists it in the project structure. The directory is absent; there are four test projects, not five. Either create it or correct the architecture document.

## 16. Constitution-required directories are missing

| Path | Required by | Status |
|------|-------------|--------|
| `docs/runbooks/` | §12.3 — *"Runbooks: stored under `docs/runbooks/` and linked from critical alerts"* | Missing |
| `docs/diagrams/` or `.specify/diagrams/` | §14 — entity/workflow diagrams as code (Mermaid) | Missing |

`docs/adr/` now exists, created by `spec_005`.

## 17. Documentation language is inconsistent with the Constitution

Constitution §3.3 says documentation should be **Spanish**. Every artifact under `.specify/specs/` — including spec 001, spec 004 and everything `spec_005` added — is in **English**. The application UI is correctly in Spanish.

The Constitution and the repository disagree. Somebody should decide which convention actually holds.

---

# ⚪ Decisions already made — read before changing

## 18. Do not benchmark on the Docker stack

SQL Server publishes no arm64 image and runs under Rosetta emulation on Apple Silicon — measurably slower than native. Constitution §12.1 measurements taken here would be meaningless. This is why item 12 remains open rather than being closed with numbers from this environment.

## 19. Do not enable `EnableRetryOnFailure` on the SQL Server provider

It looks like an obvious improvement. It would break the application.

`VacationRequestRepository.ExistsOverlappingAsync` (line 52) opens a **user-initiated `Serializable` transaction** — spec 001's deliberate fix for the overlap race (D-003). EF Core throws `InvalidOperationException: The configured execution strategy 'SqlServerRetryingExecutionStrategy' does not support user-initiated transactions` when a retrying strategy meets one.

The failure appears **only against SQL Server**, never against the InMemory provider the tests use — so the suite would stay green while request creation was broken in production.

Prerequisite for ever adopting it: migrate that call to `CreateExecutionStrategy().ExecuteAsync(...)`, with concurrency tests that actually run against SQL Server (see item 8).

*Full analysis: `research.md` → R-010. Tracked as GAP-005-4.*

## 20. Do not add an `appsettings.Docker.json`

Planned as T206 and deliberately rejected. Such a file is only read when `ASPNETCORE_ENVIRONMENT=Docker`, and that name makes `IsDevelopment()` return **false** — disabling seeded accounts, enabling HSTS and the production exception handler, and turning off detailed errors. The opposite of what a development stack needs.

The container stays on `Development` and takes its connection string from the `ConnectionStrings__DefaultConnection` environment variable, which outranks `appsettings.Development.json` and keeps the password out of every tracked file.

## 21. Automatic migration on startup is development-only, and guarded in code

`DatabaseInitializer` refuses to migrate when the environment is `Production` — enforced in code so a mistyped environment variable cannot enable it, and covered by unit tests.

Before any deployed environment exists, a reviewed, reversible migration step must be defined (Constitution §16.2 requires a documented migration and rollback strategy). Also note that with more than one replica, two instances could migrate concurrently; development runs one.

*Tracked as GAP-005-3.*

## 22. The container is currently the only way to build this project

The solution targets `net10.0`; the host SDK on at least one developer machine is 7.0.304, so `dotnet build` fails on the host regardless of Docker.

```bash
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet build NovaLeave.slnx
```

`dotnet test` accepts only one project per invocation — loop over the test projects.

## 23. Minor housekeeping

- **Duplicate DI registration**: `AddSingleton<TimeProvider>(TimeProvider.System)` appears twice in `Program.cs`, lines 70 and 100. Harmless — same value, last wins — but a one-line deletion and a sign two changes landed without seeing each other.
- **Table naming**: Constitution §3.3 says table names should be singular; the database has `Employees`, `VacationRequests`, `AuditRecords`. The convention *is* applied consistently, so this likely needs documenting as an accepted deviation rather than a migration.

---

# ❓ Open questions for the Product Owner

Carried in [`spec-pending-clarifications.md`](../.specify/specs/spec-pending-clarifications.md) → *Remaining Open Items*, unanswered:

| Topic | Question |
|-------|----------|
| Employee deactivation (FR-028) | What happens to `Pending` and `Approved` requests when an employee is deactivated — auto-cancelled, or left with a note? |
| Approver reassignment (FR-029) | If an approver changes, are pending requests re-routed to the new approver or left with the original? |
| Balance recalculation on voiding | Any special cases when days are returned and the employee has since accrued more? |
| Holiday calendar management | Who maintains the public holiday calendar — an HR UI, or a configuration file? |
| HR read-only views (FR-009/FR-010) | When the deferred HR story is built, which users may see org-wide balances, and should HR see the request `Reason`? |

Plus, raised by this work:

| Topic | Question |
|-------|----------|
| Documentation language | Spanish per Constitution §3.3, or English as the repository actually is? (item 17) |
| Test role model | Are `Employee`/`Admin` in the test fixtures leftovers from a role model the Constitution later narrowed to `User`/`Approver`? (item 1) |
| Table naming | Accept plural as a documented deviation, or migrate to singular? (item 23) |

---

# 📋 What `spec_005` changed in existing code

So nothing surprises you in review:

| File | Change |
|------|--------|
| `Program.cs` | Health checks registered, `/health` mapped before authentication; inline development seeding replaced by a `DatabaseInitializer` call; DI registrations for initializer, migrator and options |
| `DevelopmentDataSeeder.cs` | Now implements the new `IDevelopmentDataSeeder` — interface extraction only, no behavior change |
| `NovaLeave.Infrastructure.csproj` | Added `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` |
| `.gitignore` | Added `.env` |
| `README.md` | Replaced a one-line placeholder |
| `.specify/specs/common/architecture.md` | Docker and health checks marked delivered |

New code in `src/NovaLeave.Infrastructure/Persistence/` (`DatabaseInitializer`, `IDatabaseMigrator`, `EfCoreDatabaseMigrator`, `DatabaseInitializerOptions`), tests in `tests/NovaLeave.Application.Tests/DatabaseInitializerTests.cs`.

**No domain logic, authorization rule or business behavior was changed.**

### Verification still owed on `spec_005`

- **SC-002**: requires ten consecutive cold starts. **Five** were run, all successful (21–29 s).
- **SC-001**: the quickstart was verified by its author, who already knew the answers. **The next person to set up the project should follow [quickstart.md](../.specify/specs/005-docker-containerization/quickstart.md) verbatim and report where it fails.**
- **GAP-005-1**: no production image; §12.3 targets (availability, RTO, RPO, backups) unaddressed.
- **GAP-005-2**: no CI builds or tests the image.

---

## Where the full detail lives

| Document | What it holds |
|----------|---------------|
| [spec_001 tasks](../.specify/specs/001-vacation-request/tasks.md) | 36 unchecked tasks — needs triage for staleness |
| [spec_004 tasks](../.specify/specs/004-initial-setup-and-authentication/tasks.md) | 25 unchecked tasks and acceptance checks |
| [spec-pending-clarifications](../.specify/specs/spec-pending-clarifications.md) | Resolved PO decisions and the open questions above |
| [spec_005](../.specify/specs/005-docker-containerization/spec_005-docker-containerization.md) | Docker requirements, success criteria, GAP-005-* |
| [research.md](../.specify/specs/005-docker-containerization/research.md) | R-001…R-010 — decisions with rejected options |
| [ADR-001](adr/ADR-001-docker-local-development-environment.md) | The containerization decision in reviewable form |
| [spec 006](../.specify/specs/006-integration-test-isolation/spec_006-integration-test-isolation.md) | Test suite diagnosis |

---

## Suggested order of attack

1. **Item 1** — fix the test suite. Nothing else can be verified properly until the safety net works, and several items below are blocked on it.
2. **Item 2** — implement auto-expiry. It is the largest missing piece of business behavior, and the PO chose it over escalation.
3. **Item 6** — SRI or local hosting for the CDN scripts. Small change, real supply-chain exposure, and already an unmet requirement of spec 004.
4. **Item 8** — unskip the concurrency tests. The containerized SQL Server makes them runnable for the first time.
5. **Items 3 and 4** — audit immutability and audit history. Compliance-relevant (§6, §13).
6. **Item 7** — security headers, once the CDN question is settled.
7. **Triage the 61 unchecked tasks** — separate stale checkboxes from real work before planning anything from that number.
8. **Items 10–14** — E2E, accessibility, performance, coverage, rate limiting. The whole verification layer.
9. **Items 15, 16, 17, 23** — housekeeping and conventions.
