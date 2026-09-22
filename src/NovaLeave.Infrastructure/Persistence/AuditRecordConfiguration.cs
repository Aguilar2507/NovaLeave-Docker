using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NovaLeave.Domain.Entities;

namespace NovaLeave.Infrastructure.Persistence;

// Configuración EF Core para AuditRecord (inmutable, extraída por T018).
public sealed class AuditRecordConfiguration : IEntityTypeConfiguration<AuditRecord>
{
    public void Configure(EntityTypeBuilder<AuditRecord> entity)
    {
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Timestamp).IsRequired();

        entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Result).IsRequired().HasMaxLength(50);
        entity.Property(e => e.CorrelationId).IsRequired().HasMaxLength(100);

        entity.Property(e => e.ActorId).HasMaxLength(450);
        entity.Property(e => e.ActorRole).HasMaxLength(100);
        entity.Property(e => e.EntityType).HasMaxLength(100);
        entity.Property(e => e.EntityId).HasMaxLength(100);
        entity.Property(e => e.Reason).HasMaxLength(500);
        entity.Property(e => e.RequestId).HasMaxLength(100);
        entity.Property(e => e.Details);

        entity.HasIndex(e => e.Timestamp);
        entity.HasIndex(e => e.ActorId);
        entity.HasIndex(e => e.Action);
    }
}
