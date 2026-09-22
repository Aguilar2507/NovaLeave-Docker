namespace NovaLeave.Application.Features.VacationRequest.Cancel;

// T310: Command for cancelling a vacation request (US3, Phase 5).
// Owner can cancel own Pending request → Cancelled (final), reservation released.
public record CancelRequestCommand(
    Guid RequestId,
    string CorrelationId);
