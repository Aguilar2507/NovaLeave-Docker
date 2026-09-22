# Contracts: Login and Authentication

**Feature Branch**: `004-login-authentication`  
**Date**: 2026-07-28  
**Plan Reference**: [plan.md](./plan.md)  
**Research Reference**: [research.md](./research.md)  
**Common Artifacts**: [data-model.md](../common/data-model.md) | [security.md](../common/security.md)

---

## Overview

This document defines the Application-layer contracts (interfaces), commands, and ViewModels for the login and authentication feature. All contracts follow Clean Architecture: defined in Application, implemented in Infrastructure, consumed by Presentation.

---

## 1. Application Contracts (Interfaces)

### IAuthenticationAuditService

Records immutable audit entries for all authentication events.

```csharp
namespace NovaLeave.Application.Features.Authentication.Contracts;

/// <summary>
/// Records authentication-related audit events.
/// Implementation in Infrastructure writes to the AuditRecord table.
/// </summary>
public interface IAuthenticationAuditService
{
	/// <summary>
	/// Records a successful login event.
	/// </summary>
	Task RecordLoginSuccessAsync(
		string actorId,
		string correlationId,
		string requestId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Records a failed login event with an internal reason category.
	/// Reason values: "InvalidPassword", "InactiveAccount", "LockedOut", "UnknownEmail".
	/// MUST NOT include the submitted password.
	/// </summary>
	Task RecordLoginFailureAsync(
		string? actorId,
		string reason,
		string correlationId,
		string requestId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Records a logout event.
	/// </summary>
	Task RecordLogoutAsync(
		string actorId,
		string correlationId,
		string requestId,
		CancellationToken cancellationToken = default);
}
```

**Constitution compliance**: §8 (audit fields: timestamp_utc, actor_id, action, result, correlation_id, request_id). §7.3 (no passwords in logs).

---

### IAccountStatusValidator

Validates that an account is `Active` before allowing authentication or state-changing operations.

```csharp
namespace NovaLeave.Application.Features.Authentication.Contracts;

/// <summary>
/// Validates account active status during authentication and session revalidation.
/// Implementation resolves ApplicationUser from Infrastructure.
/// </summary>
public interface IAccountStatusValidator
{
	/// <summary>
	/// Returns true if the account identified by <paramref name="identityId"/> is Active.
	/// Returns false if the account does not exist or is Inactive.
	/// </summary>
	Task<bool> IsActiveAsync(string identityId, CancellationToken cancellationToken = default);
}
```

**Constitution compliance**: §4.3 (Active/Inactive status), §5 invariant 12 (revalidate active status before state changes).

---

### IDefaultDashboardResolver

Determines the role-appropriate default dashboard URL for post-authentication redirection.

```csharp
namespace NovaLeave.Application.Features.Authentication.Contracts;

/// <summary>
/// Resolves the default dashboard path based on the user's roles.
/// User role (including dual-role) → User Dashboard.
/// Approver-only → Approver Dashboard.
/// </summary>
public interface IDefaultDashboardResolver
{
	/// <summary>
	/// Returns the default redirect path for the given user.
	/// </summary>
	Task<string> GetDefaultDashboardPathAsync(
		string identityId,
		CancellationToken cancellationToken = default);
}
```

**Spec reference**: D-007 (role-based default redirection), FR-013.

---

## 2. Commands

### LoginCommand

```csharp
namespace NovaLeave.Application.Features.Authentication.Login;

/// <summary>
/// Command to authenticate a user with email and password.
/// Does NOT include anti-forgery token (handled by MVC framework).
/// </summary>
public sealed record LoginCommand(
	string Email,
	string Password,
	string? ReturnUrl);
```

### LoginCommandResult

```csharp
namespace NovaLeave.Application.Features.Authentication.Login;

public sealed record LoginCommandResult(
	bool Succeeded,
	string? RedirectUrl,
	string? ErrorMessage);
```

### LogoutCommand

```csharp
namespace NovaLeave.Application.Features.Authentication.Logout;

/// <summary>
/// Command to sign out the current user and invalidate the session cookie.
/// </summary>
public sealed record LogoutCommand(string ActorId);
```

---

## 3. Validators

### LoginCommandValidator

```csharp
namespace NovaLeave.Application.Features.Authentication.Login;

using FluentValidation;

/// <summary>
/// Input validation only. Business rules (account existence, status, lockout)
/// are checked in the handler, not here.
/// </summary>
public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
	public LoginCommandValidator()
	{
		RuleFor(x => x.Email)
			.NotEmpty().WithMessage("Email is required.")
			.EmailAddress().WithMessage("Invalid email format.")
			.MaximumLength(256);

		RuleFor(x => x.Password)
			.NotEmpty().WithMessage("Password is required.")
			.MaximumLength(128);
	}
}
```

**Constitution compliance**: §2.IV (input validation via FluentValidation), §7.2 (FluentValidation resolved through DI, invoked with `ValidateAsync`).

---

## 4. ViewModels (Presentation Layer)

### LoginViewModel

```csharp
namespace NovaLeave.Presentation.Web.ViewModels;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Dedicated ViewModel for the Login form. Contains ONLY the fields
/// needed for authentication. Anti-forgery token is handled by the
/// Razor Tag Helper, not as a model property.
/// </summary>
public sealed class LoginViewModel
{
	[Required(ErrorMessage = "El correo electrónico es obligatorio.")]
	[EmailAddress(ErrorMessage = "Formato de correo electrónico inválido.")]
	[MaxLength(256)]
	[Display(Name = "Correo electrónico")]
	public string Email { get; set; } = string.Empty;

	[Required(ErrorMessage = "La contraseña es obligatoria.")]
	[DataType(DataType.Password)]
	[MaxLength(128)]
	[Display(Name = "Contraseña")]
	public string Password { get; set; } = string.Empty;

	/// <summary>
	/// Preserved return URL for post-authentication redirection.
	/// Validated server-side with Url.IsLocalUrl() to prevent open redirect.
	/// </summary>
	public string? ReturnUrl { get; set; }

	/// <summary>
	/// Generic error message displayed on authentication failure.
	/// Same message for all failure categories (SR-007).
	/// </summary>
	public string? ErrorMessage { get; set; }
}
```

**Constitution compliance**: §7.2 (dedicated ViewModel, no entity binding), §11.3 (accessible labels), §3.3 (user-visible messages in Spanish).

---

## 5. Infrastructure Entities (for reference)

### ApplicationUser

```csharp
namespace NovaLeave.Infrastructure.Identity;

using Microsoft.AspNetCore.Identity;

/// <summary>
/// Extends IdentityUser with application-specific properties.
/// Lives in Infrastructure — Domain MUST NOT depend on this.
/// (Constitution §7.1)
/// </summary>
public class ApplicationUser : IdentityUser
{
	/// <summary>
	/// Whether the account is active. Only Active accounts may authenticate.
	/// </summary>
	public bool IsActive { get; set; } = true;

	/// <summary>
	/// Link to the Domain Employee entity.
	/// </summary>
	public Guid? EmployeeId { get; set; }
}
```

---

## Contract Dependency Map

```text
Presentation (AccountController)
  └── Application
		├── LoginCommand / LoginCommandValidator / LoginCommandHandler
		├── LogoutCommand / LogoutCommandHandler
		├── IAuthenticationAuditService ──→ Infrastructure (AuthenticationAuditService)
		├── IAccountStatusValidator ──→ Infrastructure (AccountStatusValidator)
		└── IDefaultDashboardResolver ──→ Infrastructure (DefaultDashboardResolver)
```

---

## Referenced By

- [plan.md](./plan.md) — Phase 1 contracts
- [quickstart.md](./quickstart.md) — Implementation sequence
- [../common/data-model.md](../common/data-model.md) — AuditRecord entity definition
- [../common/security.md](../common/security.md) — SR-002, SR-007, SR-008
