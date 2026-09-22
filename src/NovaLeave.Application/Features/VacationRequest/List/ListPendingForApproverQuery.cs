namespace NovaLeave.Application.Features.VacationRequest.List;

// T212: Query for listing pending requests assigned to an approver (US2, Phase 4).
public record ListPendingForApproverQuery(
    Guid ApproverId,
    int PageNumber = 1,
    int PageSize = 20);
