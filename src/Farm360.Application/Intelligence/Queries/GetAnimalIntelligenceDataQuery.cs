using Farm360.Application.Common.Interfaces;
using Farm360.Application.Intelligence.Interfaces;
using Farm360.Domain.Intelligence;
using Farm360.Domain.Intelligence.Interfaces.Repositories;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Intelligence.Queries;

public sealed record GetAnimalIntelligenceDataQuery(Guid AnimalId) : IRequest<AnimalIntelligenceDataResponse>;

public sealed record AnimalIntelligenceDataResponse(
    IReadOnlyList<ActionableInsightDto> ActiveInsights,
    GrowthCurveDto? GrowthCurve
);

public sealed record ActionableInsightDto(
    Guid Id,
    string Type,
    string Severity,
    string Title,
    string Message,
    DateTime CreatedAtUtc
);

public sealed record GrowthCurveDto(
    decimal CurrentWeightKg,
    decimal Projected30DayWeightKg,
    decimal Projected60DayWeightKg,
    decimal Projected90DayWeightKg,
    decimal CurrentAdgKg
);

public class GetAnimalIntelligenceDataQueryHandler : IRequestHandler<GetAnimalIntelligenceDataQuery, AnimalIntelligenceDataResponse>
{
    private readonly IInsightRepository _insightRepository;
    private readonly IGrowthPredictionEngine _growthPredictionEngine;

    public GetAnimalIntelligenceDataQueryHandler(
        IInsightRepository insightRepository,
        IGrowthPredictionEngine growthPredictionEngine)
    {
        _insightRepository = insightRepository;
        _growthPredictionEngine = growthPredictionEngine;
    }

    public async Task<AnimalIntelligenceDataResponse> Handle(GetAnimalIntelligenceDataQuery request, CancellationToken cancellationToken)
    {
        var insights = await _insightRepository.GetActiveInsightsByAnimalIdAsync(request.AnimalId, cancellationToken);
        var growthCurve = await _growthPredictionEngine.CalculateGrowthCurveAsync(request.AnimalId, cancellationToken);

        var insightDtos = new List<ActionableInsightDto>();
        foreach (var insight in insights)
        {
            insightDtos.Add(new ActionableInsightDto(
                insight.Id,
                insight.Type.ToString(),
                insight.Severity.ToString(),
                insight.Title,
                insight.Message,
                insight.CreatedAtUtc
            ));
        }

        GrowthCurveDto? curveDto = null;
        if (growthCurve != null)
        {
            curveDto = new GrowthCurveDto(
                growthCurve.CurrentWeightKg,
                growthCurve.Projected30DayWeightKg,
                growthCurve.Projected60DayWeightKg,
                growthCurve.Projected90DayWeightKg,
                growthCurve.CurrentAdgKg
            );
        }

        return new AnimalIntelligenceDataResponse(insightDtos, curveDto);
    }
}
