using Farm360.Domain.Intelligence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Intelligence;

public class ActionableInsightConfiguration : IEntityTypeConfiguration<ActionableInsight>
{
    public void Configure(EntityTypeBuilder<ActionableInsight> builder)
    {
        builder.ToTable("ActionableInsights", "intelligence");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Message)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.ActionData)
            .HasMaxLength(4000);

        builder.HasIndex(x => x.FarmId);
        builder.HasIndex(x => x.AnimalId);
        builder.HasIndex(x => x.BatchId);
        
        // Ensure multitenancy filters are respected if they are applied globally, 
        // otherwise we can add it here if needed, but it's usually in DbContext.
    }
}
