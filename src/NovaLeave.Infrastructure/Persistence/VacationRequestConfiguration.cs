using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NovaLeave.Domain.Entities;

namespace NovaLeave.Infrastructure.Persistence;

// Configuración EF Core para VacationRequest.
// Aplica RowVersion (concurrencia optimista, FR-013) y un índice compuesto filtrado
// (OwnerId, Status, StartDate, EndDate) para acelerar el chequeo de solapamiento por dueño
// en estados Pending/Approved (D-003 de research.md).
public sealed class VacationRequestConfiguration : IEntityTypeConfiguration<VacationRequest>
{
    public void Configure(EntityTypeBuilder<VacationRequest> entity)
    {
        entity.HasKey(e => e.Id);

        entity.Property(e => e.OwnerId).IsRequired();
        entity.Property(e => e.StartDate).IsRequired();
        entity.Property(e => e.EndDate).IsRequired();

        entity.Property(e => e.Reason)
            .IsRequired()
            .HasMaxLength(500);

        entity.Property(e => e.Status)
            .IsRequired()
            .HasConversion<string>();

        entity.Property(e => e.RequestedDays).IsRequired();
        entity.Property(e => e.CreatedAt).IsRequired();
        entity.Property(e => e.ExpiryDate).IsRequired();

        entity.Property(e => e.RejectionReason).HasMaxLength(500);

        // Concurrencia optimista (FR-013)
        entity.Property(e => e.RowVersion).IsRowVersion();

        // Relación con Employee (Owner)
        entity.HasOne(e => e.Owner)
            .WithMany()
            .HasForeignKey(e => e.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relación con Employee (ResolvedBy - nullable)
        entity.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(e => e.ResolvedBy)
            .OnDelete(DeleteBehavior.Restrict);

        // Índices base
        entity.HasIndex(e => e.OwnerId);
        entity.HasIndex(e => e.Status);
        entity.HasIndex(e => new { e.StartDate, e.EndDate });

        // Índice compuesto filtrado para detección eficiente de solapamientos por empleado (D-003).
        // Nota: la exclusión real (invariante) se aplica en el handler mediante transacción
        // Serializable (research.md §1); este índice sólo optimiza el pre-check.
        entity.HasIndex(e => new { e.OwnerId, e.Status, e.StartDate, e.EndDate })
            .HasDatabaseName("IX_VacationRequests_Owner_Status_Range")
            .HasFilter("[Status] IN ('Pending', 'Approved')");
    }
}
