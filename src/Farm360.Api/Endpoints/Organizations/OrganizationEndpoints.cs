using Farm360.Application.Organizations.Commands;
using Farm360.Application.Organizations.Queries;
using Farm360.Persistence.Seed;
using MediatR;

namespace Farm360.Api.Endpoints.Organizations;

public static class OrganizationEndpoints
{
    public static void MapOrganizationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/organizations")
            .WithTags("Organizations")
            .RequireAuthorization();

        group.MapGet("/", async (ISender sender, string? search, int? status, int page = 1, int size = 10) =>
        {
            var result = await sender.Send(new GetOrganizationsQuery(search, status, page, size));
            return Results.Ok(result);
        })
        .RequireAuthorization($"Permission:{PermissionConstants.OrganizationModule.View}");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetOrganizationByIdQuery(id));
            return Results.Ok(result);
        })
        .RequireAuthorization($"Permission:{PermissionConstants.OrganizationModule.View}");

        group.MapPost("/", async (CreateOrganizationCommand command, ISender sender) =>
        {
            var id = await sender.Send(command);
            return Results.Created($"/api/v1/organizations/{id}", new { Id = id });
        })
        .RequireAuthorization($"Permission:{PermissionConstants.OrganizationModule.Create}");

        group.MapPut("/{id:guid}", async (Guid id, UpdateOrganizationCommand command, ISender sender) =>
        {
            if (id != command.Id)
                return Results.BadRequest("ID mismatch.");

            await sender.Send(command);
            return Results.NoContent();
        })
        .RequireAuthorization($"Permission:{PermissionConstants.OrganizationModule.Edit}");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            await sender.Send(new DeactivateOrganizationCommand(id));
            return Results.NoContent();
        })
        .RequireAuthorization($"Permission:{PermissionConstants.OrganizationModule.Delete}");

        group.MapPost("/{id:guid}/activate", async (Guid id, ISender sender) =>
        {
            await sender.Send(new ActivateOrganizationCommand(id));
            return Results.NoContent();
        })
        .RequireAuthorization($"Permission:{PermissionConstants.OrganizationModule.Edit}");

        group.MapPost("/{id:guid}/logo", async (Guid id, Microsoft.AspNetCore.Http.IFormFile file, Farm360.Application.Common.Interfaces.IFileStorageService storageService, ISender sender) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest("No file uploaded.");

            using var stream = file.OpenReadStream();
            var logoUrl = await storageService.UploadFileAsync(stream, file.FileName, "organizations");

            await sender.Send(new UpdateOrganizationLogoCommand(id, logoUrl));
            
            return Results.Ok(new { LogoUrl = logoUrl });
        })
        .DisableAntiforgery()
        .RequireAuthorization($"Permission:{PermissionConstants.OrganizationModule.Edit}");
    }
}
