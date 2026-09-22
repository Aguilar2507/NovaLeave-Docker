using NovaLeave.Application.Features.VacationRequest.Contracts;
using NovaLeave.Domain.Entities;

namespace NovaLeave.Application.Features.VacationRequest.List;

// T510: Handler for listing employee's own vacation requests with pagination (US5, Phase 7).
// Returns own requests only, ordered by CreatedAt DESC, with limit/offset pagination (SC-007).
public sealed class ListMyRequestsHandler
{
    private readonly IVacationRequestRepository _repository;

    public ListMyRequestsHandler(IVacationRequestRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<ListMyRequestsResult> Handle(
        ListMyRequestsQuery query,
        Employee employee,
        CancellationToken cancellationToken)
    {
        // Fetch all requests for this employee
        var allRequests = await _repository.GetByOwnerAsync(employee.Id, cancellationToken);

        // Order by CreatedAt descending (most recent first)
        var orderedRequests = allRequests.OrderByDescending(r => r.CreatedAt).ToList();

        // Apply pagination
        var paginatedRequests = orderedRequests
            .Skip(query.Offset)
            .Take(query.Limit)
            .Select(r => new RequestSummaryDto(
                r.Id,
                r.OwnerId,
                r.StartDate,
                r.EndDate,
                r.RequestedDays,
                r.Status,
                r.CreatedAt,
                r.ExpiryDate))
            .ToList();

        return new ListMyRequestsResult(
            paginatedRequests,
            employee.Balance,
            orderedRequests.Count);
    }
}
