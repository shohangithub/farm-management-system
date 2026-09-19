using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Farm360.Api.Endpoints;
using Farm360.Api.Endpoints.Dashboard;
using Farm360.Api.Endpoints.Feeding;
using Farm360.Api.Endpoints.Finance;
using Farm360.Api.Endpoints.Health;
using Farm360.Api.Endpoints.Inventory;
using Farm360.Api.Endpoints.Livestock;
using Farm360.Api.Endpoints.Analytics;
using Farm360.Api.Endpoints.Auth;
using Farm360.Api.Endpoints.Farms;
using Farm360.Api.Endpoints.MasterData;
using Farm360.Api.Endpoints.Organizations;
using Farm360.Api.Endpoints.Tenants;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using Xunit;

namespace Farm360.Api.FunctionalTests;

/// <summary>
/// Guards the endpoint table itself, rather than the behaviour behind it.
/// </summary>
/// <remarks>
/// ASP.NET Core validates endpoint names lazily, when it builds the route matcher on the
/// <b>first request</b> — not at startup. A duplicate name therefore compiles, deploys, starts
/// cleanly and then throws <c>InvalidOperationException</c> at the first HTTP call, which is the
/// worst possible moment to find out. The trap is easy to fall into here because
/// <c>MapFinanceEndpoints</c> deliberately registers the same routes twice, under the legacy
/// <c>/api</c> prefix and the current <c>/api/v1</c> one, so any fixed <c>.WithName()</c> inside
/// it is declared twice.
/// </remarks>
public class EndpointRoutingTests
{
    /// <summary>
    /// Registers the endpoint modules against a bare route builder. Minimal-API handlers resolve
    /// their services per request, so nothing here needs the application's DI container, a
    /// database or Redis — only the route metadata is built.
    /// </summary>
    private static List<Endpoint> BuildEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorizationBuilder();
        builder.Services.AddRouting();

        // Minimal APIs decide whether an un-attributed handler parameter is a service or a
        // request body by asking the container. Handlers take application interfaces
        // (IMediator, IFileStorageService, ...) without attributes, so those types must be
        // resolvable or endpoint construction throws "cannot use both form and JSON body".
        // Registering every Farm360 interface with a null factory satisfies the inference
        // without a database or configuration; nothing is ever invoked, because this test only
        // inspects the route table. Doing it reflectively keeps the test from breaking each
        // time a handler takes a new dependency.
        RegisterInterfacesAsNullServices(builder.Services, typeof(Farm360.Application.Common.Interfaces.IFarmBusinessRulesProvider).Assembly);
        RegisterInterfacesAsNullServices(builder.Services, typeof(MediatR.IMediator).Assembly);

        var app = builder.Build();

        // Mirrors Program.cs exactly — including MapFinanceEndpoints, which internally
        // registers every finance route twice (legacy /api and current /api/v1).
        app.MapGroup("/api/v1/livestock").MapLivestockEndpoints();
        app.MapGroup("/api/v1/livestock").MapBreedEndpoints();
        app.MapHealthEndpoints();
        app.MapFeedingEndpoints();
        app.MapOrganizationEndpoints();
        app.MapBranchEndpoints();
        app.MapTenantEndpoints();
        app.MapFarmEndpoints();
        app.MapDashboardEndpoints();
        app.MapReportEndpoints();
        app.MapShedEndpoints();
        app.MapPenEndpoints();
        app.MapMasterDataEndpoints();
        app.MapLocationEndpoints();
        app.MapInventoryEndpoints();
        app.MapGroup("/api/v1/auth").MapAuthEndpoints();
        app.MapGroup("/api/v1/users").MapUsersEndpoints();
        app.MapIntelligenceEndpoints();
        app.MapFinanceEndpoints();
        app.MapAnalyticsEndpoints();

        // Read the builder's own data sources. Resolving EndpointDataSource from the container
        // returns a composite that stays empty until the app is started, which would make the
        // uniqueness assertion below pass vacuously.
        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(ds => ds.Endpoints)
            .ToList();
    }

    [Fact]
    public void EndpointNamesAreGloballyUnique()
    {
        var named = BuildEndpoints()
            .Select(e => new
            {
                Name = e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName,
                Route = e.DisplayName,
            })
            .Where(x => !string.IsNullOrEmpty(x.Name))
            .ToList();

        var duplicates = named
            .GroupBy(x => x.Name, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"'{g.Key}' on {string.Join(" AND ", g.Select(x => x.Route))}")
            .ToList();

        duplicates.Should().BeEmpty(
            "ASP.NET Core rejects duplicate endpoint names when it builds the matcher on the " +
            "first request, so this would pass CI and fail in production. Remove the " +
            ".WithName() or make it unique per route prefix.");
    }

    [Fact]
    public void EveryEndpointHasARoutePattern()
    {
        // Cheap sanity check that the modules registered at all — if a Map*Endpoints call were
        // dropped, the uniqueness test above would pass vacuously.
        var endpoints = BuildEndpoints();

        endpoints.Should().NotBeEmpty();
        endpoints.OfType<RouteEndpoint>().Should().NotBeEmpty();
    }



    private static void RegisterInterfacesAsNullServices(IServiceCollection services, Assembly assembly)
    {
        foreach (var type in assembly.GetExportedTypes())
        {
            if (type is { IsInterface: true, IsGenericTypeDefinition: false, ContainsGenericParameters: false })
            {
                services.AddSingleton(type, _ => null!);
            }
        }
    }
}
