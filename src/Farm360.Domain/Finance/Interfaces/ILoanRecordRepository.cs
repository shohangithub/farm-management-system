using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Domain.Finance.Interfaces;

public interface ILoanRecordRepository
{
    Task<LoanRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LoanRecord>> GetByFarmIdAsync(Guid farmId, CancellationToken cancellationToken = default);
    void Add(LoanRecord loan);
    void Update(LoanRecord loan);
    void Delete(LoanRecord loan);
}
