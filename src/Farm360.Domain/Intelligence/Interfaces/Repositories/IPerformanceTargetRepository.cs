using Farm360.Domain.Interfaces.Repositories;
using Farm360.Domain.Intelligence;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Domain.Intelligence.Interfaces.Repositories;

public interface IPerformanceTargetRepository
{
    Task<PerformanceTarget?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(PerformanceTarget target, CancellationToken cancellationToken = default);
    Task<PerformanceTarget?> GetTargetForBreedAndStageAsync(string breedName, string stage, CancellationToken cancellationToken = default);
}
