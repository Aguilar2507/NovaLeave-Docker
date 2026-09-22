# Implementation Plan: Login and Authentication

**Branch**: `004-login-authentication` | **Date**: 2026-07-28 | **Spec**: [spec_004-login-authentication.md](../../.specify/specs/spec_004-login-authentication.md)

**Input**: Feature specification from `.specify/specs/spec_004-login-authentication.md`

---

## Summary

Implement ASP.NET Core Identity cookie-based authentication for NovaLeave MVP: login form, credential validation with generic error messages, secure session cookies, logout, session expiration/revalidation, rate limiting, and audit logging. Two roles (User, Approver) with role-appropriate redirection.

---

## Technical Context

**Language/Version**: C# / .NET 10  
**Primary Dependencies**: ASP.NET Core Identity, ASP.NET Core MVC, EF Core, FluentValidation, Serilog  
**Storage**: SQL Server (Identity tables + Employee linkage + AuditRecord)  
**Testing**: xUnit, WebApplicationFactory, Playwright  
**Target Platform**: Linux container / Azure App Service  
**Project Type**: Web application (server-rendered MVC)  
**Performance Goals**: Login page p95 < 300ms, POST auth p95 < 500ms  
**Constraints**: OWASP A01/A06/A09 compliance, no JWT/Bearer, no self-registration

---

## Common Artifacts (DO NOT DUPLICATE)

The following shared artifacts are defined once and referenced here:

- **Data Model**: [`specs/common/data-model.md`](../common/data-model.md) — IdentityUser, Employee (status, roles), AuditRecord
- **Security Requirements**: [`specs/common/security.md`](../common/security.md) — SR-001 through SR-009, SR-012, SR-013, SR-014, SR-015
- **Architecture & Tech Stack**: [`specs/common/architecture.md`](../common/architecture.md) — Clean Architecture, layers, testing strategy, configuration

---

## Constitution Check

| Gate | Status |
|------|--------|
| Clean Architecture (§2.I) | ✅ Presentation → Application → Domain |
| ASP.NET Core MVC + Identity Cookies (§3.2) | ✅ No JWT/Bearer |
| Anti-CSRF global (§7.2) | ✅ AutoValidateAntiforgeryToken |
| Post/Redirect/Get (§7.2) | ✅ Login + Logout follow PRG |
| Dedicated ViewModels (§7.2) | ✅ LoginViewModel only |
| Security stamp invalidation (§7.1) | ✅ On role/status change |
| Logout behavior (SR-012) | ✅ POST → SignOut → redirect to Login |
| Read-session validity (SR-013) | ✅ Stamp revalidation every 5 min for reads |
| Server-authoritative validation (§7.2) | ✅ Never trust client |
| Structured logging/audit (§8) | ✅ Serilog + AuditRecord |
| TimeProvider for time-dependent logic (§2.VI) | ✅ Session expiry tests |
| Async I/O (§2.VIII) | ✅ End-to-end async |

---

## Project Structure

### Documentation (this feature)

```text
specs/004-initial-setup-and-authentication/
├── plan.md              # This file
├── research.md          # Phase 0: Identity configuration research
├── contracts/           # Phase 1: Interface contracts
│   ├── IAuthenticationAuditService.cs
│   └── IAccountStatusValidator.cs
└── quickstart.md        # Phase 1: Getting started guide
```

### Source Code

```text
src/
  NovaLeave.Domain/
	Entities/Employee.cs          (AccountStatus enum, Active check — SHARED across specs)
	Entities/AuditRecord.cs       (Immutable audit entity — SHARED across specs)

  NovaLeave.Application/
	Features/Authentication/
	  Login/
		LoginCommand.cs
		LoginCommandValidator.cs
		LoginCommandHandler.cs
	  Logout/
		LogoutCommand.cs
		LogoutCommandHandler.cs
	  Contracts/
		IAuthenticationAuditService.cs
		IAccountStatusValidator.cs

  NovaLeave.Infrastructure/
	Identity/
	  ApplicationUser.cs          (extends IdentityUser)
	  ApplicationDbContext.cs
	  AccountStatusValidator.cs
	  AuthenticationAuditService.cs
	Configuration/
	  IdentityConfiguration.cs
	  CookieConfiguration.cs
	  RateLimitingConfiguration.cs

  NovaLeave.Presentation.Web/
	Controllers/
	  AccountController.cs        (Login GET/POST, Logout POST)
	ViewModels/
	  LoginViewModel.cs
	Views/
	  Shared/
		_LoginLayout.cshtml       (independent layout, no sidebar — per spec_003 §3.1)
	  Account/
		Login.cshtml
	Filters/
	  ValidateAccountStatusFilter.cs

tests/
  NovaLeave.Application.Tests/
	Authentication/
	  LoginCommandValidatorTests.cs
  NovaLeave.Domain.Tests/
	Entities/
	  EmployeeTests.cs
	  VacationRequestTests.cs
	ValueObjects/
	  DateRangeTests.cs
  NovaLeave.Presentation.Tests/
	Fixtures/
	  AuthTestFixture.cs          (WebApplicationFactory + seed data)
	Authentication/
	  LoginTests.cs
	  LogoutTests.cs
	  SessionExpirationTests.cs
	  RateLimitingTests.cs
	  AuditLoggingTests.cs
  NovaLeave.E2E.Tests/
	Authentication/
	  LoginE2ETests.cs
	  LogoutE2ETests.cs
```

---

## Implementation Phases

### Phase 0 — Solution Scaffold (Prerequisite)

> **Blocking**: No other phase can begin until the multi-layer solution exists.

1. Create `src/NovaLeave.Domain/NovaLeave.Domain.csproj` (class library, net10.0) — shared entities: `Employee`, `AccountStatus`, domain invariants
2. Create `src/NovaLeave.Application/NovaLeave.Application.csproj` (class library, references Domain) — use cases, contracts, validators
3. Create `src/NovaLeave.Infrastructure/NovaLeave.Infrastructure.csproj` (class library, references Application) — EF Core, Identity, SQL Server
4. Refactor root `NovaLeave.csproj` → move to `src/NovaLeave.Presentation.Web/NovaLeave.Presentation.Web.csproj` (references Application + Infrastructure for DI composition root)
5. Create `tests/NovaLeave.Application.Tests/NovaLeave.Application.Tests.csproj` (xUnit, references Application)
6. Create `tests/NovaLeave.Presentation.Tests/NovaLeave.Presentation.Tests.csproj` (xUnit + WebApplicationFactory, references Presentation.Web)
7. Update `NovaLeave.slnx` with all projects organized in `src/` and `tests/` solution folders
8. Create `Directory.Build.props` at root (shared `TargetFramework=net10.0`, `Nullable=enable`, `ImplicitUsings=enable`)
9. Create `.editorconfig` with C# coding conventions
10. Add NuGet packages: `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `FluentValidation.AspNetCore`, `Serilog.AspNetCore`, `Microsoft.EntityFrameworkCore.SqlServer` (to Infrastructure); `xunit`, `Microsoft.AspNetCore.Mvc.Testing` (to test projects)
11. Configure `Program.cs`: add `AddControllersWithViews()`, `UseAuthentication()`, `MapControllerRoute()` alongside existing Razor Pages
12. Configure `appsettings.Development.json` with connection string for SQL Server LocalDB (`Server=(localdb)\\mssqllocaldb;Database=NovaLeave;Trusted_Connection=True;TrustServerCertificate=True`)
13. Create **ALL** shared domain entities from `common/data-model.md` in `src/NovaLeave.Domain/` — complete with every attribute, method, and invariant: `AccountStatus.cs`, `RequestStatus.cs`, `Employee.cs` (with Balance, ReserveDays/DeductDays/RestoreDays), `AuditRecord.cs` (immutable, 12 properties), `VacationRequest.cs` (state machine, RowVersion), `ValueObjects/DateRange.cs`

**Shared Entity Strategy**: All entities from `common/data-model.md` are defined **completely** in `NovaLeave.Domain` as part of Phase 0. No feature spec should create or modify these entities — only consume them. The `ApplicationDbContext` in Infrastructure maps them. Each feature branch creates **additive migrations** only. On merge to main, conflicts are resolved with `dotnet ef migrations add MergePoint`.

### Phase 0.1 — Research

- ASP.NET Core Identity cookie configuration options for .NET 10
- Rate limiting middleware configuration (`System.Threading.RateLimiting`)
- Security stamp revalidation interval and custom `CookieAuthenticationEvents`
- `TimeProvider` integration for deterministic session expiry testing

### Phase 1 — Design & Contracts

1. Define `LoginCommand`, `LoginViewModel`, and validation rules
2. Define `IAuthenticationAuditService` contract
3. Define `IAccountStatusValidator` contract
4. Design `ApplicationUser` extending `IdentityUser` with status linkage
5. Design cookie/session configuration model

### Phase 2 — Implementation

1. **Identity setup**: Configure Identity services, `ApplicationUser`, `ApplicationDbContext`
2. **Cookie configuration**: Secure settings, sliding expiration, absolute lifetime, idle timeout
3. **AccountController**: Login GET (render form), Login POST (authenticate), Logout POST
4. **LoginViewModel**: Email, Password, anti-forgery (handled by framework)
5. **Account status validation**: Check `Active` status during sign-in
6. **Generic error messages**: Same message for all failure types
7. **Rate limiting**: Configure per-endpoint rate limiter for `/Account/Login`
8. **Audit logging**: Record all auth events via `IAuthenticationAuditService`
9. **Session revalidation**: Security stamp validation interval, custom events
10. **Role-based redirection**: User Dashboard (default) vs Approver Dashboard
11. **Login page UI**: Razor view with Bootstrap, centered layout, auto-focus, accessibility

### Phase 3 — Testing

1. Unit tests: `LoginCommandValidator`, account status logic
2. Integration tests: successful login, all failure paths, rate limiting, audit records
3. Integration tests: logout invalidation, session expiry, security stamp changes
4. Integration tests: anti-CSRF rejection, over-posting protection
5. E2E tests: login flow, redirection, role switcher visibility

---

## Feature-Specific Functional Requirements

> Common security requirements (anti-CSRF, existence hiding, session revalidation, secure cookies, audit, data minimization) are defined in [`specs/common/security.md`](../common/security.md).

| ID | Requirement |
|----|-------------|
| FR-001 | Login page at `/Account/Login` with Email, Password, Sign In button |
| FR-002 | Authenticate via ASP.NET Core Identity cookies only |
| FR-003 | Deny `Inactive` accounts with same generic error |
| FR-004 | Dedicated `LoginViewModel` (no entity binding) |
| FR-005 | Anti-forgery on Login POST and Logout POST |
| FR-006 | Rate limiting on `/Account/Login` independent of lockout |
| FR-007 | Login page uses independent layout (no sidebar) |
| FR-008 | Session cookie: HttpOnly, Secure, SameSite, sliding + absolute expiry |
| FR-009 | Invalidate cookie on logout |
| FR-010 | Security stamp update on role/status change |
| FR-011 | Server-side revalidation before state-changing ops |
| FR-012 | ReturnUrl redirection after login |
| FR-013 | Default dashboard by role |
| FR-014 | Already-authenticated users redirected from Login |
| FR-015 | Identity lockout: 5 failed attempts → 15-minute lockout (independent of SR-006 rate limiting) |
| FR-016 | Same generic error for all auth failures |
| FR-017 | Reset AccessFailedCount on success |
| FR-018 | Audit entry for every auth attempt |
| FR-019 | No passwords/PII in audit logs |
| FR-020 | ReturnUrl must be validated as local URL; external URLs rejected (SR-014) |

---

## Success Criteria

See spec: SC-001 through SC-010 in `spec_004-login-authentication.md`.

---

## Dependencies

- **Depends on**: Common data model (Employee, AuditRecord), common architecture
- **Blocks**: All other features (authentication is the entry point)
- **No dependency on**: spec_001 (vacation request)
