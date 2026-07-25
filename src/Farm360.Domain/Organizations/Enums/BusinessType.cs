namespace Farm360.Domain.Organizations.Enums;

/// <summary>
/// Categorizes the nature of an organization's business.
/// Used in Organization CRUD and displayed in the Angular org form.
///
/// IMPORTANT: These integer values are stored in the database.
/// Do NOT reorder or change existing values without a data migration.
/// </summary>
public enum BusinessType
{
    Farm = 1,
    Supplier = 2,
    Buyer = 3,
    VeterinaryClinic = 4,
    Cooperative = 5
}
