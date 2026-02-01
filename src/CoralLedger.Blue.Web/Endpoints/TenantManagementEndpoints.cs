using CoralLedger.Blue.Application.Features.Tenants.Commands.CreateTenant;
using CoralLedger.Blue.Application.Features.Tenants.Queries.GetTenantById;
using CoralLedger.Blue.Application.Features.Tenants.Queries.GetTenants;
using MediatR;

namespace CoralLedger.Blue.Web.Endpoints;

public static class TenantManagementEndpoints
{
    public static IEndpointRouteBuilder MapTenantManagementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // SECURITY: Tenant management endpoints are currently open for development/testing
        // TODO: Add proper authorization in production - these endpoints should be restricted to system administrators
        // Example: .RequireAuthorization(policy => policy.RequireRole("SystemAdmin"))
        var group = endpoints.MapGroup("/api/tenants")
            .WithTags("Tenant Management")
            .WithDescription("SECURITY WARNING: Tenant management requires admin authorization in production");

        // GET /api/tenants - Get all active tenants
        group.MapGet("/", async (
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetTenantsQuery(), ct).ConfigureAwait(false);

            if (!result.Success)
                return Results.BadRequest(new { error = result.Error });

            return Results.Ok(result.Tenants);
        })
        .WithName("GetTenants")
        .WithDescription("Get all active tenants in the system")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        // GET /api/tenants/{tenantId} - Get tenant by ID
        group.MapGet("/{tenantId:guid}", async (
            Guid tenantId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetTenantByIdQuery(tenantId), ct).ConfigureAwait(false);

            if (!result.Success)
                return Results.NotFound(new { error = result.Error });

            return Results.Ok(result.Tenant);
        })
        .WithName("GetTenantById")
        .WithDescription("Get a specific tenant by ID with configuration and branding")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/tenants - Create a new tenant
        group.MapPost("/", async (
            CreateTenantRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new CreateTenantCommand(
                request.Name,
                request.Slug,
                request.Description,
                request.RegionCode);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!result.Success)
                return Results.BadRequest(new { error = result.Error });

            return Results.Created($"/api/tenants/{result.Tenant!.Id}", result.Tenant);
        })
        .WithName("CreateTenant")
        .WithDescription("Create a new tenant for multi-tenant deployment. Each tenant gets default configuration and branding.")
        .Produces<object>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);

        return endpoints;
    }
}

public record CreateTenantRequest(
    string Name,
    string Slug,
    string? Description = null,
    string? RegionCode = null);
