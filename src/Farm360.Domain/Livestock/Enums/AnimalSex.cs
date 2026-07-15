namespace Farm360.Domain.Livestock.Enums;

/// <summary>
/// Biological sex of the animal.
/// Constitution §4.2: Check constraint CK_Animals_Sex enforced in DB.
/// </summary>
public enum AnimalSex
{
    Male = 1,
    Female = 2
}
