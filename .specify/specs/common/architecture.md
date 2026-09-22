# NovaLeave — Shared Architecture & Technical Context

**Version:** 1.0.0  
**Constitution Reference:** NovaLeave — Constitution v4.0.0, §2–§3  
**Last Updated:** 2026-07-28

---

## Overview

This document defines the shared architectural decisions and technical context for all NovaLeave features. Feature plans reference this document for technology stack, project structure, and cross-cutting concerns.

---

## Technology Stack

| Concern | Decision |
|---------|----------|
| **Runtime** | .NET 10, C# (latest) |
| **Presentation** | ASP.NET Core MVC with Controllers + Razor Views |
| **UI Framework** | Bootstrap 5.3 (version-pinned, locally hosted) |
| **Authentication** | ASP.NET Core Identity with secure cookie authentication |
| **Authorization** | Policy-based + resource-based authorization |
| **ORM** | Entity Framework Core |
| **Database** | SQL Server |
| **Validation** | FluentValidation (input); Domain (business rules) |
| **Logging** | Serilog (structured) |
| **Testing** | xUnit, WebApplicationFactory, Playwright (E2E) |
| **Containerization** | Docker — delivered for local development by [`005-docker-containerization`](../005-docker-containerization/plan.md); see [ADR-001](../../../docs/adr/ADR-001-docker-local-development-environment.md). A production image is not yet built (GAP-005-1) |
| **CI/CD** | GitHub Actions |
| **Deployment** | Azure-ready |

---

## Clean Architecture Layers

```text
src/
  NovaLeave.Domain/           → Entities, value objects, aggregates, invariants
  NovaLeave.Application/      → Use cases, contracts, input validation, orchestration
  NovaLeave.Infrastructure/   → EF Core, SQL Server, repositories, external services
  NovaLeave.Presentation.Web/ → Controllers, Views, ViewModels, Filters, TagHelpers, composition root

tests/
  NovaLeave.Domain.Tests/
  NovaLeave.Application.Tests/
  NovaLeave.Infrastructure.Tests/
  NovaLeave.Presentation.Tests/    → Integration + E2E
```

**Dependency Rules (Constitution §2.I):**
- Dependencies point inward: Presentation → Application → Domain
- Infrastructure implements Application contracts
- Domain has ZERO outward dependencies
- Controllers/Views MUST NOT access `DbContext` or Infrastructure directly
- `Program.cs` is the sole composition root exception

---

## Cross-Cutting Concerns

### Observability
- Structured logging with Serilog (correlation IDs, request IDs)
- Health checks — `/health` delivered by `005-docker-containerization`, including an EF Core `DbContext` check
- Metrics and tracing (baseline, not optional) — **not yet delivered by any feature**

### Time Abstraction
- All time-dependent logic uses `TimeProvider` (Constitution §2.VI)
- No direct `DateTime.Now` or `DateTime.UtcNow` in Domain/Application

### Async by Default
- All I/O uses `async`/`await` end-to-end
- `.Result`, `.Wait()`, sync-over-async prohibited (Constitution §2.VIII)

### Concurrency
- Optimistic concurrency via `RowVersion` on mutable entities
- Database-level constraints for critical invariants (overlap prevention)

### CQRS (Preferred, Not Mandatory)
- Commands and queries separated where beneficial
- MediatR optional; application services equally valid
- Organized by feature/vertical slice in `Application/`

---

## Security Baseline

- OWASP Top 10:2025 primary baseline: A01, A06, A09
- Global anti-forgery validation (AutoValidateAntiforgeryToken)
- Post/Redirect/Get for all successful form submissions
- Server-authoritative validation (never trust client)
- TLS 1.2+, HSTS in production
- CSP-compatible static assets

See [`specs/common/security.md`](./security.md) for detailed security requirements.

---

## Testing Strategy

| Level | Scope | Framework |
|-------|-------|-----------|
| Unit | Domain invariants, value objects | xUnit |
| Unit | Application use cases (mocked infra) | xUnit + Moq/NSubstitute |
| Integration | Full stack via WebApplicationFactory | xUnit |
| E2E | Browser flows | Playwright |
| Security | CSRF, forced browsing, cookie expiry, privilege escalation | Integration tests |

**Test-First Discipline (Constitution §2.VII):**
- Acceptance criteria defined before implementation
- Every domain invariant has positive + negative tests
- Critical workflows have integration tests
- No merge without corresponding tests for critical changes

**Code Coverage (Constitution §9.2):**
- Line-coverage target for Domain and Application modules: **>= 80%**
- Infrastructure and Presentation: **>= 60%** (measured, not gated)
- Coverage does not replace the obligation to test every invariant and rejection scenario
- Coverage is measured in CI via `dotnet-coverage` or `coverlet` and reported per-project
- Untested critical paths (state transitions, balance operations, authorization) are blocking regardless of overall percentage

---

## Code Documentation Standards

All production code generated during task implementation **must** be documented with **inline comments** following these rules (Constitution §10):

### Mandatory Comments

| Element | Requirement |
|---------|-------------|
| Public interfaces and contracts (`I*.cs`) | Comment above each member explaining its purpose and contract expectations |
| Domain entities and value objects | Comment on class purpose and on methods that enforce invariants or state transitions (explain the *why*) |
| Application handlers (commands/queries) | Comment describing the use case, preconditions, and side effects |
| FluentValidation validators | Comment listing the validation rules applied and their business justification |
| Controllers / PageModels | Comment on each action method describing authorization requirements and business flow |
| EF Core configurations | Comment on non-obvious constraints (e.g., exclusion constraints, computed columns, index rationale) |
| Background jobs / hosted services | Comment describing schedule, behavior, and failure mode |
| Complex conditionals and guards | Comment explaining the business rule or security rationale behind the check |

### Documentation Style Rules

- Comments explain **intent, risk, or rationale** — they do not repeat the code.
- Use `//` inline comments, not XML doc comments (`<summary>` is not required).
- Dead code, commented-out code, and `TODO` without a linked issue are prohibited.
- Keep comments concise: 1–2 sentences, placed directly above the relevant code.
- Language: code identifiers and comments in **English**; user-facing messages in **Spanish** (Constitution §4.4).

---

## Configuration Parameters

| Parameter | Description | Default |
|-----------|-------------|---------|
| `MAX_FAILED_ACCESS_ATTEMPTS` | Identity lockout threshold | 5 |
| `LOCKOUT_DURATION_MINUTES` | Lockout duration | 5 |
| `SESSION_IDLE_TIMEOUT_MINUTES` | Cookie idle timeout | 20 |
| `SESSION_ABSOLUTE_LIFETIME_HOURS` | Cookie absolute lifetime | 8–12 |
| `EXPIRY_DAYS` | Auto-expiry for Pending requests (working days) | Configurable |
| `RATE_LIMIT_LOGIN` | Login attempts per minute per IP | 5 |

---

## Referenced By

- [`specs/004-initial-setup-and-authentication/plan.md`](../004-initial-setup-and-authentication/plan.md)
- [`specs/001-vacation-request/plan.md`](../001-vacation-request/plan.md)
- [`specs/005-docker-containerization/plan.md`](../005-docker-containerization/plan.md)
