using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;

namespace Farm360.Application.Reporting.Definitions;

/// <summary>How reports name an animal: its tag, plus "(Sold 12-Nov-2026)" / "(Died …)" once it has
/// left the farm, so history rows for a sold or dead animal are never mistaken for a live one.</summary>
public static class AnimalLabels
{
    public static string Tag(Animal animal)
    {
        ArgumentNullException.ThrowIfNull(animal);
        if (!animal.HasLeftFarm)
            return animal.Tag.TagId;

        var what = animal.Status switch
        {
            AnimalStatus.Sold => "Sold",
            AnimalStatus.Dead => "Died",
            AnimalStatus.Slaughtered => "Slaughtered",
            _ => "Left",
        };
        return animal.ExitDate is { } d ? $"{animal.Tag.TagId} ({what} {d:dd-MMM-yyyy})" : $"{animal.Tag.TagId} ({what})";
    }
}
