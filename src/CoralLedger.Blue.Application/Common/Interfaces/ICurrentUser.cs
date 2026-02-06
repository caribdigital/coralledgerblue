namespace CoralLedger.Blue.Application.Common.Interfaces;

/// <summary>
/// Provides access to the currently authenticated user's information
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// Gets the unique identifier of the current user
    /// </summary>
    Guid? UserId { get; }
    
    /// <summary>
    /// Gets the email address of the current user
    /// </summary>
    string? Email { get; }
    
    /// <summary>
    /// Gets the full name of the current user
    /// </summary>
    string? Name { get; }
    
    /// <summary>
    /// Gets whether the current user is authenticated
    /// </summary>
    bool IsAuthenticated { get; }
    
    /// <summary>
    /// Gets the roles assigned to the current user
    /// </summary>
    IEnumerable<string> Roles { get; }
    
    /// <summary>
    /// Gets the tenant ID of the current user
    /// </summary>
    Guid? TenantId { get; }
}
