using CoralLedger.Blue.Domain.Entities;

namespace CoralLedger.Blue.Application.Common.Interfaces;

/// <summary>
/// Service for managing API clients and keys
/// </summary>
public interface IApiKeyService
{
    /// <summary>
    /// Validates an API key and returns the associated ApiKey entity
    /// </summary>
    Task<ApiKey?> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a new API client with an initial API key
    /// </summary>
    Task<(ApiClient client, ApiKey apiKey, string plainKey)> CreateApiClientAsync(
        string name,
        string? organizationName = null,
        string? description = null,
        string? contactEmail = null,
        int rateLimitPerMinute = 60,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a new API key for an existing client
    /// </summary>
    Task<(ApiKey apiKey, string plainKey)> CreateApiKeyAsync(
        Guid apiClientId,
        string keyName,
        DateTime? expiresAt = null,
        string scopes = "read",
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Revokes an API key
    /// </summary>
    Task RevokeApiKeyAsync(Guid apiKeyId, string reason, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all API clients
    /// </summary>
    Task<List<ApiClient>> GetApiClientsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets an API client by ID
    /// </summary>
    Task<ApiClient?> GetApiClientAsync(Guid apiClientId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates the last used timestamp for an API key
    /// </summary>
    Task UpdateApiKeyLastUsedAsync(Guid apiKeyId, CancellationToken cancellationToken = default);
}
