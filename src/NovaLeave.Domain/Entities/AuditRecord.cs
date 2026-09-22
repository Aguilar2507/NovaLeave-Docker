namespace NovaLeave.Domain.Entities;

// Entidad inmutable que representa un registro de auditoría
// No permite actualizaciones ni eliminaciones después de su creación
public class AuditRecord
{
    public Guid Id { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }
    public string? ActorId { get; private set; }
    public string? ActorRole { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string? EntityType { get; private set; }
    public string? EntityId { get; private set; }
    public string Result { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public string? RequestId { get; private set; }
    public string? Details { get; private set; }

    // Constructor para EF Core
    private AuditRecord()
    {
    }

    public AuditRecord(
        string action,
        string result,
        string correlationId,
        string? actorId = null,
        string? actorRole = null,
        string? entityType = null,
        string? entityId = null,
        string? reason = null,
        string? requestId = null,
        string? details = null)
    {
        Id = Guid.NewGuid();
        Timestamp = DateTimeOffset.UtcNow;
        ActorId = actorId;
        ActorRole = actorRole;
        Action = action ?? throw new ArgumentNullException(nameof(action));
        EntityType = entityType;
        EntityId = entityId;
        Result = result ?? throw new ArgumentNullException(nameof(result));
        Reason = reason;
        CorrelationId = correlationId ?? throw new ArgumentNullException(nameof(correlationId));
        RequestId = requestId;
        Details = details;

        ValidateInvariants();
    }

    private void ValidateInvariants()
    {
        // Invariante: no debe contener datos sensibles como passwords o tokens
        // Esta validación se haría en tiempo de ejecución o con reglas adicionales
        // Por ahora, solo validamos que los campos obligatorios no estén vacíos
        if (string.IsNullOrWhiteSpace(Action))
        {
            throw new ArgumentException("Action no puede estar vacío.", nameof(Action));
        }

        if (string.IsNullOrWhiteSpace(Result))
        {
            throw new ArgumentException("Result no puede estar vacío.", nameof(Result));
        }

        if (string.IsNullOrWhiteSpace(CorrelationId))
        {
            throw new ArgumentException("CorrelationId no puede estar vacío.", nameof(CorrelationId));
        }
    }
}
