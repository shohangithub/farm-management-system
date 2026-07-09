using NetArchTest.Rules;
using Xunit;

namespace Farm360.Architecture.Tests;

/// <summary>
/// Architecture enforcement tests.
/// Constitution §17 (Unit Testing Standards): Architecture tests run in CI on every PR.
/// Tests FAIL on violation — enforces Clean Architecture dependency rules automatically.
/// Run: dotnet test tests/Farm360.Architecture.Tests
/// </summary>
public sealed class DependencyRuleTests
{
    // Assembly names (Constitution §5 — Folder/Project Standards)
    private const string SharedAssembly       = "Farm360.Shared";
    private const string DomainAssembly       = "Farm360.Domain";
    private const string ContractsAssembly    = "Farm360.Contracts";
    private const string ApplicationAssembly  = "Farm360.Application";
    private const string PersistenceAssembly  = "Farm360.Persistence";
    private const string IdentityAssembly     = "Farm360.Identity";
    private const string InfraAssembly        = "Farm360.Infrastructure";
    private const string ApiAssembly          = "Farm360.Api";

    /// <summary>Domain layer MUST NOT depend on Application, Persistence, Infrastructure, or API.</summary>
    [Fact]
    public void Domain_Should_Not_DependOn_ApplicationOrInfrastructure()
    {
        var result = Types.InAssembly(typeof(Farm360.Domain.Common.BaseEntity).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                ApplicationAssembly,
                PersistenceAssembly,
                IdentityAssembly,
                InfraAssembly,
                ApiAssembly)
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Domain layer has illegal outward dependencies: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    /// <summary>Shared layer MUST NOT depend on any other Farm360 layer.</summary>
    [Fact]
    public void Shared_Should_Not_DependOn_AnyOtherLayer()
    {
        var result = Types.InAssembly(typeof(Farm360.Shared.Primitives.Result).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                DomainAssembly,
                ContractsAssembly,
                ApplicationAssembly,
                PersistenceAssembly,
                IdentityAssembly,
                InfraAssembly,
                ApiAssembly)
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Shared layer has illegal dependencies: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    /// <summary>Application layer MUST NOT depend on Persistence, Identity, Infrastructure, or API.</summary>
    [Fact]
    public void Application_Should_Not_DependOn_Infrastructure()
    {
        var result = Types.InAssembly(typeof(Farm360.Application.DependencyInjection.ApplicationServiceExtensions).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                PersistenceAssembly,
                IdentityAssembly,
                InfraAssembly,
                ApiAssembly)
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Application layer has illegal outward dependencies: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    /// <summary>All MediatR handlers must be in a Features/ folder (Convention §8.3).</summary>
    [Fact]
    public void CommandHandlers_Should_BeInFeaturesFolder()
    {
        var result = Types.InAssembly(typeof(Farm360.Application.DependencyInjection.ApplicationServiceExtensions).Assembly)
            .That()
            .ImplementInterface(typeof(MediatR.IRequestHandler<,>))
            .Should()
            .ResideInNamespaceContaining("Features")
            .GetResult();

        // Only enforce when handlers exist (scaffolding has none)
        if (result.FailingTypeNames?.Any() == true)
        {
            Assert.True(result.IsSuccessful,
                $"MediatR handlers outside Features/: {string.Join(", ", result.FailingTypeNames)}");
        }
    }

    /// <summary>All FluentValidation validators must be co-located with their command (Features/ §8.3).</summary>
    [Fact]
    public void Validators_Should_BeInFeaturesFolder()
    {
        var result = Types.InAssembly(typeof(Farm360.Application.DependencyInjection.ApplicationServiceExtensions).Assembly)
            .That()
            .Inherit(typeof(FluentValidation.AbstractValidator<>))
            .Should()
            .ResideInNamespaceContaining("Features")
            .GetResult();

        if (result.FailingTypeNames?.Any() == true)
        {
            Assert.True(result.IsSuccessful,
                $"Validators outside Features/: {string.Join(", ", result.FailingTypeNames)}");
        }
    }

    /// <summary>All controllers/endpoints must be in Farm360.Api (not leaking into other layers).</summary>
    [Fact]
    public void ApiEndpoints_Should_OnlyExistInApiProject()
    {
        // Verify no endpoint definitions exist in Application or Domain
        var applicationResult = Types.InAssembly(typeof(Farm360.Application.DependencyInjection.ApplicationServiceExtensions).Assembly)
            .ShouldNot()
            .HaveNameEndingWith("Endpoint")
            .GetResult();

        Assert.True(applicationResult.IsSuccessful,
            "Endpoint classes found in Application layer — move to Farm360.Api.");

        var domainResult = Types.InAssembly(typeof(Farm360.Domain.Common.BaseEntity).Assembly)
            .ShouldNot()
            .HaveNameEndingWith("Endpoint")
            .GetResult();

        Assert.True(domainResult.IsSuccessful,
            "Endpoint classes found in Domain layer — move to Farm360.Api.");
    }

    /// <summary>No direct DB context references in Application layer (reads go through handler directly, writes through IUnitOfWork).</summary>
    [Fact]
    public void Application_Should_Not_Reference_DbContext_Directly()
    {
        var result = Types.InAssembly(typeof(Farm360.Application.DependencyInjection.ApplicationServiceExtensions).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Application layer directly references EF Core — use interfaces only: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
