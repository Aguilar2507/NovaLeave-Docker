namespace NovaLeave.Application.Features.VacationRequest.Edit;

// T602: Command for editing a Pending vacation request (Phase 8, FR-009).
// Only Pending requests can be edited; balance/overlap re-checked atomically.
public record EditRequestCommand(
    Guid RequestId,
    DateOnly StartDate,
    DateOnly EndDate,
    string Reason,
    string CorrelationId);
