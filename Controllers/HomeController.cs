using CandyShop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CandyShop.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    /// <summary>The application has no dashboard: "/" goes straight to the Sales page.</summary>
    [HttpGet]
    public IActionResult Index() => RedirectToAction("Index", "Sales");

    /// <summary>
    /// Friendly error page. The exception is written to the log only - never to the response.
    /// </summary>
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? statusCode = null)
    {
        var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        if (feature?.Error is not null)
        {
            _logger.LogError(feature.Error, "Unhandled exception on {Path}.", feature.Path);
        }

        return View(new ErrorViewModel
        {
            RequestId = HttpContext.TraceIdentifier,
            StatusCode = statusCode,
            Message = statusCode switch
            {
                404 => "The page you asked for does not exist.",
                403 => "You are not allowed to view that page.",
                _ => "Something went wrong while processing your request."
            }
        });
    }
}
