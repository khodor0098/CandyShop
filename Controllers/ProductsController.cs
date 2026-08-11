using CandyShop.Data;
using CandyShop.Models;
using CandyShop.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CandyShop.Controllers;

[Authorize]
public class ProductsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(ApplicationDbContext db, ILogger<ProductsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var products = await _db.Products
            .AsNoTracking()
            .OrderBy(p => p.Category.Name)
            .ThenBy(p => p.Name)
            .Select(p => new
            {
                p.Id,
                p.Name,
                CategoryName = p.Category.Name,
                CategoryIsActive = p.Category.IsActive,
                p.Price,
                p.IsActive,
                p.CreatedAt,
                HasSales = p.SaleItems.Any()
            })
            .ToListAsync(ct);

        var rows = products.Select(p => new ProductListItemViewModel
        {
            Id = p.Id,
            Name = p.Name,
            CategoryName = p.CategoryName,
            CategoryIsActive = p.CategoryIsActive,
            Price = p.Price,
            IsActive = p.IsActive,
            CreatedAtLocal = p.CreatedAt.ToLocalTime(),
            HasSales = p.HasSales
        }).ToList();

        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var model = new ProductFormViewModel();

        if (!await _db.Categories.AnyAsync(c => c.IsActive, ct))
        {
            TempData["Error"] = "Create an active category first - every product must belong to one.";
            return RedirectToAction("Index", "Categories");
        }

        model.CategoryOptions = await BuildCategoryOptionsAsync(null, ct);
        return View("Form", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductFormViewModel model, CancellationToken ct)
    {
        model.Id = 0;
        model.Name = model.Name?.Trim() ?? string.Empty;

        if (!ModelState.IsValid)
        {
            return await RedisplayAsync(model, ct);
        }

        // Only active categories may be chosen for a new product.
        if (!await _db.Categories.AnyAsync(c => c.Id == model.CategoryId && c.IsActive, ct))
        {
            ModelState.AddModelError(nameof(model.CategoryId), "Please select an active category.");
            return await RedisplayAsync(model, ct);
        }

        if (await _db.Products.AnyAsync(p => p.Name == model.Name, ct))
        {
            ModelState.AddModelError(nameof(model.Name), "A product with this name already exists.");
            return await RedisplayAsync(model, ct);
        }

        var product = new Product
        {
            Name = model.Name,
            CategoryId = model.CategoryId!.Value,
            Price = model.Price!.Value,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _db.Products.Add(product);

        if (!await TrySaveAsync(ct))
        {
            return await RedisplayAsync(model, ct);
        }

        TempData["Success"] = $"Product \"{product.Name}\" was added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
        {
            TempData["Error"] = $"Product #{id} was not found.";
            return RedirectToAction(nameof(Index));
        }

        return View("Form", new ProductFormViewModel
        {
            Id = product.Id,
            Name = product.Name,
            CategoryId = product.CategoryId,
            Price = product.Price,
            IsActive = product.IsActive,
            CategoryOptions = await BuildCategoryOptionsAsync(product.CategoryId, ct)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProductFormViewModel model, CancellationToken ct)
    {
        model.Id = id;
        model.Name = model.Name?.Trim() ?? string.Empty;

        if (!ModelState.IsValid)
        {
            return await RedisplayAsync(model, ct);
        }

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
        {
            TempData["Error"] = $"Product #{id} was not found.";
            return RedirectToAction(nameof(Index));
        }

        // The product's existing category stays selectable even if it has been deactivated;
        // any other choice must be an active category.
        var categoryOk = model.CategoryId == product.CategoryId ||
                         await _db.Categories.AnyAsync(c => c.Id == model.CategoryId && c.IsActive, ct);
        if (!categoryOk)
        {
            ModelState.AddModelError(nameof(model.CategoryId), "Please select an active category.");
            return await RedisplayAsync(model, ct);
        }

        if (await _db.Products.AnyAsync(p => p.Name == model.Name && p.Id != id, ct))
        {
            ModelState.AddModelError(nameof(model.Name), "A product with this name already exists.");
            return await RedisplayAsync(model, ct);
        }

        // Editing name or price only affects future sales: SaleItem keeps its own snapshot.
        product.Name = model.Name;
        product.CategoryId = model.CategoryId!.Value;
        product.Price = model.Price!.Value;
        product.IsActive = model.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

        if (!await TrySaveAsync(ct))
        {
            return await RedisplayAsync(model, ct);
        }

        TempData["Success"] = $"Product \"{product.Name}\" was updated. Past sales keep their original prices.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Flips IsActive. Products are never physically deleted, so sales history is always intact.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id, CancellationToken ct)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
        {
            TempData["Error"] = $"Product #{id} was not found.";
            return RedirectToAction(nameof(Index));
        }

        product.IsActive = !product.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

        if (await TrySaveAsync(ct))
        {
            TempData["Success"] = $"Product \"{product.Name}\" is now {(product.IsActive ? "active" : "inactive")}.";
        }
        else
        {
            TempData["Error"] = "The product could not be updated because of a database error. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>Re-renders the form with a freshly loaded category list.</summary>
    private async Task<IActionResult> RedisplayAsync(ProductFormViewModel model, CancellationToken ct)
    {
        model.CategoryOptions = await BuildCategoryOptionsAsync(model.CategoryId, ct);
        return View("Form", model);
    }

    /// <summary>
    /// Active categories, plus <paramref name="includeCategoryId"/> even when inactive so an
    /// existing product does not silently lose its category while being edited.
    /// </summary>
    private async Task<IEnumerable<SelectListItem>> BuildCategoryOptionsAsync(int? includeCategoryId, CancellationToken ct)
    {
        var categories = await _db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive || c.Id == includeCategoryId)
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.IsActive })
            .ToListAsync(ct);

        return categories.Select(c => new SelectListItem
        {
            Value = c.Id.ToString(),
            Text = c.IsActive ? c.Name : $"{c.Name} (inactive)"
        });
    }

    /// <summary>Saves changes and turns database failures into a friendly model error.</summary>
    private async Task<bool> TrySaveAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Failed to save product changes.");
            ModelState.AddModelError(string.Empty, "The product could not be saved because of a database error. Please try again.");
            return false;
        }
    }
}
