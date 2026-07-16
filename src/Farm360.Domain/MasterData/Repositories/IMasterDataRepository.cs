using Farm360.Domain.MasterData.Enums;

namespace Farm360.Domain.MasterData.Repositories;

public interface IMasterDataRepository
{
    Task<MasterDataEntry?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
    Task<MasterDataEntry?> GetByCodeAsync(Guid tenantId, MasterDataType type, string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MasterDataEntry>> GetAllByTypeAsync(Guid tenantId, MasterDataType type, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(Guid tenantId, MasterDataType type, string code, CancellationToken cancellationToken = default);
    
    void Add(MasterDataEntry entry);
    void Update(MasterDataEntry entry);
    void Delete(MasterDataEntry entry);
}
