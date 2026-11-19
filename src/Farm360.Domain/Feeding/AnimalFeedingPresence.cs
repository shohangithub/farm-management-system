using System.Globalization;
using System.Linq.Expressions;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;

namespace Farm360.Domain.Feeding;

/// <summary>
/// Which animals are fed on a given day — the one rule every feeding path uses (the daily job,
/// group membership, per-animal allocation, confirm/adjust guards and exit processing).
/// </summary>
/// <remarks>
/// <para><b>Farm rule — "fed on day D":</b> the animal was acquired on or before D, and it has no
/// exit date or D is before its exit date. The exit date is the sale date or the death date.</para>
/// <list type="bullet">
/// <item>On the exit day itself the animal is not fed by new or pending entries. An entry for the
/// exit day that was already confirmed stays: it really ate that morning.</item>
/// <item>Quarantined animals are on the farm and are fed.</item>
/// <item>Sold, dead or slaughtered animals are fed only on days before their exit date; with no
/// recorded exit date they are not fed at all. Transferred animals have left and are not fed.</item>
/// </list>
/// <para>The rule is written against the day, never against today's status: a sale recorded three
/// days late must still count the animal as fed on the days before the sale, and not after.</para>
/// </remarks>
public static class AnimalFeedingPresence
{
    /// <summary>The day the animal left the farm: its sale date, else its death date.</summary>
    public static DateOnly? ExitDate(Animal animal)
    {
        ArgumentNullException.ThrowIfNull(animal);
        return animal.SaleDate ?? animal.DeathDate;
    }

    /// <summary>The "fed on day D" rule as a query expression (translatable by EF Core).</summary>
    public static Expression<Func<Animal, bool>> FedOn(DateOnly day) =>
        a => a.AcquisitionDate <= day
             && (a.Status == AnimalStatus.Active
                 || a.Status == AnimalStatus.Quarantined
                 || a.Status == AnimalStatus.Sold
                 || a.Status == AnimalStatus.Dead
                 || a.Status == AnimalStatus.Slaughtered)
             && ((a.SaleDate != null && a.SaleDate > day)
                 || (a.SaleDate == null && a.DeathDate != null && a.DeathDate > day)
                 || (a.SaleDate == null && a.DeathDate == null
                     && (a.Status == AnimalStatus.Active || a.Status == AnimalStatus.Quarantined)));

    /// <summary>The same rule as <see cref="FedOn"/>, for an animal already in memory.</summary>
    public static bool IsFedOn(Animal animal, DateOnly day) => NotFedReason(animal, day) is null;

    /// <summary>
    /// Null when the animal is fed on <paramref name="day"/>; otherwise a sentence saying why not,
    /// fit to show to the user ("Cow-12 was sold on 13 Nov 2026 …").
    /// </summary>
    public static string? NotFedReason(Animal animal, DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(animal);
        var tag = animal.Tag?.TagId ?? "This animal";
        var onDay = Format(day);

        if (animal.AcquisitionDate > day)
            return $"{tag} was not on the farm yet on {onDay} (it arrived on {Format(animal.AcquisitionDate)}), so it can't be fed that day.";

        if (animal.SaleDate is { } sold && day >= sold)
            return $"{tag} was sold on {Format(sold)}, so it can't be fed on {onDay}.";

        if (animal.SaleDate is null && animal.DeathDate is { } died && day >= died)
            return $"{tag} died on {Format(died)}, so it can't be fed on {onDay}.";

        return animal.Status switch
        {
            AnimalStatus.Active or AnimalStatus.Quarantined => null,
            AnimalStatus.Sold or AnimalStatus.Dead or AnimalStatus.Slaughtered when ExitDate(animal) is not null => null,
            _ => $"{tag} is {animal.Status} and no longer on the farm, so it can't be fed.",
        };
    }

    private static string Format(DateOnly date) => date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
}
