using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Contracts.Intelligence;

namespace Farm360.Application.Intelligence.Services;

public interface IProjectionDefaultsResolver
{
    Task<ProjectionDefaultsDto> ResolveDefaultsAsync(Guid animalId, CancellationToken cancellationToken);
}
