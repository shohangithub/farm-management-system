using Farm360.Domain.Livestock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Livestock;

public class AnimalMovementConfiguration : IEntityTypeConfiguration<AnimalMovement>
{
    public void Configure(EntityTypeBuilder<AnimalMovement> builder)
    {
        builder.ToTable("AnimalMovements");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ShedId).IsRequired(false);
        builder.Property(x => x.PenId).IsRequired(false);
        builder.Property(x => x.PlacedAtUtc).IsRequired();
        builder.Property(x => x.PlacedBy).IsRequired();
        builder.Property(x => x.RemovedAtUtc).IsRequired(false);
        builder.Property(x => x.RemovedBy).IsRequired(false);
        builder.Property(x => x.TransferReason).HasMaxLength(255).IsRequired(false);

        // Standard audits
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.CreatedBy).IsRequired();
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);

        // Concurrency
        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        // Index on AnimalId for fast child query
        builder.HasIndex(x => x.AnimalId);
        
        // Ensure fast lookup for active placements
        builder.HasIndex(x => new { x.AnimalId, x.RemovedAtUtc });
    }
}
