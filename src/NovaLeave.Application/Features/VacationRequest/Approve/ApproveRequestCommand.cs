namespace NovaLeave.Application.Features.VacationRequest.Approve;

// T210: Command for approving a vacation request (US2, Phase 4).
// ApproverId and RequestId come from the controller/authorization context.
public record ApproveRequestCommand(
    Guid RequestId,
    Guid ApproverId,
    string CorrelationId);
