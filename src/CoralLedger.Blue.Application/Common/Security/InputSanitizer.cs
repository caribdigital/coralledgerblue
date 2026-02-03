using System.Text.RegularExpressions;
using System.Web;

namespace CoralLedger.Blue.Application.Common.Security;

/// <summary>
/// Input sanitization utilities to prevent XSS, SQL injection, and other attacks
/// </summary>
public static partial class InputSanitizer
{
    private static readonly Regex HtmlTagRegex = HtmlTagPattern();
    private static readonly Regex ScriptTagRegex = ScriptTagPattern();
    private static readonly Regex SqlInjectionRegex = SqlInjectionPattern();
    private static readonly Regex PathTraversalRegex = PathTraversalPattern();

    /// <summary>
    /// Sanitize general text input by removing HTML tags and encoding special characters
    /// </summary>
    public static string SanitizeText(string? input, int maxLength = 1000)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Trim and limit length
        var sanitized = input.Trim();
        if (sanitized.Length > maxLength)
            sanitized = sanitized[..maxLength];

        // Remove HTML tags
        sanitized = HtmlTagRegex.Replace(sanitized, string.Empty);

        // Remove script tags specifically (in case of malformed HTML)
        sanitized = ScriptTagRegex.Replace(sanitized, string.Empty);

        // Encode HTML entities
        sanitized = HttpUtility.HtmlEncode(sanitized);

        return sanitized;
    }

    /// <summary>
    /// Sanitize a name field (no special characters except common name characters)
    /// </summary>
    public static string SanitizeName(string? input, int maxLength = 200)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var sanitized = input.Trim();
        if (sanitized.Length > maxLength)
            sanitized = sanitized[..maxLength];

        // Allow only letters, numbers, spaces, hyphens, apostrophes, and common punctuation
        sanitized = Regex.Replace(sanitized, @"[^\p{L}\p{N}\s\-'.,:()]", string.Empty);

        return sanitized;
    }

    /// <summary>
    /// Sanitize an email address
    /// </summary>
    public static string SanitizeEmail(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var sanitized = input.Trim().ToLowerInvariant();

        // Basic email character whitelist
        sanitized = Regex.Replace(sanitized, @"[^\w.@+-]", string.Empty);

        // Validate email format
        if (!IsValidEmail(sanitized))
            return string.Empty;

        return sanitized;
    }

    /// <summary>
    /// Sanitize a file path to prevent path traversal attacks
    /// </summary>
    public static string SanitizeFilePath(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Remove path traversal sequences
        var sanitized = PathTraversalRegex.Replace(input, string.Empty);

        // Remove absolute path indicators
        sanitized = sanitized.TrimStart('/', '\\');

        // Only allow safe filename characters
        sanitized = Regex.Replace(sanitized, @"[^\w\-./]", string.Empty);

        return sanitized;
    }

    /// <summary>
    /// Sanitize a URL
    /// </summary>
    public static string? SanitizeUrl(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var sanitized = input.Trim();

        // Only allow http and https schemes
        if (!Uri.TryCreate(sanitized, UriKind.Absolute, out var uri))
            return null;

        if (uri.Scheme != "http" && uri.Scheme != "https")
            return null;

        return uri.ToString();
    }

    /// <summary>
    /// Check for potential SQL injection patterns
    /// </summary>
    public static bool ContainsSqlInjection(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        return SqlInjectionRegex.IsMatch(input);
    }

    /// <summary>
    /// Validate email format
    /// </summary>
    public static bool IsValidEmail(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        try
        {
            var addr = new System.Net.Mail.MailAddress(input);
            return addr.Address == input;
        }
        catch (Exception)
        {
            // Invalid email format - intentionally silent for security
            return false;
        }
    }

    /// <summary>
    /// Sanitize JSON string content
    /// </summary>
    public static string SanitizeJsonString(string? input, int maxLength = 10000)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var sanitized = input.Trim();
        if (sanitized.Length > maxLength)
            sanitized = sanitized[..maxLength];

        // Remove control characters except standard whitespace
        sanitized = Regex.Replace(sanitized, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", string.Empty);

        return sanitized;
    }

    /// <summary>
    /// Validate and sanitize coordinates
    /// </summary>
    public static (double? lat, double? lon) SanitizeCoordinates(double? latitude, double? longitude)
    {
        // Validate latitude range
        if (latitude.HasValue && (latitude < -90 || latitude > 90))
            return (null, null);

        // Validate longitude range
        if (longitude.HasValue && (longitude < -180 || longitude > 180))
            return (null, null);

        return (latitude, longitude);
    }

    /// <summary>
    /// Sanitize MMSI (Maritime Mobile Service Identity) - 9 digit number
    /// </summary>
    public static string? SanitizeMmsi(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        // Remove non-digits
        var sanitized = Regex.Replace(input, @"\D", string.Empty);

        // MMSI must be exactly 9 digits
        if (sanitized.Length != 9)
            return null;

        return sanitized;
    }

    [GeneratedRegex(@"<[^>]*>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagPattern();

    [GeneratedRegex(@"<script[^>]*>[\s\S]*?</script>", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex ScriptTagPattern();

    [GeneratedRegex(@"(\b(SELECT|INSERT|UPDATE|DELETE|DROP|UNION|EXEC|EXECUTE)\b)|('|--|;|/\*|\*/)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex SqlInjectionPattern();

    [GeneratedRegex(@"\.\.[\\/]|[\\/]\.\.|\.\./|\.\.\\", RegexOptions.Compiled)]
    private static partial Regex PathTraversalPattern();
}
