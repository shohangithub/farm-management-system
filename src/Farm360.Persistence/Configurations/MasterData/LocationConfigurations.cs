using Farm360.Domain.MasterData.Locations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.MasterData;

public class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.ToTable("Countries", "app");
        builder.HasKey(c => c.Id);
        builder.HasIndex(c => new { c.TenantId, c.Code }).IsUnique();
        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Code).IsRequired().HasMaxLength(10);
    }
}

public class DivisionConfiguration : IEntityTypeConfiguration<Division>
{
    public void Configure(EntityTypeBuilder<Division> builder)
    {
        builder.ToTable("Divisions", "app");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Name).IsRequired().HasMaxLength(100);
        
        builder.HasOne<Country>()
            .WithMany()
            .HasForeignKey(d => d.CountryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class DistrictConfiguration : IEntityTypeConfiguration<District>
{
    public void Configure(EntityTypeBuilder<District> builder)
    {
        builder.ToTable("Districts", "app");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Name).IsRequired().HasMaxLength(100);
        
        builder.HasOne<Division>()
            .WithMany()
            .HasForeignKey(d => d.DivisionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class UpazilaConfiguration : IEntityTypeConfiguration<Upazila>
{
    public void Configure(EntityTypeBuilder<Upazila> builder)
    {
        builder.ToTable("Upazilas", "app");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Name).IsRequired().HasMaxLength(100);
        
        builder.HasOne<District>()
            .WithMany()
            .HasForeignKey(u => u.DistrictId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class UnionConfiguration : IEntityTypeConfiguration<Union>
{
    public void Configure(EntityTypeBuilder<Union> builder)
    {
        builder.ToTable("Unions", "app");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Name).IsRequired().HasMaxLength(100);
        
        builder.HasOne<Upazila>()
            .WithMany()
            .HasForeignKey(u => u.UpazilaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class VillageConfiguration : IEntityTypeConfiguration<Village>
{
    public void Configure(EntityTypeBuilder<Village> builder)
    {
        builder.ToTable("Villages", "app");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Name).IsRequired().HasMaxLength(100);
        
        builder.HasOne<Union>()
            .WithMany()
            .HasForeignKey(v => v.UnionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
