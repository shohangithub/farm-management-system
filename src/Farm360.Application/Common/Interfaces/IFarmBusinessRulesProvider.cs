using Farm360.Domain.BusinessRules;

namespace Farm360.Application.Common.Interfaces;

/// <summary>
/// Supplies the farm's configurable business assumptions to application code.
/// </summary>
/// <remarks>
/// An interface rather than an injected options object so the Application layer stays free of
/// <c>Microsoft.Extensions.Options</c> and so tests can state their assumptions in one line.
/// The implementation reads the <c>Farm360:BusinessRules</c> configuration section and picks up
/// changes without a restart.
/// </remarks>
public interface IFarmBusinessRulesProvider
{
    FarmBusinessRules Current { get; }
}
