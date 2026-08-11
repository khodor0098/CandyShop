using Microsoft.Extensions.Options;

namespace CandyShop.Security;

/// <summary>
/// Validates the single admin login against configuration. Registered as a singleton;
/// <see cref="IOptionsMonitor{T}"/> means credential changes in appsettings are picked up
/// without a restart.
/// </summary>
public class AdminAuthenticator
{
    private readonly IOptionsMonitor<AdminCredentialsOptions> _options;
    private readonly ILogger<AdminAuthenticator> _logger;

    public AdminAuthenticator(IOptionsMonitor<AdminCredentialsOptions> options, ILogger<AdminAuthenticator> logger)
    {
        _options = options;
        _logger = logger;
    }

    public bool Validate(string? username, string? password)
    {
        var admin = _options.CurrentValue;

        if (string.IsNullOrWhiteSpace(admin.Username) ||
            (string.IsNullOrWhiteSpace(admin.Password) && string.IsNullOrWhiteSpace(admin.PasswordHash)))
        {
            _logger.LogError("Admin credentials are not configured. Set the AdminCredentials section.");
            return false;
        }

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            return false;
        }

        // Username is compared case-insensitively; the password check runs regardless so that
        // a wrong username and a wrong password take a comparable amount of time.
        var usernameOk = string.Equals(username.Trim(), admin.Username, StringComparison.OrdinalIgnoreCase);

        var passwordOk = !string.IsNullOrWhiteSpace(admin.PasswordHash)
            ? PasswordHasher.Verify(password, admin.PasswordHash)
            : PasswordHasher.FixedTimeEquals(password, admin.Password!);

        return usernameOk && passwordOk;
    }

    public string AdminUsername => _options.CurrentValue.Username;
}
