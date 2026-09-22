Version change: 3.0.0 -> 4.0.0
Change type: MAJOR
Reason: the MVP business model is redefined per the 2026-07-16 Product Owner
decisions. Actors are reduced to User and Approver; Employee/Direct Manager/HR,
teams, hierarchies, delegation, and escalation are removed. The lifecycle adds
automatic timeout cancellation (CancelledByTimeout) and Approver deactivation of
an approved request before it begins (CancelledByApprover) with atomic balance
restoration, and Pending requests become editable. Balance becomes a single
global accruing balance (one day per completed month, non-expiring) with Pending
reservations. Time-zone-based date evaluation is removed. This is a
backward-incompatible governance change.

Amended invariants (Section 5): 3 (next-day minimum start), 4 (single business
date, no per-user time zone), 6 (Pending is editable), 7 (User/Approver
transition set including timeout and pre-start deactivation; owner cancellation
deferred to an approved specification), 8 (Approved is final except a valid
pre-start deactivation), 9 (Pending editable; resolved requests only undergo a
bounded pre-start deactivation), 10 (reserve-on-create, deduct-on-approve,
restore-on-deactivation), 12 (revalidate role and active status).
Amended sections: 4 (Actors), 5.1, 7.1, 7.3, 7.4, and Principle IX.

Prior 3.0.0 report retained below.
--------------------------------------------------------------------------------
Version change: 2.3.0 -> 3.0.0
Change type: MAJOR
Reason: the primary presentation and authentication models are being redefined
from Web API + future React + JWT Bearer to server-rendered ASP.NET Core MVC
with Razor Views, Bootstrap, ASP.NET Core Identity, and secure cookie
authentication. This is a backward-incompatible governance change.

Preserved decisions:
- .NET 10, C#, Clean Architecture, Domain/Application/Infrastructure/
  Presentation boundaries, EF Core, SQL Server, FluentValidation, Serilog,
  xUnit, Docker, GitHub Actions, and Azure-ready deployment.
- Vertical Slice organization inside Application.
- CQRS preferred but not mandatory; MediatR optional.
- OWASP Top 10:2025 primary baseline: A01, A06, and A09.
- Domain invariants, optimistic concurrency, auditing, observability, data
  governance, and Graphify/OKF/Mermaid governance.

Redefined decisions:
- Primary presentation: ASP.NET Core MVC with Controllers and Razor Views.
- UI framework: Bootstrap 5.3, version-pinned and used through shared layouts,
  partial views, Tag Helpers, and accessible components.
- Authentication: ASP.NET Core Identity with secure cookie authentication for
  the web application.
- Authorization: policies and resource-based authorization remain mandatory.
- Web API and JWT Bearer are no longer defaults. They MAY be introduced only
  for an approved external-client or integration requirement through an ADR.
- React and Blazor are not approved frontend defaults. Introducing either
  requires an ADR and a constitutional amendment when it changes project-wide
  presentation rules.

Added or strengthened controls:
- Thin MVC controllers, dedicated ViewModels, no Domain entities in views, and
  no direct Controller/View access to EF Core or Infrastructure.
- Global antiforgery validation for unsafe browser requests.
- Post/Redirect/Get after successful form submissions.
- Explicit overposting protection, server-authoritative validation, branded
  MVC error pages, and RFC 7807 only for actual API endpoints.
- Secure cookie settings, Identity lockout/password controls, session-lifetime
  rules, and tests for CSRF, forced browsing, cookie expiration, and privilege
  escalation.
- Bootstrap accessibility limitations are acknowledged; WCAG compliance must
  be verified independently rather than assumed from framework usage.
- MVC-focused integration and end-to-end testing through WebApplicationFactory
  and Playwright (or an approved equivalent).
- Static asset governance, CSP compatibility, local hosting preference, and
  dependency scanning for Bootstrap and other browser assets.

Templates requiring review:
- .specify/templates/spec-template.md
- .specify/templates/plan-template.md
- .specify/templates/tasks-template.md
- Any Spec Kit commands or skills that contain a Constitution Check.
-->

# NovaLeave — Consolidated Constitution

**Version:** 4.0.0  
**Ratified:** 2026-07-13  
**Last Amended:** 2026-07-16  
**Status:** Binding

## 1. Purpose, Scope, and Normative Language

This constitution establishes the engineering, security, quality, operations,
and governance rules for **NovaLeave**, a system for managing leave and vacation
requests. It applies to specifications, plans, ADRs, tasks, code, tests,
migrations, documentation, diagrams, pipelines, pull requests, and changes
produced by humans or AI tools.

In this document:

- **MUST / MUST NOT** indicates a non-negotiable obligation.
- **SHOULD / SHOULD NOT** indicates a strong recommendation; any deviation
  requires justification.
- **MAY** indicates a permitted option, not an obligation.

When requirements conflict, **correctness and security are entry conditions**:
neither may be sacrificed for speed, convenience, or architectural aesthetics.
Once both are satisfied, maintainability, simplicity, performance, and developer
experience are prioritized, in that order. Every decision MUST support a real
business outcome and maintainable operations.

## 2. Core Principles

### I. Clean Architecture and Inward-Pointing Dependencies

The codebase MUST be organized into four layers:

1. **Domain**: entities, value objects, aggregates, domain events, and
   invariants. It does not depend on frameworks, persistence, or presentation.
2. **Application**: use cases, contracts, input validation, and orchestration.
   It depends only on Domain and its own abstractions.
3. **Infrastructure**: EF Core, SQL Server, repositories/adapters, external
   services, and technical observability. It implements internal contracts.
4. **Presentation**: ASP.NET Core MVC with Controllers, Razor Views,
   ViewModels, Tag Helpers, filters, middleware, Bootstrap assets, and the
   composition root. It translates HTTP requests and form submissions into
   Application use cases and contains no business rules. An API surface MAY be
   added only for an approved integration or external client requirement.

Dependencies MUST point inward. Domain MUST NOT reference Application,
Infrastructure, or Presentation. Application MUST NOT reference Infrastructure
implementations. Controllers, Views, ViewModels, Tag Helpers, filters, and UI
components MUST NOT directly access `DbContext`, concrete repositories, or
business rules. `Program.cs` MAY reference Infrastructure registration
extensions solely as the composition root; this exception does not permit
Presentation classes to consume Infrastructure implementations directly.

### II. Simplicity Before Speculative Abstraction

The simplest solution that correctly satisfies the current requirement MUST be
preferred over a generic solution designed for hypothetical scenarios. An
interface, pattern, library, configuration option, or additional layer is
introduced only when it provides a demonstrable benefit in testability,
maintainability, security, performance, or decoupling.

Repository, Unit of Work, factories, specifications, state machines, MediatR,
and other patterns MAY be used, but MUST NOT be imposed merely to appear
architecturally pure. EF Core `DbContext` MAY act as the Unit of Work when that
is sufficient.

### III. SOLID and a Single Source of Truth

The design MUST apply SOLID pragmatically. Every business rule MUST exist in one
authoritative location and be reused by every flow. Balance calculations,
overlap checks, transitions, permissions, or policies MUST NOT be duplicated
across controllers, handlers, services, clients, or queries.

**Rationale**: Centralizing rules in Domain guarantees a single, testable
source of truth for what constitutes a valid leave request, balance, or
approval — preventing rule drift between Presentation, Application, and
Infrastructure code paths.

### IV. Rich Domain and Business-Rule Isolation

Invariants MUST be protected by entities, value objects, aggregates, or domain
services. Application coordinates; Domain decides. Creating or transitioning an
invalid aggregate MUST be impossible or fail explicitly and predictably.

Validation is separated as follows:

- **Input validation**: format, required fields, length, and structure;
  implemented with FluentValidation.
- **Business rule**: balance, overlap, state, approval authority, and date
  policies; implemented in Domain.
- **Authorization**: identity, role, ownership, and organizational relationship;
  enforced in Presentation and revalidated in Application for sensitive
  operations.

### V. Explicit Use Cases and Pragmatic CQRS

Every business action MUST be represented by an explicit use case. CQRS is the
preferred style for separating commands and queries, but MediatR is NOT
mandatory. An application service or native handler is valid when it is simpler
and preserves equivalent boundaries and testability.

Within `Application/`, code MUST be organized by feature or vertical slice, not
in global `Commands`, `Handlers`, or `Validators` folders without documented
justification.

### VI. Dependency Injection and Testable Time

All dependencies MUST be constructor-injected through the native ASP.NET Core
container. Service locators, mutable static dependencies, and direct creation of
Infrastructure implementations from Domain or Application are prohibited.

Domain and Application MUST NOT directly use `DateTime.Now` or
`DateTime.UtcNow`. Every time-dependent rule MUST use `TimeProvider`, `IClock`,
or an equivalent abstraction to support deterministic testing.

### VII. Test-First Discipline for Critical Rules

Every feature MUST have acceptance criteria before implementation begins. Every
domain invariant MUST have positive and negative unit tests. Critical workflows
MUST have integration tests that exercise Presentation, Application, and
Infrastructure.

A change to a critical rule, authorization boundary, balance calculation, state
transition, or audit behavior MUST NOT be merged without corresponding tests.

### VIII. Asynchronous Execution and Observability by Default

All I/O MUST use `async`/`await` end to end. `.Result`, `.Wait()`, and
sync-over-async wrappers are prohibited in application code.

Structured logging, correlation, metrics, tracing, health checks, and auditing
are baseline requirements, not optional enhancements.

### IX. Security by Design

The server never trusts the client. Authentication, authorization, ownership,
role and active status, requested-day calculations, balances, and state
transitions MUST be revalidated server-side.

NovaLeave adopts the following OWASP Top 10:2025 categories as its primary
baseline:

- **A01 — Broken Access Control**.
- **A06 — Insecure Design**.
- **A09 — Security Logging and Alerting Failures**.

Other applicable security controls remain mandatory as cross-cutting practices,
even when they are not among the three prioritized categories.

### X. Architecture Serves the Business

Every technical decision MUST be traceable to correctness, security,
maintainability, performance, compliance, or business value. When two rules
appear to compete, the chosen alternative is the one that preserves invariants,
reduces risk, and is simpler to maintain.

## 3. Structure and Technology Decisions

### 3.1 Standard Structure

```text
src/
  NovaLeave.Domain/
  NovaLeave.Application/
  NovaLeave.Infrastructure/
  NovaLeave.Presentation.Web/
    Controllers/
    Views/
      Shared/
    ViewModels/
    Filters/
    TagHelpers/
    Areas/                # only when a real module boundary requires it
    wwwroot/
      css/
      js/
      lib/
tests/
  NovaLeave.UnitTests/
  NovaLeave.IntegrationTests/
  NovaLeave.EndToEndTests/          # critical browser journeys
docs/
  adr/
  diagrams/
  runbooks/
docker/
.github/
```

Any additional top-level folder requires justification in the pull request.

Example vertical organization:

```text
Application/
  LeaveRequests/
    Create/
    Approve/
    Reject/
    Cancel/
  LeaveBalances/
    GetByUser/
```

Each slice contains its request/command/query, handler or service, validator,
DTO, mapping, and related tests.

### 3.2 Approved Stack

- **Runtime:** .NET 10 and C#.
- **Web presentation:** ASP.NET Core MVC with Controllers and Razor Views.
- **UI framework:** Bootstrap 5.3.x, pinned to an explicit version.
- **Persistence:** Entity Framework Core and SQL Server.
- **Validation:** FluentValidation for external input and Domain invariants for
  business rules.
- **Logging:** Serilog over `ILogger<T>`.
- **Authentication:** ASP.NET Core Identity with secure cookie authentication.
- **Authorization:** policies and resource-based authorization; roles MAY be
  claims used by policies but MUST NOT replace ownership or hierarchy checks.
- **Optional API:** ASP.NET Core API endpoints and JWT Bearer MAY be introduced
  only through an approved specification and ADR for a real external client or
  integration.
- **Alternative frontends:** React, Blazor, or another client technology are not
  project defaults and require an ADR before adoption.
- **Infrastructure:** Docker, GitHub Actions, and Azure-ready deployment.
- **Testing:** xUnit; `WebApplicationFactory` for MVC integration tests; Playwright
  or an approved equivalent for critical browser journeys; mocks/fakes only
  where they provide useful isolation.

Changing an approved decision requires an ADR and a constitutional amendment
when the change affects a constitutional rule.

### 3.3 Conventions

- Code, types, properties, methods, and namespaces: English.
- Documentation and user-visible messages: Spanish, unless an approved
  localization decision states otherwise.
- Types: `PascalCase`; variables and parameters: `camelCase`; private fields:
  `_camelCase`.
- Interfaces: `I` prefix.
- Asynchronous methods: `Async` suffix.
- DTO, Request, Response, Command, Query, Handler, Validator, Repository,
  Controller, and ViewModel types MUST use the appropriate suffix.
- Controller names MUST end in `Controller`; each controller SHOULD represent a
  cohesive user-facing resource or workflow.
- Views MUST be grouped by controller or approved Area and MUST use dedicated
  ViewModels rather than Domain entities.
- MVC routes MUST be resource-oriented and readable, for example
  `/leave-requests`, `/leave-requests/create`, and
  `/leave-requests/{id}/approve`. State changes MUST use unsafe HTTP methods,
  normally `POST`, and MUST NOT be performed through `GET`.
- API routes, if approved, use plural resources under `/api` and follow HTTP
  semantics.
- Table names: singular, unless another convention is documented and applied
  consistently.

## 4. MVP Actors and Authorization

The MVP defines exactly two application roles — `User` and `Approver` — plus the
automatic system actor. There are no teams, reporting lines, organizational
scopes, primary/delegate/alternate managers, approval hierarchies, escalation
chains, or HR role.

### 4.1 User

A User MAY create vacation requests, view their own requests, statuses, history,
and global balance, edit their own `Pending` requests, cancel their own `Pending`
requests, and void (annul) their own `Approved` requests before the vacation
period begins. A User MUST NOT view or modify another User's data.

### 4.2 Approver

An Approver whose status is `Active` MAY approve or reject vacation requests
assigned to them through the Employee–Approver assignment relationship
(one Approver per Employee, pre-configured). An Approver MUST NOT approve or
reject a request they own. An `Inactive` Approver MUST NOT resolve requests.

> **PO Decision (2026-07-28):** Approval scope is per-assignment, not system-wide.
> A separate table defines which Approver is assigned to each Employee.

### 4.3 Active and Inactive Status

Users and Approvers have an `Active` or `Inactive` status. Every protected
operation requires a current authenticated identity with `Active` status, role
authorization, resource authorization, and a valid request state.

### 4.4 Users with Multiple Roles

A person MAY hold both the `User` and `Approver` roles and MAY submit their own
requests. Authorization is evaluated per action and resource; holding the
`Approver` role does not permit resolving one's own request.

## 5. Invariants and Lifecycle

The following rules MUST hold regardless of browser screen, controller action, or approved API endpoint:

1. The global balance, available balance, and reservation total can never become
   negative.
2. The start date cannot be later than the end date.
3. Requests for past dates or the current date are not allowed; the earliest
   valid start date is the following calendar day.
4. Date rules are evaluated against a single system business date without
   per-user time-zone behavior; timestamps are stored in UTC.
5. Overlapping requests are prohibited according to the active policy.
6. A request starts in `Pending` and MAY be edited while `Pending` with full
   revalidation.
7. Permitted MVP transitions:
   - `Pending -> Approved`, by an active Approver assigned to the request owner.
   - `Pending -> Rejected`, by an active Approver assigned to the request owner.
   - `Pending -> Cancelled`, by the request owner (User) voluntarily.
   - `Pending -> Expired`, by the system after the configured
     unresolved-request timeout (EXPIRY_DAYS working days).
   - `Approved -> Voided`, by the request owner (User), only before the
     vacation period begins. Days are restored to the balance.

   > **Amendment (2026-07-28):** User-initiated cancellation of Pending and
   > User-initiated voiding of Approved (pre-start) are now part of the MVP
   > per approved spec_001. State names aligned: `Expired` (was
   > `CancelledByTimeout`), `Voided` (was `CancelledByApprover`),
   > `Cancelled` (User-initiated).
8. `Rejected`, `Cancelled`, `Expired`, and `Voided` are final states.
   `Approved` is final except for a valid pre-start voiding to `Voided`
   by the request owner.
9. A `Pending` request may be edited; a resolved request may not be edited,
   except that an `Approved` request may be voided by the owner before the
   vacation start date. No request is returned to `Pending`.
10. A `Pending` request reserves its requested days without a permanent
    deduction. Approving a balance-consuming request converts the reservation
    into a permanent deduction; rejecting, cancelling, or expiring a request
    releases the reservation; voiding an `Approved` request restores the
    previously deducted days. Creating a request never produces a permanent
    deduction.
11. Every transition generates an audit record.
12. Identity, ownership, role, active status, request state, and balance are
    revalidated immediately before a state-changing operation.
13. The requested number of working days MUST be calculated server-side according
    to an approved policy; a client-calculated value is never accepted as truth.

### 5.1 Rules That Belong in Feature Specifications

The constitution does NOT invent policies that have not been approved. Each
feature specification MUST resolve, when applicable:

- Calendar days versus working days and holidays.
- Leave types and which ones consume balance.
- Medical or legal exceptions.
- Whether more than one pending request is permitted.
- Half days, hourly requests, or partial ranges.
- Accrual, expiration, and carryover of balances, and Pending balance reservation.
- Automatic timeout cancellation of unresolved requests and its configuration.
- Approver deactivation of an approved request and its balance effect.
- Whether a User may cancel their own `Pending` request.
- Retroactive corrections and their payroll impact.

Until an approved specification exists, code, AI, Graphify, OKF, and diagrams
MUST NOT assume an answer.

## 6. Persistence, Integrity, and Concurrency

- EF Core is the approved ORM and SQL Server is the production database.
- Data access is confined to Infrastructure.
- Schema changes MUST use versioned migrations. `EnsureCreated()` is prohibited
  in production.
- Approval, balance deduction, and auditing MUST execute within the same atomic
  transaction.
- State and balance operations MUST use optimistic concurrency (`rowversion` or
  equivalent) and handle conflicts predictably.
- Overlap verification MUST be atomic with the operation that confirms the
  request to prevent race conditions.
- Repeatable operations or external integrations SHOULD use idempotency keys or
  equivalent mechanisms.
- Requests and audit records MUST NOT be physically deleted through normal
  operations. Soft delete or immutable retention is used according to the
  record type and data policy.
- Deletion required by law or policy MUST follow an approved, auditable process
  compatible with retention obligations.
- Read queries SHOULD use projection and `AsNoTracking()`.
- N+1 access patterns are prohibited. `Include` and `Select` are used
  deliberately.
- Lists that may exceed 50 records MUST use server-side pagination.
- Foreign keys and frequently filtered columns MUST have appropriate indexes.
- No potentially large query may call `ToListAsync()` without a limit,
  pagination, or documented justification.

## 7. Security and Privacy

### 7.1 Authentication and Authorization

- The web application MUST use ASP.NET Core Identity with cookie
  authentication. A custom password or session implementation is prohibited.
- Authentication cookies MUST be `HttpOnly`, `Secure` in production, and use an
  appropriate `SameSite` policy. Cookie lifetime, idle timeout, renewal, and
  revocation behavior MUST be explicitly configured and tested.
- Identity password, lockout, reset, and account-confirmation policies MUST be
  configured according to the approved security specification; default values
  MUST NOT be accepted without review.
- Authentication state and role/claim changes MUST invalidate or refresh the
  security stamp so stale authorization is not retained indefinitely.
- ASP.NET Core Identity persistence and adapters belong in Infrastructure. The
  Domain model MUST NOT inherit from or depend on `IdentityUser`; the identity
  account and the business `User` is linked through an explicit stable
  identifier and an Application use case.
- The default authorization policy is deny-by-default.
- Every non-public controller or action MUST declare authorization, preferably
  through centralized policies.
- Application MUST verify ownership and active status; hiding a menu item or
  Bootstrap button is not a security control.
- IDs, roles, user IDs, request IDs, balances, hidden inputs, route values,
  and form fields supplied by the browser are untrusted.
- An authorization failure MUST fail closed and avoid revealing whether an
  inaccessible resource exists.
- JWT Bearer authentication is permitted only for an approved API surface and
  MUST then validate signature, issuer, audience, expiration, and required
  claims.

### 7.2 Input, MVC Forms, and Secure Configuration

- All external input MUST be validated using allowlists, bounds, and formats.
- MVC actions MUST accept dedicated input ViewModels. Binding Domain entities
  or persistence entities directly from a request is prohibited because it
  creates overposting risk.
- Server-side validation is authoritative. Client-side Bootstrap or unobtrusive
  validation MAY improve feedback but MUST NOT be the only validation.
- FluentValidation validators MUST be resolved through DI and invoked explicitly
  with `ValidateAsync` from the Application use case or a project-owned
  asynchronous action filter. The deprecated/synchronous MVC auto-validation
  pipeline and unsupported `FluentValidation.AspNetCore` client integration
  MUST NOT be introduced in new code.
- Validation failures MUST be mapped predictably to `ModelState` by a shared
  adapter rather than duplicated in every controller.
- All unsafe browser requests (`POST`, `PUT`, `PATCH`, `DELETE`) MUST validate
  antiforgery tokens. The project SHOULD enforce this globally with
  `AutoValidateAntiforgeryToken` and use explicit exceptions only for approved
  API endpoints with an alternative CSRF threat model.
- Successful form submissions SHOULD follow Post/Redirect/Get to prevent
  accidental duplicate submissions and refresh replays.
- EF Core or parameterized SQL is mandatory; concatenating user input into SQL
  is prohibited.
- Razor's default output encoding MUST remain enabled. `Html.Raw` or equivalent
  rendering of untrusted content is prohibited unless a reviewed sanitizer and
  an explicit business requirement exist.
- CORS SHOULD be disabled for the server-rendered MVC application unless a real
  cross-origin client exists. If enabled for an approved API, production uses
  explicit origins; wildcard origins with credentials are prohibited.
- TLS 1.2+ and HSTS are mandatory in production.
- Secrets and keys are stored outside the repository, preferably in a managed
  secret store such as Azure Key Vault.
- Production MUST use appropriate security headers: CSP,
  `X-Content-Type-Options`, framing protection, and `Referrer-Policy`.
- Bootstrap and other browser assets MUST be pinned to explicit versions and
  included in dependency/security scanning. Local hosting under `wwwroot/lib`
  is preferred. A CDN MAY be used only with approved CSP configuration and
  Subresource Integrity where supported.
- Swagger UI is applicable only when an API is approved. Diagnostics and
  detailed errors MUST NOT be publicly exposed in production.
- Login, password reset, account recovery, and other sensitive endpoints MUST
  apply rate limiting proportional to risk.

### 7.3 Sensitive Data

A request reason may contain personal information and is classified as
Sensitive/PII. It may be visible only to the owner and an authorized active
Approver acting on the request through an approved use case. Logs, traces,
metrics, and diagrams MUST NOT contain the complete reason or other unnecessary
sensitive data.

### 7.4 OWASP A01, A06, and A09

Every security specification or critical workflow MUST document:

- authorized actors and protected assets;
- trust boundaries and sensitive data;
- permitted and prohibited transitions;
- successful flows, failure flows, and abuse cases;
- concurrency, replay, and duplication risks;
- audit events and security acceptance criteria.

Tests MUST cover anonymous access, expired or invalid authentication cookies,
expired-session reuse, incorrect roles, inactive-Approver resolution attempts,
cross-user access and IDOR, forced browsing, privilege escalation,
self-resolution, antiforgery failures, overposting attempts, duplicate form
submissions, duplicate transitions, timeout-versus-resolution races, pre-start
deactivation boundaries, replay, and direct HTTP access that bypasses navigation
or hidden UI controls. Approved API surfaces MUST additionally test invalid and
expired tokens.

## 8. Auditing, Logging, and Observability

- Serilog and `ILogger<T>` MUST produce structured logs.
- Every request MUST carry propagated `correlation_id` and `request_id` values.
- Request logging MUST include method, path, status, and duration without
  sensitive payloads.
- Every transition or critical change MUST record at least:
  `timestamp_utc`, `actor_id`, `actor_role`, `action`, `entity_type`,
  `entity_id`, `result`, `correlation_id`, and `request_id`.
- Where applicable, `before` and `after` values are recorded with sensitive
  fields redacted.
- Audit records MUST be protected against unauthorized modification or deletion.
- Successful and failed authentication, denials, creation, approval, rejection,
  cancellation, invalid-transition attempts, and sensitive administrative
  changes are logged.
- Logs MUST NOT contain passwords, hashes, tokens, keys, secrets, full payloads,
  or complete medical reasons.
- Health checks MUST cover the application and database without exposing PII or
  internal configuration.
- Metrics and tracing MUST instrument creation, approval, rejection, balance
  queries, errors, and latency.
- Production MUST define alerts for repeated authentication failures, anomalous
  denials, suspected IDOR, 5xx errors, latency, and high-risk administrative
  changes.

## 9. Testing and Definition of Done

### 9.1 Test Pyramid

- **Unit:** Domain and isolated use cases; no I/O.
- **Integration:** EF Core/SQL and the real MVC pipeline through
  `WebApplicationFactory`, including routing, model binding, validation,
  filters, authorization, antiforgery behavior, and rendered responses where
  relevant.
- **End-to-end:** critical browser journeys using Playwright or an approved
  equivalent, especially login, request creation, validation, approval,
  rejection, cancellation, authorization, and payroll-impacting flows.

SQLite, Testcontainers, or an isolated SQL instance MAY be used according to the
test's objective. EF Core InMemory MUST NOT be used to claim relational behavior
that the provider does not reproduce.

### 9.2 Mandatory Requirements

- Every critical rule has positive, negative, and boundary cases.
- Every bug fix includes a regression test that fails before the fix.
- Tests are deterministic and independent of execution order and the real clock.
- Tests do not call real external services.
- Concurrency changes include race-condition and double-application tests.
- Security changes include authorization, abuse-case, and redaction tests.
- Audit changes verify event creation and event structure.

The line-coverage target for critical Domain and Application modules is
**>= 80%**. Coverage does not replace the obligation to test every invariant and
every rejection scenario.

### 9.3 CI Gate

Before merge, CI MUST execute and pass:

1. restore and build;
2. formatting and `.editorconfig` checks;
3. Roslyn analyzers and nullable analysis;
4. applicable unit and integration tests;
5. coverage measurement;
6. SAST, SCA, and vulnerable-dependency scanning;
7. license verification where applicable;
8. migration and modified-Mermaid validation when tooling exists;
9. browser asset vulnerability checks and critical MVC end-to-end tests when
   affected by the change.

High/Critical findings block merge unless a formal exception includes a
mitigation, accountable owner, and expiration date.

## 10. Code Quality and Maintainability

- Nullable Reference Types MUST be enabled.
- Warnings MUST be treated as errors in Domain and Application; elsewhere, any
  exclusion requires justification.
- Methods MUST be cohesive and have a single responsibility.
- Dead code, commented-out code, temporary debugging, and `TODO` comments
  without a linked issue are prohibited.
- Cyclomatic complexity SHOULD be <= 10, and methods SHOULD remain below 40
  lines; exceptions are documented in the pull request.
- A pull request SHOULD remain below 400 net lines and cover one purpose.
  Migrations or generated code may be excluded from the measurement, but not
  from review.
- Public interfaces and contracts MUST include sufficient XML documentation.
- Comments explain intent, risk, or rationale; they do not repeat the code.

## 11. MVC, Bootstrap, User Experience, and Optional APIs

### 11.1 MVC Presentation Rules

- Controllers MUST be thin. Their responsibilities are limited to receiving the
  HTTP request, invoking an Application use case, translating the result into a
  ViewModel or redirect, and selecting the response.
- Controllers and Views MUST NOT contain balance calculations, overlap checks,
  authorization decisions, state-transition rules, or direct data access.
- Views MUST use strongly typed ViewModels. Domain and EF Core entities MUST NOT
  be exposed directly to Razor Views.
- Shared layout, navigation, validation summary, status badges, alerts,
  pagination, confirmation dialogs, and form components SHOULD be implemented
  through `_Layout.cshtml`, partial views, View Components, or Tag Helpers to
  avoid duplicated markup and behavior.
- `Areas` MAY be used for cohesive modules such as an administrative module only
  when they improve navigation and ownership; they MUST NOT duplicate Application
  or Domain logic.
- Static files MUST be served only from approved locations under `wwwroot` and
  the middleware pipeline MUST place static files, routing, authentication,
  authorization, antiforgery behavior, and exception handling in a reviewed
  order.
- State-changing actions MUST use explicit confirmation where accidental
  execution would have meaningful impact and MUST return clear feedback.

### 11.2 Bootstrap Governance

- Bootstrap 5.3.x is the approved UI framework and MUST be pinned to an explicit
  version. Version upgrades require visual regression review and dependency
  scanning.
- Bootstrap MUST be used consistently through a small set of shared design
  conventions for spacing, forms, tables, buttons, alerts, badges, navigation,
  modals, and responsive breakpoints.
- Project-specific CSS MUST live in controlled files under `wwwroot/css` and
  SHOULD extend Bootstrap variables/utilities rather than duplicate the entire
  framework.
- Bootstrap JavaScript components MUST be initialized using the supported
  Bootstrap APIs. Inline scripts and duplicated per-view initialization SHOULD
  be avoided; shared modules under `wwwroot/js` are preferred.
- Bootstrap does not guarantee accessibility by itself. Every screen MUST still
  be reviewed for semantic markup, accessible names, focus behavior, keyboard
  operation, contrast, reduced motion, and screen-reader feedback.
- Color MUST NOT be the only way to communicate request status. Badges and
  alerts MUST include readable text and appropriate semantics.

### 11.3 Forms, Validation, and Feedback

- Every form control MUST have an associated accessible label or equivalent
  accessible name.
- Razor Tag Helpers (`asp-for`, `asp-validation-for`,
  `asp-validation-summary`) SHOULD be used to connect ViewModels and validation
  messages consistently.
- Bootstrap validation styles MAY mirror server results, but server-side
  FluentValidation and Domain behavior remain authoritative.
- Validation errors MUST be specific, actionable, localized, and preserve
  non-sensitive user input when redisplaying the form.
- Success, warning, and error feedback MUST use shared components and MUST be
  understandable without relying only on color or icons.
- Destructive or final actions such as rejection or cancellation SHOULD require
  explicit confirmation and must remain protected by server-side authorization
  and antiforgery validation.

### 11.4 Errors and HTTP Responses

- MVC validation failures return the same view with `ModelState` errors or an
  equivalent typed result; they MUST NOT be converted into generic exceptions.
- Unhandled MVC failures MUST be handled by centralized exception middleware or
  filters, logged with correlation data, and rendered through branded
  400/403/404/500 pages without stack traces.
- Access denied and not-found behavior MUST avoid leaking protected resource
  existence.
- RFC 7807 Problem Details is mandatory only for approved API endpoints. It is
  not the user-facing error format for normal Razor Views.

### 11.5 Accessibility and Localization

- User interfaces SHOULD meet WCAG 2.1 AA requirements: contrast, labels,
  keyboard navigation, visible focus, semantic headings, table headers, modal
  focus management, and suitable ARIA only when native HTML is insufficient.
- User-visible text, validation messages, status labels, and navigation SHOULD
  be centralized in `.resx` resources or an approved localization mechanism.
- Terminology MUST be consistent across Views; the same business concept cannot
  alternate between unrelated labels without an approved content decision.
- Responsive behavior MUST support the approved desktop and mobile viewport
  range without hiding required actions or information.

### 11.6 Optional API Rules

- NovaLeave does not require a public API for the MVC MVP.
- An API MAY be added only when an approved specification identifies a real
  external client, integration, or automation requirement.
- Approved APIs MUST publish OpenAPI definitions, use resource-oriented routes,
  return RFC 7807 Problem Details, and follow semantic versioning for breaking
  changes.
- JWT Bearer is the default authentication mechanism only for such approved API
  clients; MVC cookie authentication MUST NOT be replaced merely to reuse the
  API model.
- MVC actions and API endpoints MUST call the same Application use cases rather
  than duplicate business logic.

## 12. Performance, Availability, and Operations

### 12.1 Performance

- Critical Domain/Application operations and focused data queries SHOULD
  achieve p95 < 300 ms under expected load.
- Standard MVC pages SHOULD complete server-side processing and begin the
  response within p95 < 500 ms under expected load, excluding client network and
  browser rendering time.
- Complex reports may exceed 500 ms only when documented, measured, and
  paginated or moved to an approved asynchronous workflow where appropriate.
- Changes that affect critical paths MUST include a proportional benchmark or
  load test.
- RPS and concurrency targets MUST be documented for each production release.

### 12.2 Scalability

- Presentation MUST be stateless with respect to business workflow state and
  suitable for horizontal scaling. Authentication cookies are permitted, but
  mutable business data MUST NOT be stored only in in-process session.
- TempData MAY be used for short-lived user feedback; it MUST NOT become the
  authoritative store for requests, balances, or authorization state.
- Long-running operations MUST execute outside the synchronous request through
  approved jobs or queues when such mechanisms are introduced.
- Redis, messaging, or other infrastructure is not added until a real use case
  and approved decision exist.

### 12.3 Production Operational Targets

- Availability target: 99.9% monthly.
- RTO target: <= 1 hour for critical workflows.
- RPO target: <= 15 minutes for transactional data.
- Automated backups: at least daily.
- Restore and disaster recovery: documented and exercised at least annually.
- Runbooks: stored under `docs/runbooks/` and linked from critical alerts.

These values are production targets. During the MVP, every unimplemented
requirement MUST be recorded as a gap, risk, and evolution plan.

## 13. Data and Retention

- Minimum data classes are Public, Internal, Sensitive/PII, and Regulated.
- NovaLeave stores only the data necessary for the process.
- The default retention period for business and audit records is seven years,
  unless another approved law or policy applies.
- Extraordinary access to PII for debugging requires justification, approval,
  and auditing.
- Export, rectification, and regulated deletion MUST have documented processes.
- Historical analytics SHOULD use anonymization or pseudonymization.
- Rules are documented in `docs/data-governance.md`.

## 14. Documentation, ADRs, and Diagrams as Code

- Relevant architectural decisions MUST be recorded under `docs/adr/`.
- Rule changes MUST update specifications, tests, and documentation in the same
  pull request.
- Material entity or aggregate changes MUST update a `classDiagram` or
  equivalent model under `docs/diagrams/` or `.specify/diagrams/`.
- Workflows and states MUST use Mermaid where appropriate:
  - `flowchart` for processes and decisions;
  - `stateDiagram-v2` for lifecycles;
  - `sequenceDiagram` for interactions;
  - `classDiagram` for domain models;
  - `erDiagram` for persistence models.
- Diagrams MUST NOT invent actors, tables, relationships, states, or rules.

## 15. Graphify, OKF, Mermaid, and AI

### 15.1 Authority Order

1. Current constitution.
2. Approved feature specifications.
3. Approved ADRs and plans.
4. Executable code and tests.
5. OKF, Mermaid, and Graphify as derived artifacts.

A derived artifact never replaces an authoritative source. A conflict MUST be
reported and resolved explicitly.

### 15.2 Graphify

Graphify MAY support dependency and impact analysis. Its output is derived
context and MUST be verified against files, specifications, and tests. A stale
or unavailable graph cannot block build, test, execution, or deployment.

### 15.3 Open Knowledge Format

OKF MAY summarize and link sources while preserving provenance and clearly
distinguishing facts, decisions, assumptions, questions, and stale content. It
MUST NOT introduce new requirements or permissions.

### 15.4 Mermaid

Mermaid is the preferred format for version-controlled diagrams. A rendering
failure does not affect the application, but modified syntax SHOULD be validated
before merge.

### 15.5 AI-Assisted Development

- All AI-generated code receives human review equivalent to any other change.
- AI MUST verify authoritative sources before modifying rules.
- AI MUST NOT silently resolve contradictions or invent requirements.
- Every dependency proposed by AI requires security, licensing, and maintenance
  evaluation.
- Prompts, outputs, graphs, and knowledge pages MUST NOT contain secrets or real
  PII.
- Use of external providers for semantic analysis of sensitive information
  requires organizational approval.

## 16. Git, Pull Requests, and Governance

### 16.1 Git Workflow

- One feature or fix per branch and pull request.
- Conventional Commits is mandatory.
- Direct pushes to `main` are prohibited.
- Every pull request requires review and a green CI pipeline.
- Applicable architecture, rules, security, migrations, tests, documentation,
  and diagrams are part of code review.

### 16.2 Minimum Pull Request Checklist

- Scope and acceptance criteria identified.
- Constitution Check completed.
- Tests added and passing.
- Authorization and abuse cases reviewed where applicable.
- Migration and rollback strategy documented where applicable.
- Auditing and log redaction verified.
- Razor Views, shared Bootstrap components, localization resources, OpenAPI
  (only if an API changed), ADR, README, and diagrams updated where applicable.
- Antiforgery, overposting, authorization, accessibility, and responsive behavior
  reviewed for affected MVC flows.
- No unresolved High/Critical findings.
- Performance impact measured for critical paths.

### 16.3 Exceptions

Every deviation MUST include in the pull request:

- affected rule;
- technical or business reason;
- risk;
- mitigation;
- accountable owner;
- expiration date or removal condition.

Silent deviations are prohibited.

### 16.4 Amendments and Versioning

Amendments are made through a pull request, with a Sync Impact Report at the
beginning of the document and a review of dependent templates and skills.

- **MAJOR:** removes or incompatibly redefines a rule.
- **MINOR:** adds a principle or materially expands obligations.
- **PATCH:** clarifies wording without changing normative meaning.

Every `/speckit-plan` plan MUST run a Constitution Check before Phase 0 and
repeat it after the Phase 1 design.

## 17. Risks and Evolution

Primary risks include incorrect balances, unauthorized approvals, lost audit
records, PII leakage, concurrency races, stale documentation, and
overengineering. Mandatory mitigations are domain invariants, resource-based
authorization, transactions, row versioning, tests, auditing, redaction,
updated diagrams, and simplicity.

The architecture MAY evolve toward notifications, calendars, Microsoft Entra
ID, multi-company support, jobs, public APIs, Power BI, Blazor components, React,
or other integrations, but none is a current default. Each requires an approved
need and ADR, and no future possibility justifies implementing complexity before
that need exists.

---

**Version:** 4.0.0 | **Ratified:** 2026-07-13 | **Last Amended:** 2026-07-16
