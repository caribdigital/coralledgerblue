using System.Security.Cryptography;
using System.Web;
using CoralLedger.Blue.Application.Common.Interfaces;
using OtpNet;

namespace CoralLedger.Blue.Infrastructure.Services;

/// <summary>
/// Implementation of TOTP service for two-factor authentication
/// </summary>
public class TotpService : ITotpService
{
    private const int SecretKeySize = 20; // 160 bits for TOTP
    private const int TotpStep = 30; // Standard TOTP step in seconds
    private const int RecoveryCodeLength = 8;

    /// <inheritdoc />
    public string GenerateSecretKey()
    {
        var key = KeyGeneration.GenerateRandomKey(SecretKeySize);
        return Base32Encoding.ToString(key);
    }

    /// <inheritdoc />
    public string GenerateQrCodeUri(string email, string secretKey, string issuer = "CoralLedger Blue")
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required", nameof(email));
        if (string.IsNullOrWhiteSpace(secretKey))
            throw new ArgumentException("Secret key is required", nameof(secretKey));

        var encodedIssuer = HttpUtility.UrlEncode(issuer);
        var encodedEmail = HttpUtility.UrlEncode(email);

        return $"otpauth://totp/{encodedIssuer}:{encodedEmail}?secret={secretKey}&issuer={encodedIssuer}&algorithm=SHA1&digits=6&period={TotpStep}";
    }

    /// <inheritdoc />
    public bool ValidateCode(string secretKey, string code)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
            return false;
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6)
            return false;

        try
        {
            var keyBytes = Base32Encoding.ToBytes(secretKey);
            var totp = new Totp(keyBytes, step: TotpStep);

            // Allow 1 step before and after for clock drift tolerance
            return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public string[] GenerateRecoveryCodes(int count = 8)
    {
        var codes = new string[count];

        for (var i = 0; i < count; i++)
        {
            codes[i] = GenerateRecoveryCode();
        }

        return codes;
    }

    private static string GenerateRecoveryCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(RecoveryCodeLength);
        // Format as XXXX-XXXX for readability
        var code = Convert.ToHexString(bytes).ToUpperInvariant();
        return $"{code[..4]}-{code[4..]}";
    }
}
