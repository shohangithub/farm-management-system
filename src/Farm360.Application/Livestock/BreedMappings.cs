using Farm360.Application.Livestock.DTOs;
using Farm360.Domain.Livestock;

namespace Farm360.Application.Livestock;

public static class BreedMappings
{
    public static BreedDto ToDto(this Breed breed) =>
        new(
            Id: breed.Id,
            Name: breed.Name,
            Description: breed.Description,
            Category: breed.Category,
            Origin: breed.Origin,
            MainPurpose: breed.MainPurpose,
            BestFor: breed.BestFor,
            AdgPoorManagement: breed.AdgPoorManagement,
            AdgAverageFarm: breed.AdgAverageFarm,
            AdgGoodCommercialFarm: breed.AdgGoodCommercialFarm,
            AdgIntensiveFattening: breed.AdgIntensiveFattening,
            StandardAdgMin: breed.StandardAdgMin,
            StandardAdgMax: breed.StandardAdgMax,
            FcrMin: breed.FcrMin,
            FcrMax: breed.FcrMax,
            MilkYieldMinLiters: breed.MilkYieldMinLiters,
            MilkYieldMaxLiters: breed.MilkYieldMaxLiters,
            FatPercentageMin: breed.FatPercentageMin,
            FatPercentageMax: breed.FatPercentageMax);
}
