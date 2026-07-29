using Farm360.Domain.Common;
using System;

namespace Farm360.Domain.Intelligence;

public class PerformanceTarget : AuditableEntity
{
    public string BreedName { get; private set; } = string.Empty;
    public string Stage { get; private set; } = string.Empty;
    
    public decimal TargetAdgKg { get; private set; }
    public decimal TargetFcr { get; private set; }
    public decimal TargetCostPerKgGainBdt { get; private set; }
    
    private PerformanceTarget() { } // EF Core
    
    public PerformanceTarget(
        Guid id,
        Guid tenantId,
        string breedName,
        string stage,
        decimal targetAdgKg,
        decimal targetFcr,
        decimal targetCostPerKgGainBdt) : base(id, tenantId)
    {
        BreedName = breedName;
        Stage = stage;
        TargetAdgKg = targetAdgKg;
        TargetFcr = targetFcr;
        TargetCostPerKgGainBdt = targetCostPerKgGainBdt;
    }
}
