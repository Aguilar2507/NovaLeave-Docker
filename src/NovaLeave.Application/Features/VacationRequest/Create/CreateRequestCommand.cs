namespace NovaLeave.Application.Features.VacationRequest.Create;

// Comando para crear una solicitud de vacaciones (US1, T111).
// El handler revalidará todos los invariantes de negocio: identity, balance, solapamiento, fechas (FR-012).
public sealed record CreateRequestCommand(
    Guid OwnerId,
    DateOnly StartDate,
    DateOnly EndDate,
    string Reason,
    string CorrelationId);
