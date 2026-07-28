using Farm360.Domain.Common;

namespace Farm360.Domain.Feeding.ValueObjects;

public sealed class NutritionalProfile : BaseValueObject
{
    public decimal DryMatterPercentage { get; private set; }
    public decimal CrudeProteinPercentage { get; private set; }
    public decimal MetabolizableEnergyMjPerKg { get; private set; }
    public decimal CrudeFiberPercentage { get; private set; }
    public decimal CalciumPercentage { get; private set; }
    public decimal PhosphorusPercentage { get; private set; }

    private NutritionalProfile() { }

    public NutritionalProfile(
        decimal dryMatterPercentage,
        decimal crudeProteinPercentage,
        decimal metabolizableEnergyMjPerKg,
        decimal crudeFiberPercentage = 0,
        decimal calciumPercentage = 0,
        decimal phosphorusPercentage = 0)
    {
        DryMatterPercentage = Math.Max(0, dryMatterPercentage);
        CrudeProteinPercentage = Math.Max(0, crudeProteinPercentage);
        MetabolizableEnergyMjPerKg = Math.Max(0, metabolizableEnergyMjPerKg);
        CrudeFiberPercentage = Math.Max(0, crudeFiberPercentage);
        CalciumPercentage = Math.Max(0, calciumPercentage);
        PhosphorusPercentage = Math.Max(0, phosphorusPercentage);
    }

    public static NutritionalProfile Empty => new(0, 0, 0, 0, 0, 0);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return DryMatterPercentage;
        yield return CrudeProteinPercentage;
        yield return MetabolizableEnergyMjPerKg;
        yield return CrudeFiberPercentage;
        yield return CalciumPercentage;
        yield return PhosphorusPercentage;
    }
}
