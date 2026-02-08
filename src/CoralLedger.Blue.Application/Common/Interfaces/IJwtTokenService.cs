using CoralLedger.Blue.Domain.Entities;

namespace CoralLedger.Blue.Application.Common.Interfaces;

/// <summary>
/// Service for generating JWT tokens
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Generates a JWT access token for the user
    /// </summary>
    string GenerateAccessToken(TenantUser user);
    
    /// <summary>
    /// Generates a refresh token
    /// </summary>
    string GenerateRefreshToken();
    
    /// <summary>
    /// Validates a JWT token and returns the user ID if valid
    /// </summary>
    Guid? ValidateToken(string token);
    
    /// <summary>
    /// Stores a refresh token in the database
    /// </summary>
    Task<Guid> StoreRefreshTokenAsync(Guid tenantUserId, string refreshToken, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates a refresh token and returns the user ID if valid
    /// </summary>
    Task<Guid?> ValidateRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Revokes a refresh token
    /// </summary>
    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Revokes all refresh tokens for a user
    /// </summary>
    Task RevokeAllUserRefreshTokensAsync(Guid tenantUserId, CancellationToken cancellationToken = default);
}
