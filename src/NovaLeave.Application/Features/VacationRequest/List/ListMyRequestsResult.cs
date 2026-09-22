using NovaLeave.Domain.Entities;

namespace NovaLeave.Application.Features.VacationRequest.List;

// T510: Result DTO for listing employee's own requests (US5, Phase 7).
// Excludes Reason from list per SR-010 (reason only visible in detail view).
public record ListMyRequestsResult(
    List<RequestSummaryDto> Requests,
    int CurrentBalance,
    int TotalCount);

public record RequestSummaryDto(
    Guid Id,
    Guid OwnerId,
    DateOnly StartDate,
    DateOnly EndDate,
    int RequestedDays,
    RequestStatus Status,
    DateTimeOffset CreatedAt,
    DateOnly ExpiryDate);
