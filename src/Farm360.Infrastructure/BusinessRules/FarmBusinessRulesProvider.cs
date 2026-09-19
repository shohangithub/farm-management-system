using System;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.BusinessRules;
using Microsoft.Extensions.Options;

namespace Farm360.Infrastructure.BusinessRules;

/// <summary>
/// Reads the farm's business assumptions from the <c>Farm360:BusinessRules</c> configuration
/// section, picking up edits without a restart.
/// </summary>
/// <remarks>
/// <see cref="IOptionsMonitor{T}"/> rather than <see cref="IOptions{T}"/> so a rule can be changed
/// in configuration and take effect on the next calculation. Historical allocations are unaffected:
/// each row stores the method that produced it, so a rule change never rewrites signed-off numbers.
/// </remarks>
public sealed class FarmBusinessRulesProvider : IFarmBusinessRulesProvider
{
    private readonly IOptionsMonitor<FarmBusinessRules> _options;

    public FarmBusinessRulesProvider(IOptionsMonitor<FarmBusinessRules> options)
    {
        _options = options;
    }

    public FarmBusinessRules Current => _options.CurrentValue;
}

/// <summary>
/// Falls back to the coded defaults when no configuration section is present.
/// </summary>
/// <remarks>
/// Used by tests and by any host that has not declared a section. The defaults are the decisions
/// recorded in docs/32 §9, so an unconfigured environment behaves the same as a configured one
/// that simply agreed with them.
/// </remarks>
public sealed class DefaultFarmBusinessRulesProvider : IFarmBusinessRulesProvider
{
    private readonly FarmBusinessRules _rules;

    public DefaultFarmBusinessRulesProvider(FarmBusinessRules? rules = null)
    {
        _rules = rules ?? new FarmBusinessRules();
    }

    public FarmBusinessRules Current => _rules;
}
