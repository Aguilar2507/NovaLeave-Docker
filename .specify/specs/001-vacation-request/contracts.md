# Contracts: Vacation Request Management

**Feature Branch**: `001-vacation-request-core`  
**Date**: 2026-07-28  
**Plan Reference**: [plan.md](./plan.md)  
**Research Reference**: [research.md](./research.md)  
**Common Artifacts**: [data-model.md](../common/data-model.md) | [security.md](../common/security.md)

---

## Overview

Application-layer contracts, commands, queries, and ViewModels for the vacation request feature. Contracts defined in Application, implemented in Infrastructure, consumed by Presentation.

---

## 1. Application Contracts (Interfaces)

### IWorkingDayCalculator

```csharp
namespace NovaLeave.Application.Features.VacationRequest.Contracts;

/// <summary>
/// Calculates the number of working days between two dates,
/// excluding weekends and public holidays.
/// Server-side only — client-supplied day counts are never authoritative.
/// </summary>
public interface IWorkingDayCalculator
{
	/// <summary>
	/// Returns the count of working days in the inclusive range [start, end].
	/// </summary>
	Task<int> CalculateAsync(DateOnly start, DateOnly end, CancellationToken ct = default);
}
```

**Constitution compliance**: §5 invariant 13 (server-side working-day calculation).

---

### IHolidayCalendar

```csharp
namespace NovaLeave.Application.Features.VacationRequest.Contracts;

/// <summary>
/// Provides the set of public holidays for a given date range.
/// Backed by database or configuration. Cacheable.
/// </summary>
public interface IHolidayCalendar
{
	Task<IReadOnlySet<DateOnly>> GetHolidaysAsync(
		DateOnly from, DateOnly to, CancellationToken ct = default);
}
```

---

### IVacationRequestRepository

```csharp
namespace NovaLeave.Application.Features.VacationRequest.Contracts;

/// <summary>
/// Data access for VacationRequest entities.
/// Implementation in Infrastructure using EF Core.
/// </summary>
public interface IVacationRequestRepository
{
	Task<Domain.Entities.VacationRequest?> GetByIdAsync(
		Guid id, CancellationToken ct = default);

	/// <summary>
	/// Checks if any Pending or Approved request for the given owner
	/// overlaps with the specified date range.
	/// </summary>
	Task<bool> HasOverlappingRequestAsync(
		Guid ownerId, DateOnly startDate, DateOnly endDate,
		Guid? excludeRequestId = null,
		CancellationToken ct = default);

	Task AddAsync(Domain.Entities.VacationRequest request, CancellationToken ct = default);

	/// <summary>
	/// Returns paginated requests owned by the specified employee.
	/// Ordered by CreatedAt descending.
	/// </summary>
	Task<PaginatedResult<Domain.Entities.VacationRequest>> GetByOwnerAsync(
		Guid ownerId, int page, int pageSize, CancellationToken ct = default);

	/// <summary>
	/// Returns paginated Pending requests for the approver queue.
	/// </summary>
	Task<PaginatedResult<Domain.Entities.VacationRequest>> GetPendingForApproverAsync(
		int page, int pageSize, CancellationToken ct = default);

	/// <summary>
	/// Returns all Pending requests whose ExpiryDate has passed.
	/// Used by the auto-expiry background job.
	/// </summary>
	Task<IReadOnlyList<Domain.Entities.VacationRequest>> GetExpiredPendingRequestsAsync(
		DateOnly today, CancellationToken ct = default);

	Task SaveChangesAsync(CancellationToken ct = default);
}
```

---

### IStateTransitionAuditService

```csharp
namespace NovaLeave.Application.Features.VacationRequest.Contracts;

/// <summary>
/// Records audit entries for vacation request state transitions.
/// Writes to the shared AuditRecord table (see common/data-model.md).
/// </summary>
public interface IStateTransitionAuditService
{
	Task RecordTransitionAsync(
		Guid entityId,
		string entityType,
		string action,
		string actorId,
		string actorRole,
		string result,
		string correlationId,
		string? details = null,
		CancellationToken ct = default);
}
```

**Constitution compliance**: §8 (audit fields).

---

### PaginatedResult (shared)

```csharp
namespace NovaLeave.Application.Common;

/// <summary>
/// Generic paginated result wrapper.
/// </summary>
public sealed record PaginatedResult<T>(
	IReadOnlyList<T> Items,
	int TotalCount,
	int Page,
	int PageSize)
{
	public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
	public bool HasPreviousPage => Page > 1;
	public bool HasNextPage => Page < TotalPages;
}
```

---

## 2. Commands

### CreateRequestCommand

```csharp
namespace NovaLeave.Application.Features.VacationRequest.Create;

public sealed record CreateRequestCommand(
	Guid OwnerId,
	DateOnly StartDate,
	DateOnly EndDate,
	string Reason);
```

### CreateRequestResult

```csharp
public sealed record CreateRequestResult(
	bool Succeeded,
	Guid? RequestId,
	string? ErrorMessage);
```

### ApproveRequestCommand

```csharp
namespace NovaLeave.Application.Features.VacationRequest.Approve;

public sealed record ApproveRequestCommand(
	Guid RequestId,
	Guid ApproverId);
```

### RejectRequestCommand

```csharp
namespace NovaLeave.Application.Features.VacationRequest.Reject;

public sealed record RejectRequestCommand(
	Guid RequestId,
	Guid ApproverId,
	string Reason);
```

### CancelRequestCommand

```csharp
namespace NovaLeave.Application.Features.VacationRequest.Cancel;

/// <summary>
/// Employee cancels their own Pending request.
/// </summary>
public sealed record CancelRequestCommand(
	Guid RequestId,
	Guid OwnerId);
```

### VoidRequestCommand

```csharp
namespace NovaLeave.Application.Features.VacationRequest.Void;

/// <summary>
/// Employee voids their own Approved request (pre-start only).
/// </summary>
public sealed record VoidRequestCommand(
	Guid RequestId,
	Guid OwnerId);
```

### EditRequestCommand

```csharp
namespace NovaLeave.Application.Features.VacationRequest.Edit;

/// <summary>
/// Employee edits their own Pending request.
/// Full revalidation is required (dates, overlap, balance).
/// </summary>
public sealed record EditRequestCommand(
	Guid RequestId,
	Guid OwnerId,
	DateOnly StartDate,
	DateOnly EndDate,
	string Reason);
```

---

## 3. Queries

### ListMyRequestsQuery

```csharp
namespace NovaLeave.Application.Features.VacationRequest.List;

public sealed record ListMyRequestsQuery(
	Guid OwnerId,
	int Page = 1,
	int PageSize = 20);
```

### ListPendingForApproverQuery

```csharp
namespace NovaLeave.Application.Features.VacationRequest.List;

public sealed record ListPendingForApproverQuery(
	int Page = 1,
	int PageSize = 20);
```

---

## 4. Validators

### CreateRequestValidator

```csharp
namespace NovaLeave.Application.Features.VacationRequest.Create;

using FluentValidation;

public sealed class CreateRequestValidator : AbstractValidator<CreateRequestCommand>
{
	public CreateRequestValidator()
	{
		RuleFor(x => x.StartDate)
			.NotEmpty().WithMessage("La fecha de inicio es obligatoria.");

		RuleFor(x => x.EndDate)
			.NotEmpty().WithMessage("La fecha de fin es obligatoria.");

		RuleFor(x => x.EndDate)
			.GreaterThanOrEqualTo(x => x.StartDate)
			.WithMessage("La fecha de fin debe ser igual o posterior a la fecha de inicio.");

		RuleFor(x => x.Reason)
			.NotEmpty().WithMessage("El motivo es obligatorio.")
			.MaximumLength(1000).WithMessage("El motivo no puede exceder 1000 caracteres.");
	}
}
```

> **Note**: Business rules (future date check, overlap, balance) are validated in the handler and Domain entity, NOT in the FluentValidation validator. FluentValidation handles input format only (Constitution §2.IV).

### RejectRequestValidator

```csharp
namespace NovaLeave.Application.Features.VacationRequest.Reject;

using FluentValidation;

public sealed class RejectRequestValidator : AbstractValidator<RejectRequestCommand>
{
	public RejectRequestValidator()
	{
		RuleFor(x => x.Reason)
			.NotEmpty().WithMessage("El motivo de rechazo es obligatorio.")
			.MaximumLength(1000);
	}
}
```

### EditRequestValidator

```csharp
namespace NovaLeave.Application.Features.VacationRequest.Edit;

using FluentValidation;

public sealed class EditRequestValidator : AbstractValidator<EditRequestCommand>
{
	public EditRequestValidator()
	{
		RuleFor(x => x.StartDate).NotEmpty();
		RuleFor(x => x.EndDate).NotEmpty();
		RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate)
			.WithMessage("La fecha de fin debe ser igual o posterior a la fecha de inicio.");
		RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
	}
}
```

---

## 5. ViewModels (Presentation Layer)

### CreateRequestViewModel

```csharp
namespace NovaLeave.Presentation.Web.ViewModels;

using System.ComponentModel.DataAnnotations;

public sealed class CreateRequestViewModel
{
	[Required(ErrorMessage = "La fecha de inicio es obligatoria.")]
	[Display(Name = "Fecha de inicio")]
	public DateOnly? StartDate { get; set; }

	[Required(ErrorMessage = "La fecha de fin es obligatoria.")]
	[Display(Name = "Fecha de fin")]
	public DateOnly? EndDate { get; set; }

	[Required(ErrorMessage = "El motivo es obligatorio.")]
	[MaxLength(1000, ErrorMessage = "Máximo 1000 caracteres.")]
	[Display(Name = "Motivo")]
	public string Reason { get; set; } = string.Empty;

	// Read-only display fields
	public int AvailableBalance { get; set; }
	public string? ErrorMessage { get; set; }
}
```

### EditRequestViewModel

```csharp
public sealed class EditRequestViewModel
{
	public Guid RequestId { get; set; }

	[Required][Display(Name = "Fecha de inicio")]
	public DateOnly? StartDate { get; set; }

	[Required][Display(Name = "Fecha de fin")]
	public DateOnly? EndDate { get; set; }

	[Required][MaxLength(1000)][Display(Name = "Motivo")]
	public string Reason { get; set; } = string.Empty;

	public int AvailableBalance { get; set; }
	public string? ErrorMessage { get; set; }
}
```

### RejectRequestViewModel

```csharp
public sealed class RejectRequestViewModel
{
	public Guid RequestId { get; set; }
	public string OwnerName { get; set; } = string.Empty;
	public DateOnly StartDate { get; set; }
	public DateOnly EndDate { get; set; }
	public int RequestedDays { get; set; }

	[Required(ErrorMessage = "El motivo de rechazo es obligatorio.")]
	[MaxLength(1000)]
	[Display(Name = "Motivo de rechazo")]
	public string Reason { get; set; } = string.Empty;
}
```

### RequestListViewModel

```csharp
public sealed class RequestListViewModel
{
	public IReadOnlyList<RequestListItemViewModel> Requests { get; set; } = [];
	public int TotalCount { get; set; }
	public int Page { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
	public bool HasPreviousPage { get; set; }
	public bool HasNextPage { get; set; }
	public int AvailableBalance { get; set; }
}

public sealed class RequestListItemViewModel
{
	public Guid Id { get; set; }
	public DateOnly StartDate { get; set; }
	public DateOnly EndDate { get; set; }
	public int RequestedDays { get; set; }
	public string Status { get; set; } = string.Empty;
	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset? ResolvedAt { get; set; }
}
```

### RequestDetailViewModel

```csharp
public sealed class RequestDetailViewModel
{
	public Guid Id { get; set; }
	public string OwnerName { get; set; } = string.Empty;
	public DateOnly StartDate { get; set; }
	public DateOnly EndDate { get; set; }
	public int RequestedDays { get; set; }
	public string Reason { get; set; } = string.Empty;  // Visible only to owner + approver (SR-009)
	public string Status { get; set; } = string.Empty;
	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset? ResolvedAt { get; set; }
	public string? ResolvedByName { get; set; }
	public string? RejectionReason { get; set; }

	// UI control flags
	public bool CanApprove { get; set; }
	public bool CanReject { get; set; }
	public bool CanCancel { get; set; }
	public bool CanVoid { get; set; }
	public bool CanEdit { get; set; }
}
```

---

## 6. Domain Entities (signatures)

> Full entity definitions are in [data-model.md](../common/data-model.md). Below are the method signatures for the state machine and balance operations.

### VacationRequest — Transition Methods

```csharp
public void Approve(Guid approverId, TimeProvider timeProvider);
public void Reject(Guid approverId, string reason, TimeProvider timeProvider);
public void Cancel();
public void Void(DateOnly today);
public void Expire(TimeProvider timeProvider);
public void Edit(DateOnly newStart, DateOnly newEnd, string newReason, int newRequestedDays);
```

### Employee — Balance Methods

```csharp
public int AvailableBalance { get; }  // Balance - ReservedDays
public void ReserveDays(int days);
public void ReleaseReservation(int days);
public void DeductDays(int days);
public void RestoreDays(int days);
```

---

## Contract Dependency Map

```text
Presentation
  ├── EmployeeRequestsController
  │     └── Create, Edit, Cancel, Void, List handlers
  └── ApproverRequestsController
		└── Approve, Reject, ListPending handlers

Application
  ├── CreateRequestHandler ──→ IVacationRequestRepository, IWorkingDayCalculator, IStateTransitionAuditService
  ├── ApproveRequestHandler ──→ IVacationRequestRepository, IStateTransitionAuditService
  ├── RejectRequestHandler ──→ IVacationRequestRepository, IStateTransitionAuditService
  ├── CancelRequestHandler ──→ IVacationRequestRepository, IStateTransitionAuditService
  ├── VoidRequestHandler ──→ IVacationRequestRepository, IStateTransitionAuditService
  ├── EditRequestHandler ──→ IVacationRequestRepository, IWorkingDayCalculator, IStateTransitionAuditService
  ├── ListMyRequestsHandler ──→ IVacationRequestRepository
  ├── ListPendingHandler ──→ IVacationRequestRepository
  └── ExpireStaleRequestsHandler ──→ IVacationRequestRepository, IStateTransitionAuditService

Infrastructure
  ├── VacationRequestRepository : IVacationRequestRepository
  ├── WorkingDayCalculator : IWorkingDayCalculator
  ├── HolidayCalendar : IHolidayCalendar
  └── StateTransitionAuditService : IStateTransitionAuditService
```

---

## Referenced By

- [plan.md](./plan.md) — Phase 1 contracts
- [quickstart.md](./quickstart.md) — Implementation sequence
- [../common/data-model.md](../common/data-model.md) — VacationRequest, Employee, AuditRecord
- [../common/security.md](../common/security.md) — SR-001, SR-002, SR-003, SR-004, SR-010, SR-011
