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
}
