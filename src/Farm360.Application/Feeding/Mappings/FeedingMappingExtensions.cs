using Farm360.Application.Feeding.DTOs;
using Farm360.Domain.Feeding;

namespace Farm360.Application.Feeding.Mappings;

public static class FeedingMappingExtensions
{
    public static FeedIngredientDto ToDto(this FeedIngredient entity)
    {
        return new FeedIngredientDto(
            entity.Id,
            entity.TenantId,
            entity.Name,
            entity.Category,
            entity.Category.ToString(),
            entity.NutritionalProfile?.DryMatterPercentage ?? 0,
            entity.NutritionalProfile?.CrudeProteinPercentage ?? 0,
            entity.NutritionalProfile?.MetabolizableEnergyMjPerKg ?? 0,
            entity.NutritionalProfile?.CrudeFiberPercentage ?? 0,
            entity.NutritionalProfile?.CalciumPercentage ?? 0,
            entity.NutritionalProfile?.PhosphorusPercentage ?? 0,
            entity.Unit,
            entity.UnitCostBdt,
            entity.IsPreloaded,
            entity.IsActive,
            entity.Description);
    }

    public static FeedFormulaDto ToDto(this FeedFormula entity, Dictionary<Guid, string>? ingredientNames = null)
    {
        var ingredients = entity.Ingredients.Select(i => new FormulaIngredientDto(
            i.Id,
            i.IngredientId,
            ingredientNames != null && ingredientNames.TryGetValue(i.IngredientId, out var name) ? name : "Ingredient",
            i.Percentage,
            i.IngredientCostPerKg,
            i.IngredientNutritionalProfile?.DryMatterPercentage ?? 0,
            i.IngredientNutritionalProfile?.CrudeProteinPercentage ?? 0,
            i.IngredientNutritionalProfile?.MetabolizableEnergyMjPerKg ?? 0
        )).ToList();

        return new FeedFormulaDto(
            entity.Id,
            entity.TenantId,
            entity.Title,
            entity.TargetSpecies,
            entity.TargetSpecies.ToString(),
            entity.TargetStage,
            entity.Status,
            entity.Status.ToString(),
            entity.Description,
            entity.TotalCostPerKgBdt,
            entity.NutritionalProfile?.DryMatterPercentage ?? 0,
            entity.NutritionalProfile?.CrudeProteinPercentage ?? 0,
            entity.NutritionalProfile?.MetabolizableEnergyMjPerKg ?? 0,
            ingredients);
    }

    public static FeedingScheduleDto ToDto(this FeedingSchedule entity, string formulaTitle, string? shedNum = null, string? penNum = null, string? batchName = null)
    {
        return new FeedingScheduleDto(
            entity.Id,
            entity.TenantId,
            entity.FarmId,
            entity.ShedId,
            shedNum,
            entity.PenId,
            penNum,
            entity.BatchId,
            batchName,
            entity.FormulaId,
            formulaTitle,
            entity.Title,
            entity.TargetQuantityKgPerHead,
            entity.Frequency,
            entity.Frequency.ToString(),
            entity.StartDate,
            entity.EndDate,
            entity.IsActive,
            entity.Notes);
    }

    public static FeedConsumptionLogDto ToDto(this FeedConsumptionLog entity, string formulaTitle, Dictionary<Guid, string>? ingredientNames = null, string? shedNum = null, string? penNum = null, string? batchName = null)
    {
        var details = entity.Details.Select(d => new ConsumptionDetailDto(
            d.Id,
            d.IngredientId,
            ingredientNames != null && ingredientNames.TryGetValue(d.IngredientId, out var name) ? name : "Ingredient",
            d.OfferedKg,
            d.RefusalKg,
            d.NetConsumedKg,
            d.CostBdt
        )).ToList();

        return new FeedConsumptionLogDto(
            entity.Id,
            entity.TenantId,
            entity.FarmId,
            entity.ShedId,
            shedNum,
            entity.PenId,
            penNum,
            entity.BatchId,
            batchName,
            entity.FormulaId,
            formulaTitle,
            entity.LogDate,
            entity.HeadCount,
            entity.TotalFeedOfferedKg,
            entity.TotalRefusalKg,
            entity.NetConsumptionKg,
            entity.TotalCostBdt,
            entity.LoggedByUserId,
            entity.Notes,
            details);
    }
}
