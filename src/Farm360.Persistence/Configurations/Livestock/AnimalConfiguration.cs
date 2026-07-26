using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Livestock;

/// <summary>
/// EF Core Fluent API configuration for the Animal aggregate root.
/// Constitution §3.1 Infrastructure Layer: Fluent API ONLY — no data annotations on domain entities.
/// Constitution §4.2 Database Naming:
///   - Table: PascalCase plural → app.Animals
///   - FK:    {Entity}Id
///   - Index: IX_{Table}_{Columns}
///   - UQ:    UQ_{Table}_{Columns}
///   - CK:    CK_{Table}_{Column}
///
/// F360-MTA-2026-001 Layer 4: UQ_Animals_TenantId_TagId scoped to TenantId+TagId
///   so two tenants CAN use the same TagId — intended behavior.
/// </summary>
public sealed class AnimalConfiguration : IEntityTypeConfiguration<Animal>
{
    public void Configure(EntityTypeBuilder<Animal> builder)
    {
        builder.ToTable("Animals", "app");

        builder.HasKey(a => a.Id);

        // ── Concurrency token ──────────────────────────────────────────────────
        builder.Property(a => a.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        // ── Tenant isolation (required by multi-tenancy) ───────────────────────
        builder.Property(a => a.TenantId)
            .IsRequired();

        // ── Farm / Shed references ────────────────────────────────────────────
        builder.Property(a => a.FarmId)
            .IsRequired();

        // ── Owned Value Object: AnimalTag ─────────────────────────────────────
        // EF Core Owned Entity maps TagId and TagType as columns on this table.
        builder.OwnsOne(a => a.Tag, tagBuilder =>
        {
            tagBuilder.Property(t => t.TagId)
                .HasColumnName("TagId")
                .HasMaxLength(50)
                .IsRequired();

            tagBuilder.Property(t => t.TagType)
                .HasColumnName("TagType")
                .HasConversion<int>()
                .IsRequired();
        });

        // ── Unique constraint: UQ_Animals_TenantId_TagId (filtered — IsDeleted = 0) ────
        // F360-MTA-2026-001 Layer 4 — Constitution §4.2 UQ naming.
        // This index cannot be expressed purely via HasIndex on owned-type shadow properties.
        // It is created as raw SQL in the migration:
        //   CREATE UNIQUE INDEX UQ_Animals_TenantId_TagId
        //     ON app.Animals (TenantId, TagId) WHERE IsDeleted = 0
        // See: Livestock_AddAnimalAggregate migration — migrationBuilder.Sql()

        // ── Classification ─────────────────────────────────────────────────────
        builder.Property(a => a.Species)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(a => a.BreedName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.Sex)
            .HasConversion<int>()
            .IsRequired();

        // Check constraint: Sex must be 1 (Male) or 2 (Female)
        builder.ToTable(tb => tb.HasCheckConstraint("CK_Animals_Sex", "[Sex] IN (1, 2)"));

        // ── Dates ──────────────────────────────────────────────────────────────
        builder.Property(a => a.DateOfBirth).IsRequired();

        builder.Property(a => a.AcquisitionType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(a => a.AcquisitionDate).IsRequired();

        // ── Financial ──────────────────────────────────────────────────────────
        builder.Property(a => a.AcquisitionPriceBdt)
            .HasPrecision(14, 2)
            .IsRequired(false);

        builder.Property(a => a.SalePriceBdt)
            .HasPrecision(14, 2)
            .IsRequired(false);

        builder.Property(a => a.SaleWeightKg)
            .HasPrecision(8, 2)
            .IsRequired(false);

        builder.Property(a => a.BuyerName)
            .HasMaxLength(200)
            .IsRequired(false);

        // ── Status ─────────────────────────────────────────────────────────────
        builder.Property(a => a.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(a => a.DisposalReason)
            .HasConversion<int>()
            .IsRequired(false);

        builder.Property(a => a.QuarantineReason)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(a => a.Notes)
            .HasMaxLength(2000)
            .IsRequired(false);

        // ── Denormalized weight fields (updated by event handler) ─────────────
        builder.Property(a => a.LatestWeightKg)
            .HasPrecision(8, 2)
            .IsRequired(false);

        builder.Property(a => a.AdgKgPerDay)
            .HasPrecision(8, 3)
            .IsRequired(false);

        // ── Soft delete columns (AuditableEntity) ─────────────────────────────
        builder.Property(a => a.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(a => a.DeletedAtUtc).IsRequired(false);
        builder.Property(a => a.DeletedBy).IsRequired(false);

        // ── Audit columns ──────────────────────────────────────────────────────────────────
        builder.Property(a => a.CreatedAtUtc).IsRequired();
        builder.Property(a => a.CreatedBy).IsRequired();        // Guid — non-nullable
        builder.Property(a => a.ModifiedAtUtc).IsRequired(false);
        builder.Property(a => a.ModifiedBy).IsRequired(false);  // Guid? — nullable

        // Navigation properties
        builder.HasMany(x => x.WeightRecords)
            .WithOne()
            .HasForeignKey(x => x.AnimalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.BreedingRecords)
            .WithOne()
            .HasForeignKey(x => x.AnimalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Photos)
            .WithOne()
            .HasForeignKey(x => x.AnimalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Movements)
            .WithOne()
            .HasForeignKey(x => x.AnimalId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes for common query patterns ──────────────────────────────────
        builder.HasIndex(a => new { a.TenantId, a.Status })
            .HasDatabaseName("IX_Animals_TenantId_Status");

        builder.HasIndex(a => new { a.TenantId, a.FarmId })
            .HasDatabaseName("IX_Animals_TenantId_FarmId");

        builder.HasIndex(a => new { a.TenantId, a.Species })
            .HasDatabaseName("IX_Animals_TenantId_Species");

        builder.HasIndex(a => a.IsDeleted)
            .HasDatabaseName("IX_Animals_IsDeleted");
    }
}
