namespace CoralLedger.Blue.Application.Common.Interfaces;

/// <summary>
/// Service for Time-based One-Time Password (TOTP) operations for 2FA
/// </summary>
public interface ITotpService
{
    /// <summary>
    /// Generates a new secret key for TOTP
    /// </summary>
    string GenerateSecretKey();

    /// <summary>
    /// Generates a QR code URI for authenticator apps
    /// </summary>
    /// <param name="email">User's email address</param>
    /// <param name="secretKey">The secret key</param>
    /// <param name="issuer">The issuer name (app name)</param>
    /// <returns>otpauth:// URI for QR code generation</returns>
    string GenerateQrCodeUri(string email, string secretKey, string issuer = "CoralLedger Blue");

    /// <summary>
    /// Validates a TOTP code against the secret key
    /// </summary>
    /// <param name="secretKey">The user's secret key</param>
    /// <param name="code">The 6-digit code to validate</param>
    /// <returns>True if the code is valid</returns>
    bool ValidateCode(string secretKey, string code);

    /// <summary>
    /// Generates recovery codes for backup access
    /// </summary>
    /// <param name="count">Number of codes to generate</param>
    /// <returns>Array of recovery codes</returns>
    string[] GenerateRecoveryCodes(int count = 8);
}
