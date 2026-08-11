namespace CandyShop.Security;

/// <summary>
/// The single admin account, bound from the "AdminCredentials" configuration section.
/// No user table exists and the password is never written to the database.
/// </summary>
public class AdminCredentialsOptions
{
    public const string SectionName = "AdminCredentials";

    public string Username { get; set; } = string.Empty;

    /// <summary>Plain-text password. Convenient for local use; prefer <see cref="PasswordHash"/> in production.</summary>
    public string? Password { get; set; }

    /// <summary>
    /// PBKDF2 hash in the format "PBKDF2$&lt;iterations&gt;$&lt;saltBase64&gt;$&lt;hashBase64&gt;".
    /// When set it takes precedence over <see cref="Password"/>.
    /// Generate one with: dotnet run -- hash-password "YourPassword"
    /// </summary>
    public string? PasswordHash { get; set; }
}
