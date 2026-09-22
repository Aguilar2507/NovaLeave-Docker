# Research: Vacation Request Management

**Feature Branch**: `001-vacation-request-core`  
**Date**: 2026-07-28  
**Plan Reference**: [plan.md](./plan.md)  
**Spec Reference**: [spec_001-vacation-request.md](../../.specify/specs/spec_001-vacation-request.md)  
**Common Artifacts**: [architecture.md](../common/architecture.md) | [security.md](../common/security.md) | [data-model.md](../common/data-model.md)

---

## Research Objectives

Phase 0 technical investigation for the vacation request management feature. Each topic covers findings, recommended approach, and risks.

---

## 1. Date Range Overlap Prevention with Database Constraints

### Question
How to enforce that no two `Pending`/`Approved` vacation requests for the same employee overlap, using both application-level validation and a database-level constraint in SQL Server with EF Core?

### Findings

SQL Server does not natively support PostgreSQL-style `EXCLUDE` constraints with range types. Alternatives:

**Option A — Check constraint with a scalar function (limited):**
Cannot reference other rows. Not suitable for overlap.

**Option B — Unique filtered index + trigger (complex):**
A trigger or indexed view could enforce it, but adds significant complexity.

**Option C — Application-level serialized check + unique index as safety net:**

1. **Application layer**: Query for overlapping requests in the same transaction before insert.
2. **Database layer**: Use a serializable transaction isolation level or a `HOLDLOCK` hint to prevent race conditions during the overlap check + insert.

```sql
-- Application-level query (in EF Core):
SELECT COUNT(1)
FROM VacationRequests
WHERE OwnerId = @ownerId
  AND Status IN ('Pending', 'Approved')
  AND StartDate <= @endDate
  AND EndDate >= @startDate;
```

**Option D — Serializable isolation for the create operation:**

```csharp
using var transaction = await dbContext.Database
	.BeginTransactionAsync(IsolationLevel.Serializable, ct);

var hasOverlap = await dbContext.VacationRequests
	.Where(r => r.OwnerId == ownerId
		&& (r.Status == RequestStatus.Pending || r.Status == RequestStatus.Approved)
		&& r.StartDate <= endDate
		&& r.EndDate >= startDate)
	.AnyAsync(ct);

if (hasOverlap)
	throw new OverlappingRequestException();

dbContext.VacationRequests.Add(newRequest);
await dbContext.SaveChangesAsync(ct);
await transaction.CommitAsync(ct);
```

### Recommendation
- **Primary**: Serializable transaction for the create/edit operation ensures atomicity of overlap check + insert.
- **Secondary**: Composite index on `(OwnerId, Status, StartDate, EndDate)` for query performance.
- **Application validation**: Pre-check before entering the transaction for fast user feedback.
- Document as D-003 in the spec.

### Risks
- Serializable transactions hold broader locks; acceptable for the low-frequency create operation.
- Must handle `DbUpdateException` from concurrency conflicts gracefully.

---

## 2. Working-Day Calculation

### Question
How to calculate vacation duration in working days, excluding weekends and configurable public holidays?

### Findings

**Option A — Custom implementation:**

```csharp
public class WorkingDayCalculator : IWorkingDayCalculator
{
	private readonly IHolidayCalendar _holidays;

	public WorkingDayCalculator(IHolidayCalendar holidays) => _holidays = holidays;

	public async Task<int> CalculateAsync(DateOnly start, DateOnly end, CancellationToken ct)
	{
		var holidays = await _holidays.GetHolidaysAsync(start, end, ct);
		var count = 0;

		for (var date = start; date <= end; date = date.AddDays(1))
		{
			if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
				continue;
			if (holidays.Contains(date))
				continue;
			count++;
		}

		return count;
	}
}
```

**Option B — NuGet library (e.g., Nager.Date, PublicHoliday):**
Provides country-specific holidays but adds external dependency. For MVP with a configurable calendar, custom is simpler.

### Holiday Calendar

```csharp
public interface IHolidayCalendar
{
	Task<IReadOnlySet<DateOnly>> GetHolidaysAsync(
		DateOnly from, DateOnly to, CancellationToken ct);
}
```

Implementation reads from a `PublicHolidays` database table or configuration. Cacheable since holidays change infrequently.

### Recommendation
- Custom `WorkingDayCalculator` + `IHolidayCalendar` with DB-backed holidays.
- Cache holidays per year (in-memory, invalidated on change).
- No external library for MVP — simpler, fewer dependencies (Constitution §2.II).

### Risks
- Holiday data must be pre-loaded. Missing holidays = incorrect day counts.
- Leap years and edge cases covered by unit tests.

---

## 3. Background Job for Auto-Expiry

### Question
How to implement automatic expiration of `Pending` requests that exceed the configured timeout?

### Findings

**Option A — `IHostedService` with periodic timer:**

```csharp
public class AutoExpiryBackgroundService : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly TimeProvider _timeProvider;

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));

		while (await timer.WaitForNextTickAsync(stoppingToken))
		{
			using var scope = _scopeFactory.CreateScope();
			var handler = scope.ServiceProvider
				.GetRequiredService<IExpireStaleRequestsHandler>();
			await handler.ExecuteAsync(stoppingToken);
		}
	}
}
```

**Option B — Hangfire:**
More features (retry, dashboard, scheduling) but adds significant dependency. Overkill for a single periodic job.

**Option C — Check on access (lazy expiry):**
When a request is accessed, check if it should be expired. Simple but doesn't guarantee timely expiration.

### Recommendation
- **IHostedService** with `PeriodicTimer` for MVP. Runs every 15 minutes.
- Uses `TimeProvider` for testability (Constitution §2.VI).
- The handler queries `Pending` requests where `ExpiryDate <= today`, transitions them to `Expired`, releases reserved days, and creates audit records — all in a single transaction per request.
- No Hangfire until a real need exists (Constitution §2.II).

### Risks
- If the application restarts, there's a brief window where expiry doesn't run. Acceptable for MVP.
- Must handle concurrent expiry + approval race: optimistic concurrency (`RowVersion`) ensures only one wins.

---

## 4. Optimistic Concurrency with EF Core

### Question
How to implement optimistic concurrency for `VacationRequest` to handle concurrent approve/reject/cancel/void operations?

### Findings

EF Core supports `[Timestamp]` / `rowversion` natively:

```csharp
public class VacationRequest
{
	// ... other properties

	[Timestamp]
	public byte[] RowVersion { get; set; } = null!;
}
```

EF Core configuration:

```csharp
builder.Property(r => r.RowVersion)
	.IsRowVersion();
```

On concurrent update, EF Core throws `DbUpdateConcurrencyException`. The handler catches it and returns a conflict result:

```csharp
try
{
	await dbContext.SaveChangesAsync(ct);
}
catch (DbUpdateConcurrencyException)
{
	// Another operation modified the request — reload and inform the user
	throw new ConcurrencyConflictException("The request was modified by another user.");
}
```

### Recommendation
- `RowVersion` (`byte[]` with `[Timestamp]`) on `VacationRequest`.
- Catch `DbUpdateConcurrencyException` in Application handlers.
- Return a user-friendly conflict message; do NOT silently retry.
- Test with two simultaneous approval attempts → exactly one succeeds (SC-006).

### Risks
- None significant. This is the standard EF Core pattern.

---

## 5. State Machine for VacationRequest

### Question
How to model the request lifecycle transitions in the Domain entity?

### Findings

Implement transitions as guarded methods on the entity:

```csharp
public class VacationRequest
{
	public RequestStatus Status { get; private set; }

	public void Approve(Guid approverId, TimeProvider timeProvider)
	{
		if (Status != RequestStatus.Pending)
			throw new InvalidStateTransitionException(Status, RequestStatus.Approved);
		if (approverId == OwnerId)
			throw new SelfApprovalException();

		Status = RequestStatus.Approved;
		ResolvedBy = approverId;
		ResolvedAt = timeProvider.GetUtcNow();
	}

	public void Reject(Guid approverId, string reason, TimeProvider timeProvider)
	{
		if (Status != RequestStatus.Pending)
			throw new InvalidStateTransitionException(Status, RequestStatus.Rejected);
		if (string.IsNullOrWhiteSpace(reason))
			throw new RejectionReasonRequiredException();

		Status = RequestStatus.Rejected;
		ResolvedBy = approverId;
		ResolvedAt = timeProvider.GetUtcNow();
		RejectionReason = reason;
	}

	public void Cancel()
	{
		if (Status != RequestStatus.Pending)
			throw new InvalidStateTransitionException(Status, RequestStatus.Cancelled);
		Status = RequestStatus.Cancelled;
	}

	public void Void(DateOnly today)
	{
		if (Status != RequestStatus.Approved)
			throw new InvalidStateTransitionException(Status, RequestStatus.Voided);
		if (StartDate <= today)
			throw new VacationAlreadyStartedException();
		Status = RequestStatus.Voided;
	}

	public void Expire(TimeProvider timeProvider)
	{
		if (Status != RequestStatus.Pending)
			throw new InvalidStateTransitionException(Status, RequestStatus.Expired);
		Status = RequestStatus.Expired;
		ResolvedAt = timeProvider.GetUtcNow();
	}
}
```

### Recommendation
- Transition methods on the entity itself (Constitution §2.IV: rich domain).
- Each method validates preconditions and throws predictable exceptions.
- `TimeProvider` injected for timestamp generation.
- No external state machine library needed for this simple lifecycle.

### Risks
- Must ensure all transitions are tested (positive + negative cases).

---

## 6. Balance Management — Reserve, Deduct, Restore

### Question
How to implement the balance reservation/deduction/restoration pattern described in D-004?

### Findings

The `Employee` entity manages balance:

```csharp
public class Employee
{
	public int Balance { get; private set; }         // Available days
	public int ReservedDays { get; private set; }    // Pending reservations

	public int AvailableBalance => Balance - ReservedDays;

	public void ReserveDays(int days)
	{
		if (days <= 0) throw new ArgumentException("Days must be positive.");
		if (AvailableBalance < days) throw new InsufficientBalanceException();
		ReservedDays += days;
	}

	public void ReleaseReservation(int days)
	{
		ReservedDays -= days;
		if (ReservedDays < 0) throw new InvalidOperationException("Reservation underflow.");
	}

	public void DeductDays(int days)
	{
		ReservedDays -= days;
		Balance -= days;
		if (Balance < 0) throw new NegativeBalanceException();
	}

	public void RestoreDays(int days)
	{
		Balance += days;
	}
}
```

**Lifecycle mapping:**
| Event | Balance Operation |
|-------|-------------------|
| Create (Pending) | `ReserveDays(n)` |
| Approve | `DeductDays(n)` — converts reservation to deduction |
| Reject / Cancel / Expire | `ReleaseReservation(n)` |
| Void (pre-start) | `RestoreDays(n)` |

### Recommendation
- Balance methods on `Employee` entity (single source of truth — Constitution §2.III).
- `AvailableBalance` = `Balance - ReservedDays` used for validation.
- All balance operations happen in the same transaction as the state transition.

### Risks
- Must ensure atomic transaction for transition + balance change + audit.
- Test for negative balance invariant on every path.

---

## 7. EF Core Configuration — Indexes and Performance

### Question
What indexes and configurations are needed for the VacationRequest and Employee tables?

### Findings

```csharp
public class VacationRequestConfiguration : IEntityTypeConfiguration<VacationRequest>
{
	public void Configure(EntityTypeBuilder<VacationRequest> builder)
	{
		builder.HasKey(r => r.Id);
		builder.Property(r => r.RowVersion).IsRowVersion();

		// Overlap query performance
		builder.HasIndex(r => new { r.OwnerId, r.Status, r.StartDate, r.EndDate })
			.HasFilter("[Status] IN (0, 1)"); // Pending, Approved

		// Approver queue
		builder.HasIndex(r => r.Status)
			.HasFilter("[Status] = 0"); // Pending

		// Auto-expiry query
		builder.HasIndex(r => new { r.Status, r.ExpiryDate })
			.HasFilter("[Status] = 0"); // Pending

		// Reason is sensitive
		builder.Property(r => r.Reason).HasMaxLength(1000);
		builder.Property(r => r.RejectionReason).HasMaxLength(1000);
	}
}
```

### Recommendation
- Filtered indexes for active-status queries.
- Composite index on `(OwnerId, Status, StartDate, EndDate)` for overlap checks.
- `AsNoTracking()` for read queries (Constitution §6).

### Risks
- Filtered index syntax is SQL Server-specific. Document for portability.

---

## Summary of Decisions

| Topic | Decision | Rationale |
|-------|----------|-----------|
| Overlap prevention | Serializable transaction + composite index | No native exclusion constraint in SQL Server |
| Working days | Custom calculator + DB-backed holiday calendar | Simple, no external dependency |
| Auto-expiry | IHostedService with PeriodicTimer (15 min) | No Hangfire overhead for single job |
| Concurrency | EF Core RowVersion | Standard pattern, simple |
| State machine | Guarded methods on entity | Rich domain, Constitution §2.IV |
| Balance | Reserve/Deduct/Restore on Employee entity | Single source of truth, Constitution §2.III |
| Indexes | Filtered composite indexes for overlap + expiry | Query performance |
