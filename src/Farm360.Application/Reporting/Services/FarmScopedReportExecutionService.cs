using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Application.Reporting.Registry;

namespace Farm360.Application.Reporting.Services;

/// <summary>
/// Decorator over <see cref="ReportExecutionService"/> that validates entity parameters before a
/// report runs: every Farm, Animal and Batch parameter must reference an entity in the caller's
/// tenant and within the caller's allowed farms. Otherwise the run is refused with 403, so a
/// farm-restricted user cannot read another farm's data by editing a report parameter.
/// </summary>
public sealed class FarmScopedReportExecutionService(
    ReportExecutionService inner,
    IReportRegistry registry,
    IFarmAccessService farmAccess) : IReportExecutionService
{
    public IReadOnlyList<ReportDescriptor> GetCatalog() => inner.GetCatalog();

    public Task<IReadOnlyList<ReportDescriptor>> GetCatalogAsync(CancellationToken cancellationToken = default)
        => inner.GetCatalogAsync(cancellationToken);

    public Task<bool> HasPermissionAsync(string? permission, CancellationToken cancellationToken = default)
        => inner.HasPermissionAsync(permission, cancellationToken);

    public ReportDescriptor GetMetadata(string key) => inner.GetMetadata(key);

    public async Task<ReportDataSet> RunAsync(ReportRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureEntityParametersAccessibleAsync(request, cancellationToken).ConfigureAwait(false);
        return await inner.RunAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<RenderedReport> ExportAsync(ReportRequest request, ReportExportFormat format, CancellationToken cancellationToken = default)
    {
        await EnsureEntityParametersAccessibleAsync(request, cancellationToken).ConfigureAwait(false);
        return await inner.ExportAsync(request, format, cancellationToken).ConfigureAwait(false);
    }

    internal async Task EnsureEntityParametersAccessibleAsync(ReportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Unknown keys are left to the inner service (it reports them as not found).
        if (request.Parameters is null || !registry.TryGet(request.Key, out var definition))
        {
            return;
        }

        foreach (var parameter in definition.Parameters)
        {
            if (!request.Parameters.TryGetValue(parameter.Name, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            foreach (var id in ParseIds(raw))
            {
                var allowed = parameter.Type switch
                {
                    ReportParameterType.Farm => await farmAccess.CanAccessFarmAsync(id, cancellationToken).ConfigureAwait(false),
                    ReportParameterType.Animal => await farmAccess.CanAccessAnimalAsync(id, cancellationToken).ConfigureAwait(false),
                    ReportParameterType.Batch => await farmAccess.CanAccessBatchAsync(id, cancellationToken).ConfigureAwait(false),
                    _ => true,
                };

                if (!allowed)
                {
                    throw new ForbiddenAccessException("You do not have access to the selected farm, animal or batch.");
                }
            }
        }
    }

    private static IEnumerable<Guid> ParseIds(string raw) =>
        raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => Guid.TryParse(v, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty);
}
