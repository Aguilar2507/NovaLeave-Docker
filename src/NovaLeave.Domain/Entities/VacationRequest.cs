namespace NovaLeave.Domain.Entities;

// Entidad que representa una solicitud de vacaciones con máquina de estados
public class VacationRequest
{
    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Employee Owner { get; private set; } = null!; // Navigation property
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public RequestStatus Status { get; private set; }
    public int RequestedDays { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public Guid? ResolvedBy { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateOnly ExpiryDate { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    // Constructor para EF Core
    private VacationRequest()
    {
    }

    public VacationRequest(
        Guid id,
        Guid ownerId,
        DateOnly startDate,
        DateOnly endDate,
        string reason,
        int requestedDays,
        DateOnly expiryDate)
    {
        Id = id;
        OwnerId = ownerId;
        StartDate = startDate;
        EndDate = endDate;
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        RequestedDays = requestedDays;
        ExpiryDate = expiryDate;
        Status = RequestStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;

        ValidateInvariants();
    }

    // State machine: Submit (implícito en constructor - estado inicial Pending)

    // Aprobar solicitud
    public void Approve(Guid approverId)
    {
        EnsurePendingStatus();

        Status = RequestStatus.Approved;
        ResolvedAt = DateTimeOffset.UtcNow;
        ResolvedBy = approverId;
    }

    // Rechazar solicitud
    public void Reject(Guid approverId, string rejectionReason)
    {
        EnsurePendingStatus();

        if (string.IsNullOrWhiteSpace(rejectionReason))
        {
            throw new ArgumentException("El motivo de rechazo es obligatorio.", nameof(rejectionReason));
        }

        Status = RequestStatus.Rejected;
        ResolvedAt = DateTimeOffset.UtcNow;
        ResolvedBy = approverId;
        RejectionReason = rejectionReason;
    }

    // Cancelar solicitud (solo el owner puede cancelar si está Pending)
    public void Cancel()
    {
        EnsurePendingStatus();

        Status = RequestStatus.Cancelled;
        ResolvedAt = DateTimeOffset.UtcNow;
    }

    // Anular solicitud (para correcciones administrativas)
    public void Void(Guid voidedBy, string reason)
    {
        // Solo solicitudes Approved pueden ser voided
        if (Status != RequestStatus.Approved)
        {
            throw new InvalidOperationException("Solo las solicitudes aprobadas pueden ser anuladas.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("El motivo de anulación es obligatorio.", nameof(reason));
        }

        Status = RequestStatus.Voided;
        ResolvedAt = DateTimeOffset.UtcNow;
        ResolvedBy = voidedBy;
        RejectionReason = reason; // Reutilizamos este campo para la razón de void
    }

    // Editar solicitud (FR-009: solo Pending pueden editarse)
    public void Edit(DateOnly newStartDate, DateOnly newEndDate, string newReason, int newRequestedDays)
    {
        // Solo Pending puede editarse
        if (Status != RequestStatus.Pending)
        {
            throw new InvalidOperationException("Solo las solicitudes en estado Pending pueden ser editadas.");
        }

        if (string.IsNullOrWhiteSpace(newReason))
        {
            throw new ArgumentException("El motivo es obligatorio.", nameof(newReason));
        }

        StartDate = newStartDate;
        EndDate = newEndDate;
        Reason = newReason;
        RequestedDays = newRequestedDays;

        // Re-validate new dates satisfy invariants
        ValidateInvariants();
    }

    // Marcar como expirada (proceso automático)
    public void Expire()
    {
        EnsurePendingStatus();

        Status = RequestStatus.Expired;
        ResolvedAt = DateTimeOffset.UtcNow;
    }

    // Editar solicitud (solo si está Pending)
    public void Edit(DateOnly newStartDate, DateOnly newEndDate, string newReason, int newRequestedDays, DateOnly newExpiryDate)
    {
        EnsurePendingStatus();

        StartDate = newStartDate;
        EndDate = newEndDate;
        Reason = newReason ?? throw new ArgumentNullException(nameof(newReason));
        RequestedDays = newRequestedDays;
        ExpiryDate = newExpiryDate;

        ValidateInvariants();
    }

    private void EnsurePendingStatus()
    {
        if (Status != RequestStatus.Pending)
        {
            throw new InvalidOperationException($"Esta operación solo es válida para solicitudes en estado Pending. Estado actual: {Status}");
        }
    }

    private void ValidateInvariants()
    {
        // Invariante: StartDate < EndDate
        if (StartDate >= EndDate)
        {
            throw new InvalidOperationException("La fecha de inicio debe ser anterior a la fecha de fin.");
        }

        // Invariante: StartDate debe ser estrictamente futura (> hoy)
        // Nota: Esta validación se hace contra DateOnly.FromDateTime(DateTime.UtcNow)
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (StartDate <= today)
        {
            throw new InvalidOperationException("La fecha de inicio debe ser estrictamente futura (posterior a hoy).");
        }

        // Invariante: RequestedDays > 0
        if (RequestedDays <= 0)
        {
            throw new InvalidOperationException("Los días solicitados deben ser mayor a cero.");
        }
    }

    public bool IsFinalState()
    {
        return Status == RequestStatus.Approved ||
               Status == RequestStatus.Rejected ||
               Status == RequestStatus.Cancelled ||
               Status == RequestStatus.Voided ||
               Status == RequestStatus.Expired;
    }
}
