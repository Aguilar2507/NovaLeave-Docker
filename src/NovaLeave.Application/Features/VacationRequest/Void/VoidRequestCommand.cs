namespace NovaLeave.Application.Features.VacationRequest.Void;

// T410: Command for voiding an approved vacation request (US4, Phase 6).
// Owner voids Approved request when StartDate > today → Voided (final), days returned.
public record VoidRequestCommand(
    Guid RequestId,
    string Reason,
    string CorrelationId);
