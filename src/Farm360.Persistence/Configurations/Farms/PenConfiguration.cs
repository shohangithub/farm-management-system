using Farm360.Domain.Farms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Farms;

public class PenConfiguration : IEntityTypeConfiguration<Pen>
{
    public void Configure(EntityTypeBuilder<Pen> builder)
    {
        builder.ToTable("Pens", "app");

        builder.HasKey(p => p.Id);

        builder.HasIndex(p => new { p.TenantId, p.ShedId, p.PenNumber }).IsUnique();

        builder.Property(p => p.PenNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.PenName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.AnimalGroup)
            .HasMaxLength(100);

        builder.Property(p => p.Notes)
            .HasMaxLength(500);

        builder.HasOne<Shed>()
            .WithMany()
            .HasForeignKey(p => p.ShedId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
