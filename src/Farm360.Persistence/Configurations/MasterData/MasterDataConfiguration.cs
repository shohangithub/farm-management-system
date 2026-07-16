using Farm360.Domain.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.MasterData;

public class MasterDataConfiguration : IEntityTypeConfiguration<MasterDataEntry>
{
    public void Configure(EntityTypeBuilder<MasterDataEntry> builder)
    {
        builder.ToTable("MasterDataEntries", "app");

        builder.HasKey(m => m.Id);

        // A tenant cannot have two entries with the same type and code
        builder.HasIndex(m => new { m.TenantId, m.Type, m.Code }).IsUnique();

        builder.Property(m => m.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(m => m.Code)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(m => m.Description)
            .HasMaxLength(500);

        builder.Property(m => m.Type)
            .HasConversion<int>()
            .IsRequired();
    }
}
