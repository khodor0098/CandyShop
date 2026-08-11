using System.Globalization;
using System.Threading.RateLimiting;
using CandyShop.Configuration;
using CandyShop.Data;
using CandyShop.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

// Utility mode: generate a PBKDF2 hash for the admin password without starting the web host.
//   dotnet run -- hash-password "YourPassword"
if (args.Length > 0 && args[0].Equals("hash-password", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
    {
        Console.Error.WriteLine("Usage: dotnet run -- hash-password \"YourPassword\"");
        return 1;
    }

    Console.WriteLine(PasswordHasher.Hash(args[1]));
    return 0;
}

// Money and dates are formatted consistently regardless of the machine's regional settings.
var appCulture = new CultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = appCulture;
CultureInfo.DefaultThreadCurrentUICulture = appCulture;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? "Data Source=candyshop.db";

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));

builder.Services.Configure<AdminCredentialsOptions>(
    builder.Configuration.GetSection(AdminCredentialsOptions.SectionName));
builder.Services.AddSingleton<AdminAuthenticator>();

// Store name and footer text printed on invoices.
builder.Services.Configure<StoreOptions>(
    builder.Configuration.GetSection(StoreOptions.SectionName));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
        options.Cookie.Name = "CandyShop.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // Sent over HTTPS when available, but still works on a plain-HTTP LAN setup in the van.
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

builder.Services.AddAuthorization();

// Every page requires authentication; [AllowAnonymous] opts individual actions out.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AuthorizeFilter());
});

// Slows down brute-force attempts against the single admin account.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("login", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    // Friendly error page; stack traces are logged, never shown.
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Sales}/{action=Index}/{id?}");

// Creates the SQLite file, applies pending migrations and seeds starter products when empty.
await DbInitializer.InitializeAsync(
    app.Services,
    seed: app.Configuration.GetValue("Database:SeedSampleProducts", true));

app.Run();
return 0;
