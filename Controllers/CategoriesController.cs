using CandyShop.Data;
using CandyShop.Models;
using CandyShop.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CandyShop.Controllers;

[Authorize]
public class CategoriesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<CategoriesController> _logger;

    public CategoriesController(ApplicationDbContext db, ILogger<CategoriesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var categories = await _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.IsActive,
                c.CreatedAt,
                ProductCount = c.Products.Count,
                ActiveProductCount = c.Products.Count(p => p.IsActive)
            })
            .ToListAsync(ct);

        var rows = categories.Select(c => new CategoryListItemViewModel
        {
            Id = c.Id,
            Name = c.Name,
            IsActive = c.IsActive,
            CreatedAtLocal = c.CreatedAt.ToLocalTime(),
            ProductCount = c.ProductCount,
            ActiveProductCount = c.ActiveProductCount
        }).ToList();

        return View(rows);
    }

    [HttpGet]
    public IActionResult Create() => View("Form", new CategoryFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryFormViewModel model, CancellationToken ct)
    {
        model.Id = 0;
        model.Name = model.Name?.Trim() ?? string.Empty;

        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        if (await _db.Categories.AnyAsync(c => c.Name == model.Name, ct))
        {
            ModelState.AddModelError(nameof(model.Name), "A category with this name already exists.");
            return View("Form", model);
        }

        _db.Categories.Add(new Category
        {
            Name = model.Name,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        });

        if (!await TrySaveAsync(ct))
        {
            return View("Form", model);
        }

        TempData["Success"] = $"Category \"{model.Name}\" was added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var category = await _db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null)
        {
            TempData["Error"] = $"Category #{id} was not found.";
            return RedirectToAction(nameof(Index));
        }

        return View("Form", new CategoryFormViewModel
        {
            Id = category.Id,
            Name = category.Name,
            IsActive = category.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CategoryFormViewModel model, CancellationToken ct)
    {
        model.Id = id;
        model.Name = model.Name?.Trim() ?? string.Empty;

        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null)
        {
            TempData["Error"] = $"Category #{id} was not found.";
            return RedirectToAction(nameof(Index));
        }

        if (await _db.Categories.AnyAsync(c => c.Name == model.Name && c.Id != id, ct))
        {
            ModelState.AddModelError(nameof(model.Name), "A category with this name already exists.");
            return View("Form", model);
        }

        category.Name = model.Name;
        category.IsActive = model.IsActive;
        category.UpdatedAt = DateTime.UtcNow;

        if (!await TrySaveAsync(ct))
        {
            return View("Form", model);
        }

        TempData["Success"] = $"Category \"{category.Name}\" was updated.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Flips IsActive. Categories are never physically deleted, so products and sales
    /// history always keep a valid category reference. Deactivating a category also hides
    /// its products from the Sales page.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id, CancellationToken ct)
    {
        var category = await _db.Categories
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (category is null)
        {
            TempData["Error"] = $"Category #{id} was not found.";
            return RedirectToAction(nameof(Index));
        }

        category.IsActive = !category.IsActive;
        category.UpdatedAt = DateTime.UtcNow;

        if (!await TrySaveAsync(ct))
        {
            TempData["Error"] = "The category could not be updated because of a database error. Please try again.";
            return RedirectToAction(nameof(Index));
        }

        var affected = category.Products.Count(p => p.IsActive);
        TempData["Success"] = category.IsActive
            ? $"Category \"{category.Name}\" is now active."
            : $"Category \"{category.Name}\" is now inactive." +
              (affected > 0 ? $" {affected} active product(s) are hidden from the Sales page." : string.Empty);

        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> TrySaveAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Failed to save category changes.");
            ModelState.AddModelError(string.Empty, "The category could not be saved because of a database error. Please try again.");
            return false;
        }
    }
}
