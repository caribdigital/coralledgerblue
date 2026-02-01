using CoralLedger.Blue.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace CoralLedger.Blue.Web.Endpoints;

public static class ApiKeyManagementEndpoints
{
    public static IEndpointRouteBuilder MapApiKeyManagementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/api-keys")
            .WithTags("API Key Management");

        // POST /api/api-keys/clients - Create a new API client
        group.MapPost("/clients", async (
            CreateApiClientRequest request,
            IApiKeyService apiKeyService,
            CancellationToken ct) =>
        {
            var (client, apiKey, plainKey) = await apiKeyService.CreateApiClientAsync(
                request.Name,
                request.OrganizationName,
                request.Description,
                request.ContactEmail,
                request.RateLimitPerMinute,
                ct).ConfigureAwait(false);

            return Results.Ok(new
            {
                client = new
                {
                    client.Id,
                    client.Name,
                    client.ClientId,
                    client.OrganizationName,
                    client.Description,
                    client.ContactEmail,
                    client.RateLimitPerMinute,
                    client.IsActive,
                    client.CreatedAt
                },
                apiKey = new
                {
                    apiKey.Id,
                    apiKey.Name,
                    apiKey.KeyPrefix,
                    apiKey.Scopes,
                    apiKey.ExpiresAt,
                    apiKey.CreatedAt
                },
                plainKey = plainKey,
                warning = "Store this API key securely. It will not be shown again."
            });
        })
        .WithName("CreateApiClient")
        .WithDescription("Create a new API client with an initial API key. Returns the plain API key - store it securely!")
        .Produces<object>(StatusCodes.Status200OK);

        // POST /api/api-keys/clients/{clientId}/keys - Create additional API key for a client
        group.MapPost("/clients/{clientId:guid}/keys", async (
            Guid clientId,
            CreateApiKeyRequest request,
            IApiKeyService apiKeyService,
            CancellationToken ct) =>
        {
            try
            {
                var (apiKey, plainKey) = await apiKeyService.CreateApiKeyAsync(
                    clientId,
                    request.Name,
                    request.ExpiresAt,
                    request.Scopes,
                    ct).ConfigureAwait(false);

                return Results.Ok(new
                {
                    apiKey = new
                    {
                        apiKey.Id,
                        apiKey.Name,
                        apiKey.KeyPrefix,
                        apiKey.Scopes,
                        apiKey.ExpiresAt,
                        apiKey.CreatedAt
                    },
                    plainKey = plainKey,
                    warning = "Store this API key securely. It will not be shown again."
                });
            }
            catch (InvalidOperationException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("CreateApiKey")
        .WithDescription("Create a new API key for an existing client")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // DELETE /api/api-keys/{keyId} - Revoke an API key
        group.MapDelete("/{keyId:guid}", async (
            Guid keyId,
            RevokeApiKeyRequest request,
            IApiKeyService apiKeyService,
            CancellationToken ct) =>
        {
            try
            {
                await apiKeyService.RevokeApiKeyAsync(keyId, request.Reason, ct).ConfigureAwait(false);
                return Results.Ok(new { message = "API key revoked successfully" });
            }
            catch (InvalidOperationException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("RevokeApiKey")
        .WithDescription("Revoke an API key")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/api-keys/clients - Get all API clients
        group.MapGet("/clients", async (
            IApiKeyService apiKeyService,
            CancellationToken ct) =>
        {
            var clients = await apiKeyService.GetApiClientsAsync(ct).ConfigureAwait(false);
            return Results.Ok(clients.Select(c => new
            {
                c.Id,
                c.Name,
                c.ClientId,
                c.OrganizationName,
                c.Description,
                c.ContactEmail,
                c.RateLimitPerMinute,
                c.IsActive,
                c.CreatedAt,
                c.DeactivatedAt,
                c.DeactivationReason,
                ApiKeys = c.ApiKeys.Select(k => new
                {
                    k.Id,
                    k.Name,
                    k.KeyPrefix,
                    k.Scopes,
                    k.IsActive,
                    k.ExpiresAt,
                    k.LastUsedAt,
                    k.RevokedAt,
                    k.CreatedAt
                })
            }));
        })
        .WithName("GetApiClients")
        .WithDescription("Get all API clients with their keys")
        .Produces<object>(StatusCodes.Status200OK);

        // GET /api/api-keys/clients/{clientId} - Get specific API client
        group.MapGet("/clients/{clientId:guid}", async (
            Guid clientId,
            IApiKeyService apiKeyService,
            CancellationToken ct) =>
        {
            var client = await apiKeyService.GetApiClientAsync(clientId, ct).ConfigureAwait(false);
            if (client == null)
                return Results.NotFound();

            return Results.Ok(new
            {
                client.Id,
                client.Name,
                client.ClientId,
                client.OrganizationName,
                client.Description,
                client.ContactEmail,
                client.RateLimitPerMinute,
                client.IsActive,
                client.CreatedAt,
                client.DeactivatedAt,
                client.DeactivationReason,
                ApiKeys = client.ApiKeys.Select(k => new
                {
                    k.Id,
                    k.Name,
                    k.KeyPrefix,
                    k.Scopes,
                    k.IsActive,
                    k.ExpiresAt,
                    k.LastUsedAt,
                    k.RevokedAt,
                    k.CreatedAt
                })
            });
        })
        .WithName("GetApiClient")
        .WithDescription("Get a specific API client with its keys")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/api-keys/clients/{clientId}/usage - Get usage statistics
        group.MapGet("/clients/{clientId:guid}/usage", async (
            Guid clientId,
            DateTime? startDate,
            DateTime? endDate,
            IApiUsageService apiUsageService,
            CancellationToken ct) =>
        {
            var stats = await apiUsageService.GetUsageStatisticsAsync(
                clientId,
                startDate,
                endDate,
                ct).ConfigureAwait(false);

            return Results.Ok(stats);
        })
        .WithName("GetApiUsageStatistics")
        .WithDescription("Get usage statistics for an API client")
        .Produces<object>(StatusCodes.Status200OK);

        // GET /api/api-keys/clients/{clientId}/logs - Get usage logs
        group.MapGet("/clients/{clientId:guid}/logs", async (
            Guid clientId,
            int pageNumber,
            int pageSize,
            IApiUsageService apiUsageService,
            CancellationToken ct) =>
        {
            var logs = await apiUsageService.GetUsageLogsAsync(
                clientId,
                pageNumber > 0 ? pageNumber : 1,
                pageSize > 0 && pageSize <= 100 ? pageSize : 50,
                ct).ConfigureAwait(false);

            return Results.Ok(logs);
        })
        .WithName("GetApiUsageLogs")
        .WithDescription("Get usage logs for an API client (paginated)")
        .Produces<object>(StatusCodes.Status200OK);

        return endpoints;
    }
}

public record CreateApiClientRequest(
    string Name,
    string? OrganizationName = null,
    string? Description = null,
    string? ContactEmail = null,
    int RateLimitPerMinute = 60);

public record CreateApiKeyRequest(
    string Name,
    DateTime? ExpiresAt = null,
    string Scopes = "read");

public record RevokeApiKeyRequest(string Reason);
