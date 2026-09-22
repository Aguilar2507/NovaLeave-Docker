using Microsoft.AspNetCore.Identity;
using NovaLeave.Domain.Entities;

namespace NovaLeave.Infrastructure.Identity;

// ApplicationUser extiende IdentityUser de ASP.NET Core Identity
// Mantiene relación 1:1 con Employee mediante EmployeeId
public class ApplicationUser : IdentityUser
{
    // FK a Employee (relación 1:1)
    public Guid? EmployeeId { get; set; }

    // Navigation property a la entidad Employee del dominio
    public Employee? Employee { get; set; }
}
