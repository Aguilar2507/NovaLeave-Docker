namespace NovaLeave.Application.Features.VacationRequest.List;

// T510: Query for listing employee's own vacation requests with pagination (US5, Phase 7).
public record ListMyRequestsQuery(
    int Limit = 20,
    int Offset = 0);
