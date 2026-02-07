namespace CoralLedger.Blue.Web.Services;

/// <summary>
/// Provides helper methods for user display operations
/// </summary>
public static class UserDisplayHelper
{
    /// <summary>
    /// Gets the initials from a user's name or email
    /// </summary>
    /// <param name="nameOrEmail">The user's name or email</param>
    /// <returns>1-2 character initials</returns>
    public static string GetUserInitials(string? nameOrEmail)
    {
        if (string.IsNullOrWhiteSpace(nameOrEmail))
        {
            return "?";
        }

        // Try to split by space to get first and last name
        var parts = nameOrEmail.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length >= 2)
        {
            // Use first letter of first and last name
            return $"{parts[0][0]}{parts[1][0]}".ToUpper();
        }
        
        // Use first character only
        return nameOrEmail[0].ToString().ToUpper();
    }
}
