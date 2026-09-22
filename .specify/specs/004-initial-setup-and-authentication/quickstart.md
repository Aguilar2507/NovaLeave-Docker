# Quickstart: Login and Authentication

**Feature Branch**: `004-login-authentication`  
**Date**: 2026-07-28  
**Plan Reference**: [plan.md](./plan.md)  
**Contracts Reference**: [contracts.md](./contracts.md)  
**Research Reference**: [research.md](./research.md)

---

## Prerequisites

- .NET 10 SDK installed
- SQL Server instance available (local or Docker)
- Solution `NovaLeave.slnx` open in Visual Studio 2026
- Common artifacts reviewed: [architecture.md](../common/architecture.md), [data-model.md](../common/data-model.md), [security.md](../common/security.md)

---

## Step-by-Step Implementation Guide

### Step 1 — Create the feature branch

```bash
git checkout -b 004-login-authentication
```

---

### Step 2 — Add NuGet packages

In `NovaLeave.Infrastructure`:

```bash
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
```

In `NovaLeave.Application`:

```bash
dotnet add package FluentValidation
```

In `NovaLeave.Presentation.Web` (if not already present):

```bash
dotnet add package Microsoft.AspNetCore.Identity.UI  # Only if scaffolding is needed
```

> **Note**: Do NOT add `FluentValidation.AspNetCore` — it is deprecated. Validators are resolved through DI and invoked with `ValidateAsync` (Constitution §7.2).

---

### Step 3 — Create `ApplicationUser` in Infrastructure

File: `src/NovaLeave.Infrastructure/Identity/ApplicationUser.cs`

Extend `IdentityUser` with `IsActive` and `EmployeeId`. See [contracts.md](./contracts.md) §5.

---

### Step 4 — Configure `ApplicationDbContext`

File: `src/NovaLeave.Infrastructure/Identity/ApplicationDbContext.cs`

```csharp
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
	public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();
	// ... other DbSets
}
```

Register in `Program.cs`:

```csharp
builder.Services.AddDbContext<ApplicationDbContext>(options =>
	options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
```

---

### Step 5 — Configure Identity and Cookies

File: `src/NovaLeave.Infrastructure/Configuration/IdentityConfiguration.cs`

Create an extension method `AddNovaLeaveIdentity(this IServiceCollection services)`:

1. `AddIdentity<ApplicationUser, IdentityRole>` with lockout and password options.
2. `AddEntityFrameworkStores<ApplicationDbContext>`.
3. `ConfigureApplicationCookie` with secure settings (see [research.md](./research.md) §1).
4. Custom `OnValidatePrincipal` for account status revalidation (see [research.md](./research.md) §3).

Call from `Program.cs`:

```csharp
builder.Services.AddNovaLeaveIdentity();
```

---

### Step 6 — Configure Rate Limiting

File: `src/NovaLeave.Infrastructure/Configuration/RateLimitingConfiguration.cs`

Create `AddNovaLeaveRateLimiting(this IServiceCollection services)`:

```csharp
services.AddRateLimiter(options =>
{
	options.AddPolicy("login-per-ip", context =>
		RateLimitPartition.GetFixedWindowLimiter(
			partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
			factory: _ => new FixedWindowRateLimiterOptions
			{
				PermitLimit = 5,
				Window = TimeSpan.FromMinutes(1)
			}));
	options.OnRejected = async (ctx, ct) =>
	{
		ctx.HttpContext.Response.StatusCode = 429;
		await ctx.HttpContext.Response.WriteAsync("Too many requests.", ct);
	};
});
```

In `Program.cs` middleware pipeline:

```csharp
app.UseRateLimiter();  // Before UseAuthentication
```

---

### Step 7 — Implement Application Contracts

Create the following files in `src/NovaLeave.Application/Features/Authentication/`:

| File | Description |
|------|-------------|
| `Contracts/IAuthenticationAuditService.cs` | Audit service contract |
| `Contracts/IAccountStatusValidator.cs` | Active status validator |
| `Contracts/IDefaultDashboardResolver.cs` | Role-based dashboard resolver |
| `Login/LoginCommand.cs` | Command record |
| `Login/LoginCommandResult.cs` | Result record |
| `Login/LoginCommandValidator.cs` | FluentValidation validator |
| `Login/LoginCommandHandler.cs` | Handler (coordinates Identity sign-in) |
| `Logout/LogoutCommand.cs` | Logout command |
| `Logout/LogoutCommandHandler.cs` | Handler (sign-out + audit) |

See [contracts.md](./contracts.md) for all signatures.

---

### Step 8 — Implement Infrastructure Services

Create implementations in `src/NovaLeave.Infrastructure/Identity/`:

| File | Implements |
|------|-----------|
| `AccountStatusValidator.cs` | `IAccountStatusValidator` |
| `AuthenticationAuditService.cs` | `IAuthenticationAuditService` |
| `DefaultDashboardResolver.cs` | `IDefaultDashboardResolver` |

Register in DI via an extension method `AddNovaLeaveAuthenticationServices(this IServiceCollection services)`.

---

### Step 9 — Create AccountController

File: `src/NovaLeave.Presentation.Web/Controllers/AccountController.cs`

```csharp
[AllowAnonymous]
public class AccountController : Controller
{
	// GET /Account/Login
	[HttpGet]
	public IActionResult Login(string? returnUrl = null) { ... }

	// POST /Account/Login
	[HttpPost]
	[ValidateAntiForgeryToken]
	[RateLimiter("login-per-ip")]
	public async Task<IActionResult> Login(LoginViewModel model) { ... }

	// POST /Account/Logout
	[HttpPost]
	[Authorize]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Logout() { ... }
}
```

Key behaviors:
- Login GET: if already authenticated → redirect to dashboard (FR-014)
- Login POST: validate → authenticate → audit → redirect (PRG pattern)
- Logout POST: sign out → audit → redirect to Login (PRG pattern)
- All failures: same generic error message "Correo electrónico o contraseña inválidos." (SR-007)

---

### Step 10 — Create LoginViewModel and Login View

File: `src/NovaLeave.Presentation.Web/ViewModels/LoginViewModel.cs` — see [contracts.md](./contracts.md) §4.

File: `src/NovaLeave.Presentation.Web/Views/Account/Login.cshtml`

Key requirements:
- Independent layout (no sidebar) — FR-007, spec_003 §3.1
- Bootstrap 5.3 centered single-column form
- `asp-for` Tag Helpers for Email and Password
- `asp-validation-for` for field errors
- Generic error message area
- Auto-focus on Email field
- Anti-forgery token (`@Html.AntiForgeryToken()` or `<form asp-antiforgery="true">`)
- Accessible labels, `role="alert"` for errors (NFR-007, NFR-008)
- Disable submit button on click to prevent double-submit

---

### Step 11 — Create EF Core Migration

```bash
dotnet ef migrations add AddIdentityAndAudit \
  --project src/NovaLeave.Infrastructure \
  --startup-project src/NovaLeave.Presentation.Web
```

Review the migration. Apply:

```bash
dotnet ef database update \
  --project src/NovaLeave.Infrastructure \
  --startup-project src/NovaLeave.Presentation.Web
```

---

### Step 12 — Seed Test Data

Create a data seeder (Development only) that provisions:
- 1 User-only account (active)
- 1 Approver-only account (active)
- 1 Dual-role account (active)
- 1 Inactive account

Use `UserManager<ApplicationUser>` to create accounts with known passwords for testing.

---

### Step 13 — Write Tests

In `tests/NovaLeave.Presentation.Tests/Authentication/`:

| Test Class | Covers |
|-----------|--------|
| `LoginTests.cs` | Successful login (all role combos), invalid credentials, inactive account, locked account, generic error message, ReturnUrl redirect, already-authenticated redirect |
| `LogoutTests.cs` | Cookie invalidation, anti-forgery rejection, redirect to Login |
| `SessionExpirationTests.cs` | Idle timeout, absolute lifetime, security stamp invalidation (use `FakeTimeProvider`) |
| `RateLimitingTests.cs` | HTTP 429 on threshold exceed, no AccessFailedCount increment |
| `AuditLoggingTests.cs` | Audit records for success, failure (all categories), logout |

In `tests/NovaLeave.Domain.Tests/`:

| Test Class | Covers |
|-----------|--------|
| (none for this feature — Domain has minimal auth logic) | |

---

### Step 14 — Build and Verify

```bash
dotnet build
dotnet test
```

Verify:
- [ ] Login page loads at `/Account/Login`
- [ ] Valid credentials → authenticated + redirect
- [ ] Invalid credentials → generic error (same for all failure types)
- [ ] Inactive account → generic error
- [ ] Rate limit → HTTP 429
- [ ] Logout → cookie invalidated → redirect to Login
- [ ] Anti-forgery token validated on POST
- [ ] Audit records created for all events
- [ ] Session revalidation catches deactivated accounts

---

## Middleware Pipeline Order

```csharp
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute(...);
```

---

## Configuration (appsettings.json)

```json
{
  "ConnectionStrings": {
	"DefaultConnection": "Server=...;Database=NovaLeave;..."
  },
  "Identity": {
	"MaxFailedAccessAttempts": 5,
	"LockoutDurationMinutes": 5,
	"SessionAbsoluteLifetimeHours": 10,
	"SessionIdleTimeoutMinutes": 20,
	"SecurityStampValidationIntervalMinutes": 5
  },
  "RateLimiting": {
	"Login": {
	  "PermitLimit": 5,
	  "WindowMinutes": 1
	}
  }
}
```

---

## Done Checklist

- [ ] Constitution Check passed (see [plan.md](./plan.md))
- [ ] All FR-001 through FR-019 implemented
- [ ] All SR from [security.md](../common/security.md) applied
- [ ] Tests cover SC-001 through SC-010
- [ ] No passwords or PII in logs
- [ ] PR < 400 net lines (split if needed)
- [ ] Migration reviewed and reversible
