using CoralLedger.Blue.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace CoralLedger.Blue.Infrastructure.Services;

/// <summary>
/// Implementation of password hashing using BCrypt
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    private readonly ILogger<PasswordHasher> _logger;

    public PasswordHasher(ILogger<PasswordHasher> logger)
    {
        _logger = logger;
    }

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying password hash");
            return false;
        }
    }
}
