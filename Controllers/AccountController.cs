using System.Security.Claims;
using CandyShop.Security;
using CandyShop.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CandyShop.Controllers;

[Authorize]
public class AccountController : Controller
{
    private readonly AdminAuthenticator _authenticator;
    private readonly ILogger<AccountController> _logger;

    public AccountController(AdminAuthenticator authenticator, ILogger<AccountController> logger)
    {
        _authenticator = authenticator;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Sales");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (!_authenticator.Validate(model.Username, model.Password))
        {
            // Deliberately vague: do not reveal whether the username or the password was wrong.
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            _logger.LogWarning("Failed login attempt from {RemoteIp}.", HttpContext.Connection.RemoteIpAddress);
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, _authenticator.AdminUsername),
            new(ClaimTypes.Role, "Admin")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false });

        _logger.LogInformation("Admin signed in.");

        return SafeRedirect(model.ReturnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied() => View();

    /// <summary>Only follows a return URL that points back into this application (open-redirect guard).</summary>
    private IActionResult SafeRedirect(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction("Index", "Sales");
}
