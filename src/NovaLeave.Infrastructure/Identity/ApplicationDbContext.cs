using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NovaLeave.Domain.Entities;
using NovaLeave.Infrastructure.Persistence;

namespace NovaLeave.Infrastructure.Identity;

// ApplicationDbContext es el DbContext principal de la aplicación
// Extiende IdentityDbContext<ApplicationUser> para incluir las tablas de Identity
// y expone las entidades del dominio (Employee, AuditRecord, VacationRequest)
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // DbSets para entidades del dominio compartido (common/data-model.md)
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();
    public DbSet<VacationRequest> VacationRequests => Set<VacationRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplica todas las IEntityTypeConfiguration<T> del ensamblado Infrastructure
        // (EmployeeConfiguration, VacationRequestConfiguration, AuditRecordConfiguration).
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(EmployeeConfiguration).Assembly);

        // Configuración de ApplicationUser -> Employee (queda inline porque ApplicationUser vive en Identity/)
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.HasOne(u => u.Employee)
                .WithOne()
                .HasForeignKey<ApplicationUser>(u => u.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
