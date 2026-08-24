using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Domain.Finance.Interfaces;

public interface IAnimalCostLedgerRepository
{
    Task<AnimalCostLedger?> GetByAnimalIdAsync(Guid animalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AnimalCostLedger>> GetByFarmIdAsync(Guid farmId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AnimalCostLedger>> GetByBatchIdAsync(Guid farmId, Guid batchId, CancellationToken cancellationToken = default);
    void Add(AnimalCostLedger ledger);
    void Update(AnimalCostLedger ledger);
}
