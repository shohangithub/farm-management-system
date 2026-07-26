using Farm360.Domain.Livestock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Livestock;

public class AnimalBatchConfiguration : IEntityTypeConfiguration<AnimalBatch>
{
    public void Configure(EntityTypeBuilder<AnimalBatch> builder)
    {
        builder.ToTable("AnimalBatches", "app");
        builder.HasKey(b => b.Id);

        // Tenancy
        builder.HasQueryFilter(b => EF.Property<Guid>(b, "TenantId") == default || b.TenantId == default);
        builder.Property(b => b.TenantId).IsRequired();
        builder.HasIndex(b => b.TenantId);

        builder.Property(b => b.FarmId).IsRequired();
        builder.HasIndex(b => new { b.TenantId, b.FarmId });

        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(b => b.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(b => b.Notes)
            .HasMaxLength(1000);

        // Audit properties are handled globally by interceptor.
        // We configure relations via AnimalConfiguration for the 1:N side.
    }
}
