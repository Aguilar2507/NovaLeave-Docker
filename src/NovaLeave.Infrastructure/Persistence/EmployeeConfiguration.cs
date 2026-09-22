using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NovaLeave.Domain.Entities;

namespace NovaLeave.Infrastructure.Persistence;

// Configuración EF Core para Employee (extraída de ApplicationDbContext por T018).
public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> entity)
    {
        entity.HasKey(e => e.Id);

        entity.Property(e => e.IdentityId)
            .IsRequired()
            .HasMaxLength(450); // Coincide con IdentityUser.Id

        entity.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        entity.Property(e => e.Email)
            .IsRequired()
            .HasMaxLength(256);

        entity.HasIndex(e => e.Email).IsUnique();
        entity.HasIndex(e => e.IdentityId).IsUnique();

        entity.Property(e => e.Balance).IsRequired();

        entity.Property(e => e.Status)
            .IsRequired()
            .HasConversion<string>();

        entity.Property(e => e.CreatedAt).IsRequired();
        entity.Property(e => e.EmploymentStartDate).IsRequired();

        // Self-reference al approver asignado
        entity.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(e => e.AssignedApproverId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
