using Farm360.Domain.Livestock;
using System;

namespace Farm360.Application.Livestock.DTOs;

public sealed record BreedDto(
    Guid Id,
    string Name,
    string Description,
    string Category,
    string Origin,
    string MainPurpose,
    string BestFor,
    decimal AdgPoorManagement,
    decimal AdgAverageFarm,
    decimal AdgGoodCommercialFarm,
    decimal AdgIntensiveFattening,
    decimal StandardAdgMin,
    decimal StandardAdgMax,
    decimal FcrMin,
    decimal FcrMax,
    decimal MilkYieldMinLiters,
    decimal MilkYieldMaxLiters,
    decimal FatPercentageMin,
    decimal FatPercentageMax);
