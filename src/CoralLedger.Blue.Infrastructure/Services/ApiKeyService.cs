using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Domain.Entities;
using CoralLedger.Blue.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CoralLedger.Blue.Infrastructure.Services;

public class ApiKeyService : IApiKeyService
{
    private readonly MarineDbContext _context;

    public ApiKeyService(MarineDbContext context)
    {
        _context = context;
    }

    public async Task<ApiKey?> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        var keyHash = ApiKey.ComputeHash(apiKey);

        var key = await _context.ApiKeys
            .Include(k => k.ApiClient)
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash, cancellationToken)
            .ConfigureAwait(false);

        if (key == null || !key.IsValid())
            return null;

        if (!key.ApiClient.IsActive)
            return null;

        return key;
    }

    public async Task<(ApiClient client, ApiKey apiKey, string plainKey)> CreateApiClientAsync(
        string name,
        string? organizationName = null,
        string? description = null,
        string? contactEmail = null,
        int rateLimitPerMinute = 60,
        CancellationToken cancellationToken = default)
    {
        var client = ApiClient.Create(name, organizationName, description, contactEmail, rateLimitPerMinute);
        _context.ApiClients.Add(client);

        var (apiKey, plainKey) = ApiKey.Create(client.Id, "Default Key", scopes: "read");
        _context.ApiKeys.Add(apiKey);

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return (client, apiKey, plainKey);
    }

    public async Task<(ApiKey apiKey, string plainKey)> CreateApiKeyAsync(
        Guid apiClientId,
        string keyName,
        DateTime? expiresAt = null,
        string scopes = "read",
        CancellationToken cancellationToken = default)
    {
        var client = await _context.ApiClients
            .FindAsync(new object[] { apiClientId }, cancellationToken)
            .ConfigureAwait(false);

        if (client == null)
            throw new InvalidOperationException($"API client {apiClientId} not found");

        var (apiKey, plainKey) = ApiKey.Create(apiClientId, keyName, expiresAt, scopes);
        _context.ApiKeys.Add(apiKey);

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return (apiKey, plainKey);
    }

    public async Task RevokeApiKeyAsync(Guid apiKeyId, string reason, CancellationToken cancellationToken = default)
    {
        var apiKey = await _context.ApiKeys
            .FindAsync(new object[] { apiKeyId }, cancellationToken)
            .ConfigureAwait(false);

        if (apiKey == null)
            throw new InvalidOperationException($"API key {apiKeyId} not found");

        apiKey.Revoke(reason);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<ApiClient>> GetApiClientsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ApiClients
            .Include(c => c.ApiKeys)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ApiClient?> GetApiClientAsync(Guid apiClientId, CancellationToken cancellationToken = default)
    {
        return await _context.ApiClients
            .Include(c => c.ApiKeys)
            .FirstOrDefaultAsync(c => c.Id == apiClientId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task UpdateApiKeyLastUsedAsync(Guid apiKeyId, CancellationToken cancellationToken = default)
    {
        var apiKey = await _context.ApiKeys
            .FindAsync(new object[] { apiKeyId }, cancellationToken)
            .ConfigureAwait(false);

        if (apiKey != null)
        {
            apiKey.UpdateLastUsed();
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
