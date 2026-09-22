using NovaLeave.Application.Features.VacationRequest.Contracts;
using NovaLeave.Domain.Entities;

namespace NovaLeave.Application.Features.VacationRequest.List;

// T212: Handler for listing pending requests assigned to an approver (US2, Phase 4).
// Ordered by urgency (ExpiryDate ascending) with pagination.
public sealed class ListPendingForApproverHandler
{
    private readonly IVacationRequestRepository _repository;

    public ListPendingForApproverHandler(IVacationRequestRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<IReadOnlyList<Domain.Entities.VacationRequest>> Handle(
        ListPendingForApproverQuery query,
        CancellationToken cancellationToken = default)
    {
        // Query pending requests where the employee's AssignedApproverId matches
        var requests = await _repository.GetPendingForApproverAsync(
            query.ApproverId,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        return requests;
    }
}
