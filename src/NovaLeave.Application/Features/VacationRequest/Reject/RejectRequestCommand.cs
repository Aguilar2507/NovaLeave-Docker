namespace NovaLeave.Application.Features.VacationRequest.Reject;

// T211: Command for rejecting a vacation request (US2, Phase 4).
// RejectionReason is mandatory (SR-006).
public record RejectRequestCommand(
    Guid RequestId,
    Guid ApproverId,
    string RejectionReason,
    string CorrelationId);
