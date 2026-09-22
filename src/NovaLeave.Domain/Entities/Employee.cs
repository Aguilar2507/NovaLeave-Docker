namespace NovaLeave.Domain.Entities;

// Entidad que representa un empleado/usuario del sistema
public class Employee
{
    public Guid Id { get; private set; }
    public string IdentityId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public Guid? AssignedApproverId { get; private set; }
    public int Balance { get; private set; }
    public AccountStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateOnly EmploymentStartDate { get; private set; }

    // Constructor para EF Core
    private Employee()
    {
    }

    public Employee(
        Guid id,
        string identityId,
        string name,
        string email,
        DateOnly employmentStartDate,
        int initialBalance = 0,
        Guid? assignedApproverId = null)
    {
        Id = id;
        IdentityId = identityId ?? throw new ArgumentNullException(nameof(identityId));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Email = email ?? throw new ArgumentNullException(nameof(email));
        EmploymentStartDate = employmentStartDate;
        Balance = initialBalance;
        AssignedApproverId = assignedApproverId;
        Status = AccountStatus.Active;
        CreatedAt = DateTimeOffset.UtcNow;

        ValidateInvariants();
    }

    // Reserva días del balance (cuando se crea una solicitud pendiente)
    public void ReserveDays(int days)
    {
        EnsureActiveAccount();

        if (days <= 0)
        {
            throw new InvalidOperationException("La cantidad de días a reservar debe ser mayor a cero.");
        }

        if (Balance - days < 0)
        {
            throw new InvalidOperationException("Balance insuficiente para reservar los días solicitados.");
        }

        Balance -= days;
    }

    // Deduce días del balance (cuando se aprueba una solicitud)
    public void DeductDays(int days)
    {
        EnsureActiveAccount();

        if (days <= 0)
        {
            throw new InvalidOperationException("La cantidad de días a deducir debe ser mayor a cero.");
        }

        if (Balance - days < 0)
        {
            throw new InvalidOperationException("Balance insuficiente para deducir los días.");
        }

        Balance -= days;
    }

    // Restaura días al balance (cuando se rechaza/cancela/vence una solicitud)
    public void RestoreDays(int days)
    {
        if (days <= 0)
        {
            throw new InvalidOperationException("La cantidad de días a restaurar debe ser mayor a cero.");
        }

        Balance += days;
    }

    public void SetAssignedApprover(Guid? approverId)
    {
        // Invariante: un empleado no puede ser su propio aprobador
        if (approverId.HasValue && approverId.Value == Id)
        {
            throw new InvalidOperationException("Un empleado no puede ser su propio aprobador.");
        }

        AssignedApproverId = approverId;
    }

    public void UpdateIdentityId(string identityId)
    {
        IdentityId = identityId ?? throw new ArgumentNullException(nameof(identityId));
    }

    public void Deactivate()
    {
        Status = AccountStatus.Inactive;
    }

    public void Activate()
    {
        Status = AccountStatus.Active;
    }

    private void EnsureActiveAccount()
    {
        // Invariante: solo cuentas Active pueden realizar operaciones de cambio de estado
        if (Status != AccountStatus.Active)
        {
            throw new InvalidOperationException("Solo las cuentas activas pueden realizar esta operación.");
        }
    }

    private void ValidateInvariants()
    {
        // Invariante: Balance no puede ser negativo
        if (Balance < 0)
        {
            throw new InvalidOperationException("El balance no puede ser negativo.");
        }

        // Invariante: un empleado no puede ser su propio aprobador
        if (AssignedApproverId.HasValue && AssignedApproverId.Value == Id)
        {
            throw new InvalidOperationException("Un empleado no puede ser su propio aprobador.");
        }
    }
}
