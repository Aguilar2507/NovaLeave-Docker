# Research: Login and Authentication

**Feature Branch**: `004-login-authentication`  
**Date**: 2026-07-28  
**Plan Reference**: [plan.md](./plan.md)  
**Spec Reference**: [spec_004-login-authentication.md](../../.specify/specs/spec_004-login-authentication.md)  
**Common Artifacts**: [architecture.md](../common/architecture.md) | [security.md](../common/security.md) | [data-model.md](../common/data-model.md)

---

## Research Objectives

This document covers Phase 0 technical investigation for the login and authentication feature. Each topic includes findings, recommended approach, and risks.

---

## 1. ASP.NET Core Identity Cookie Configuration (.NET 10)

### Question
What is the recommended approach for configuring Identity cookie authentication in .NET 10, including sliding expiration, absolute lifetime, idle timeout, and secure cookie settings?

### Findings

ASP.NET Core Identity in .NET 10 configures cookies through `AddIdentity<TUser, TRole>()` plus `ConfigureApplicationCookie()`:

```csharp
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
	// Lockout
	options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
	options.Lockout.MaxFailedAccessAttempts = 5;
	options.Lockout.AllowedForNewUsers = true;

	// Password policy (review with security spec)
	options.Password.RequiredLength = 8;
	options.Password.RequireDigit = true;
	options.Password.RequireUppercase = true;
	options.Password.RequireLowercase = true;
	options.Password.RequireNonAlphanumeric = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
	options.Cookie.HttpOnly = true;
	options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // HTTPS only in prod
	options.Cookie.SameSite = SameSiteMode.Lax;
	options.ExpireTimeSpan = TimeSpan.FromHours(10);        // Absolute lifetime
	options.SlidingExpiration = true;
	options.LoginPath = "/Account/Login";
	options.LogoutPath = "/Account/Logout";
	options.AccessDeniedPath = "/Account/AccessDenied";
});
```

### Idle Timeout vs Absolute Lifetime

ASP.NET Core cookies support `ExpireTimeSpan` (absolute from issuance) and `SlidingExpiration` (refreshes on each request within the window). However, there is **no native idle timeout** separate from sliding expiration.

**Recommended approach for idle timeout (20 min):**
- Use `SecurityStampValidationInterval` to periodically revalidate the session server-side.
- Implement a custom `CookieAuthenticationEvents.OnValidatePrincipal` that checks a `LastActivity` timestamp stored server-side (in cache or DB) and rejects the cookie if idle > 20 min.

```csharp
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
	options.ValidationInterval = TimeSpan.FromMinutes(5); // Check every 5 min
});
```

### Recommendation
- **Absolute lifetime**: 10 hours via `ExpireTimeSpan`
- **Sliding expiration**: Enabled (extends on activity)
- **Idle timeout**: Custom `OnValidatePrincipal` event checking last activity
- **Security stamp interval**: 5 minutes (catches role/status changes)

### Risks
- Custom idle timeout adds complexity; must be tested with `TimeProvider` abstraction.
- `SecurityStampValidationInterval` too short impacts performance; too long delays session invalidation.

---

## 2. Rate Limiting Middleware

### Question
How to implement per-endpoint rate limiting for `/Account/Login` independent of Identity's lockout, using .NET 10's built-in rate limiting?

### Findings

.NET 7+ includes `System.Threading.RateLimiting` with built-in middleware:

```csharp
builder.Services.AddRateLimiter(options =>
{
	options.AddFixedWindowLimiter("login", limiterOptions =>
	{
		limiterOptions.PermitLimit = 5;
		limiterOptions.Window = TimeSpan.FromMinutes(1);
		limiterOptions.QueueLimit = 0;
	});

	options.OnRejected = async (context, cancellationToken) =>
	{
		context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
		await context.HttpContext.Response.WriteAsync(
			"Too many requests. Please try again later.", cancellationToken);
	};
});

// In middleware pipeline:
app.UseRateLimiter();
```

Apply to the login endpoint:

```csharp
[HttpPost]
[RateLimiter("login")]  // Or via endpoint metadata
public async Task<IActionResult> Login(LoginViewModel model) { ... }
```

### Partitioning Strategy

For per-IP limiting:

```csharp
options.AddFixedWindowLimiter("login", limiterOptions => { ... });
// Or use a policy with partition key:
options.AddPolicy("login-per-ip", context =>
	RateLimitPartition.GetFixedWindowLimiter(
		partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
		factory: _ => new FixedWindowRateLimiterOptions
		{
			PermitLimit = 5,
			Window = TimeSpan.FromMinutes(1)
		}));
```

### Recommendation
- Use `AddPolicy` with IP-based partitioning for production.
- Use `AddFixedWindowLimiter` for simplicity in MVP.
- Rate limiter executes **before** credential verification (FR-006, spec CU-013).
- Rate-limited requests return HTTP 429 and do NOT increment `AccessFailedCount`.

### Risks
- IP-based limiting can be bypassed by distributed attackers; acceptable for MVP.
- Behind a reverse proxy, ensure `X-Forwarded-For` is trusted and configured.

---

## 3. Security Stamp Revalidation and Custom Cookie Events

### Question
How to implement server-side revalidation of account status (`Active`/`Inactive`) and role changes during an active session?

### Findings

Identity's `SecurityStampValidator` runs on `OnValidatePrincipal` at the configured interval. When the stamp in the cookie doesn't match the DB, the principal is rejected.

**For account status checks**, extend the validator or use custom events:

```csharp
builder.Services.ConfigureApplicationCookie(options =>
{
	options.Events = new CookieAuthenticationEvents
	{
		OnValidatePrincipal = async context =>
		{
			// Run Identity's built-in stamp validation first
			await SecurityStampValidator.ValidatePrincipalAsync(context);

			if (context.Principal?.Identity?.IsAuthenticated == true)
			{
				var userManager = context.HttpContext.RequestServices
					.GetRequiredService<UserManager<ApplicationUser>>();
				var user = await userManager.GetUserAsync(context.Principal);

				if (user == null || !user.IsActive)
				{
					context.RejectPrincipal();
					await context.HttpContext.SignOutAsync();
				}
			}
		}
	};
});
```

### When Security Stamp Changes

The stamp MUST be updated when:
- Role is added/removed → `UserManager.AddToRoleAsync` / `RemoveFromRoleAsync` auto-updates stamp.
- Account status changes to `Inactive` → Must manually call `UpdateSecurityStampAsync`.
- Password changes → Auto-updates stamp.

### Recommendation
- Custom `OnValidatePrincipal` that combines stamp validation + active status check.
- `ValidationInterval = 5 minutes` balances security vs. performance.
- For immediate invalidation on status change: update security stamp + user must re-auth on next validation cycle.

### Risks
- Between validation intervals, a deactivated user can still perform actions. Mitigated by FR-011 (server-side revalidation before state-changing ops).

---

## 4. TimeProvider Integration for Deterministic Testing

### Question
How to use `TimeProvider` abstraction for session expiry and time-dependent logic in tests?

### Findings

.NET 8+ provides `System.TimeProvider` as a first-class abstraction:

```csharp
// Production
builder.Services.AddSingleton(TimeProvider.System);

// Test
var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 7, 28, 10, 0, 0, TimeSpan.Zero));
services.AddSingleton<TimeProvider>(fakeTime);

// Advance time in test
fakeTime.Advance(TimeSpan.FromMinutes(25)); // Simulate idle timeout
```

For Identity cookie events, inject `TimeProvider` into the custom `OnValidatePrincipal` to compare `LastActivity` against testable time.

### Recommendation
- Register `TimeProvider.System` in DI for production.
- Use `Microsoft.Extensions.TimeProvider.Testing.FakeTimeProvider` in tests.
- All time-dependent checks (idle timeout, session expiry) use injected `TimeProvider`.

### Risks
- None significant. `TimeProvider` is the .NET standard approach.

---

## 5. ApplicationUser Design — Identity vs Domain Separation

### Question
How to link ASP.NET Core Identity `IdentityUser` with the Domain `Employee` entity without violating Constitution §7.1 (Domain MUST NOT depend on IdentityUser)?

### Findings

Constitution §7.1 explicitly states:
> *The Domain model MUST NOT inherit from or depend on `IdentityUser`; the identity account and the business User is linked through an explicit stable identifier and an Application use case.*

**Design:**

```
Infrastructure layer:
  ApplicationUser : IdentityUser
	+ IsActive (bool)
	+ EmployeeId (Guid) — FK to Domain Employee

Domain layer:
  Employee (no Identity dependency)
	+ Id (Guid)
	+ IdentityId (string) — stored but not an FK in Domain
```

The `ApplicationUser` lives in Infrastructure. The `Employee` lives in Domain. They are linked via `IdentityId` / `EmployeeId` through an Application-layer mapping use case.

### Recommendation
- `ApplicationUser` in `NovaLeave.Infrastructure.Identity` extends `IdentityUser` with `IsActive` and `EmployeeId`.
- `Employee` in `NovaLeave.Domain.Entities` has no Identity reference.
- Application handlers resolve the mapping when needed.

### Risks
- Slightly more complex than a single entity, but constitutionally required.

---

## 6. Post-Authentication Redirection Logic

### Question
How to implement role-based default redirection and ReturnUrl validation securely?

### Findings

**ReturnUrl validation** — must prevent open redirect attacks:

```csharp
if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
{
	return Redirect(returnUrl);
}
```

**Role-based default:**

```csharp
if (await userManager.IsInRoleAsync(user, "User"))
	return RedirectToAction("Index", "EmployeeDashboard");

if (await userManager.IsInRoleAsync(user, "Approver"))
	return RedirectToAction("Index", "ApproverDashboard");
```

Per spec D-007: Users with `User` role (including dual-role) → User Dashboard. Only-Approver → Approver Dashboard.

### Recommendation
- Validate `ReturnUrl` with `Url.IsLocalUrl()` to prevent open redirect.
- Check roles in order: User first (dual-role defaults to User), then Approver-only.
- Already-authenticated users hitting `/Account/Login` are redirected immediately (FR-014).

### Risks
- None significant with `IsLocalUrl` validation.

---

## 7. Audit Logging Architecture

### Question
How to structure authentication audit logging without coupling to the controller?

### Findings

Define an `IAuthenticationAuditService` contract in Application:

```csharp
public interface IAuthenticationAuditService
{
	Task RecordLoginSuccessAsync(string actorId, string correlationId, CancellationToken ct);
	Task RecordLoginFailureAsync(string? actorId, string reason, string correlationId, CancellationToken ct);
	Task RecordLogoutAsync(string actorId, string correlationId, CancellationToken ct);
}
```

Implementation in Infrastructure writes to `AuditRecord` table. The controller calls the service after each auth outcome.

### Recommendation
- Contract in Application, implementation in Infrastructure.
- Reasons are internal categories: `InvalidPassword`, `InactiveAccount`, `LockedOut`, `UnknownEmail`.
- Correlation ID from HTTP request pipeline (`HttpContext.TraceIdentifier`).
- Never log the submitted password.

### Risks
- Audit write failure should not block login. Use try/catch with Serilog fallback logging.

---

## Summary of Decisions

| Topic | Decision | Rationale |
|-------|----------|-----------|
| Cookie config | `ExpireTimeSpan` 10h, sliding enabled | Balance security/UX |
| Idle timeout | Custom `OnValidatePrincipal` with `TimeProvider` | No native idle timeout |
| Stamp validation | 5-minute interval | Balance security/performance |
| Rate limiting | Built-in `System.Threading.RateLimiting`, IP-partitioned | No external dependency |
| Identity/Domain split | `ApplicationUser` in Infrastructure, `Employee` in Domain | Constitution §7.1 |
| Redirection | `IsLocalUrl` + role-ordered check | Prevent open redirect |
| Audit | `IAuthenticationAuditService` in Application | Clean Architecture |
| Time abstraction | `TimeProvider.System` / `FakeTimeProvider` | Constitution §2.VI |
