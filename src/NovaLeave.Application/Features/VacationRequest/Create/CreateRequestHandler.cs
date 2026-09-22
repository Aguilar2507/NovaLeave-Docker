using FluentValidation;
using NovaLeave.Application.Features.VacationRequest.Contracts;
using NovaLeave.Domain.Entities;

namespace NovaLeave.Application.Features.VacationRequest.Create;

// T112: Handler que orquesta la creación de una solicitud de vacaciones.
// Revalida identity/balance/overlap (FR-012), calcula días laborables, persiste atomicamente con AuditRecord (SR-008, FR-011).
public sealed class CreateRequestHandler
{
    private readonly IVacationRequestRepository _repository;
    private readonly IWorkingDayCalculator _workingDayCalculator;
    private readonly IStateTransitionAuditService _auditService;
    private readonly IValidator<CreateRequestCommand> _validator;
    private readonly TimeProvider _timeProvider;

    public CreateRequestHandler(
        IVacationRequestRepository repository,
        IWorkingDayCalculator workingDayCalculator,
        IStateTransitionAuditService auditService,
        IValidator<CreateRequestCommand> validator,
        TimeProvider timeProvider)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _workingDayCalculator = workingDayCalculator ?? throw new ArgumentNullException(nameof(workingDayCalculator));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<Domain.Entities.VacationRequest> Handle(
        CreateRequestCommand command,
        Employee employee,
        CancellationToken cancellationToken = default)
    {
        // 1. Validación de entrada (FluentValidation)
        var validationResult = await _validator.ValidateAsync(command, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        // 2. Revalidar identidad: el command.OwnerId debe coincidir con el employee provisto (FR-012, defense-in-depth)
        if (command.OwnerId != employee.Id)
        {
            throw new UnauthorizedAccessException("El OwnerId no coincide con el empleado autenticado.");
        }

        // 3. Calcular días laborables (excluyendo fines de semana y feriados)
        var workingDays = _workingDayCalculator.Count(command.StartDate, command.EndDate);
        if (workingDays <= 0)
        {
            throw new InvalidOperationException("El rango de fechas debe incluir al menos un día laborable.");
        }

        // 4. Verificar balance suficiente ANTES de verificar solapamiento (fail-fast)
        if (employee.Balance < workingDays)
        {
            throw new InvalidOperationException($"Balance insuficiente. Disponible: {employee.Balance}, Requerido: {workingDays}.");
        }

        // 5. Verificar overlap (atomico via Serializable transaction en repository)
        var hasOverlap = await _repository.ExistsOverlappingAsync(
            command.OwnerId,
            command.StartDate,
            command.EndDate,
            excludeRequestId: null,
            cancellationToken).ConfigureAwait(false);

        if (hasOverlap)
        {
            throw new InvalidOperationException("Ya existe una solicitud Pending o Approved que se solapa con las fechas ingresadas.");
        }

        // 6. Reservar días del balance (la entidad Employee maneja el invariante non-negative)
        employee.ReserveDays(workingDays);

        // 7. Crear la solicitud en estado Pending
        var expiryDate = command.StartDate.AddDays(30); // Política: expira 30 días después del StartDate
        var request = new Domain.Entities.VacationRequest(
            id: Guid.NewGuid(),
            ownerId: command.OwnerId,
            startDate: command.StartDate,
            endDate: command.EndDate,
            reason: command.Reason,
            requestedDays: workingDays,
            expiryDate: expiryDate);

        // 8. Persistir solicitud + employee (con balance actualizado) + audit record atomicamente
        await _repository.AddAsync(request, cancellationToken).ConfigureAwait(false);

        // Registrar transición (no-op -> Pending)
        await _auditService.RecordTransitionAsync(
            requestId: request.Id,
            fromStatus: RequestStatus.Pending, // convención: primera creación usa Pending como "from"
            toStatus: RequestStatus.Pending,
            actorId: employee.IdentityId,
            actorRole: "Employee",
            correlationId: command.CorrelationId,
            reason: null,
            requestId2: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return request;
    }
}
