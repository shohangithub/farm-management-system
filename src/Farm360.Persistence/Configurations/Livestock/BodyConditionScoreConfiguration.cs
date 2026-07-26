using Farm360.Domain.Livestock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Livestock;

public class BodyConditionScoreConfiguration : IEntityTypeConfiguration<BodyConditionScore>
{
    public void Configure(EntityTypeBuilder<BodyConditionScore> builder)
    {
        builder.ToTable("BodyConditionScores", "app");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.AnimalId).IsRequired();
        builder.HasIndex(b => b.AnimalId);

        builder.Property(b => b.Score)
            .IsRequired()
            .HasPrecision(3, 2);

        builder.Property(b => b.RecordedDate).IsRequired();
        builder.Property(b => b.EvaluatorId).IsRequired();

        builder.Property(b => b.Notes)
            .HasMaxLength(500);

        builder.HasOne<Animal>()
            .WithMany(a => a.BcsRecords)
            .HasForeignKey(b => b.AnimalId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
