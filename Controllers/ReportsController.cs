using CandyShop.Data;
using CandyShop.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CandyShop.Controllers;

[Authorize]
public class ReportsController : Controller
{
    /// <summary>Safety cap on rows rendered in one report. The view warns when it is hit.</summary>
    private const int MaxRows = 500;

    private readonly ApplicationDbContext _db;

    public ReportsController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] ReportFilterViewModel filter, CancellationToken ct)
    {
        // First visit (no filter submitted yet): show today.
        if (!filter.Applied && filter is { DateFrom: null, DateTo: null, ProductId: null, CategoryId: null, SaleId: null })
        {
            var today = DateTime.Today;
            filter.DateFrom = today;
            filter.DateTo = today;
        }

        // Model errors must use the binding prefix ("Filter.X") so asp-validation-for finds them.
        if (filter.DateFrom.HasValue && filter.DateTo.HasValue && filter.DateFrom > filter.DateTo)
        {
            ModelState.AddModelError("Filter.DateFrom", "\"Date From\" cannot be later than \"Date To\".");
        }

        if (filter.CategoryId.HasValue && !await _db.Categories.AnyAsync(c => c.Id == filter.CategoryId, ct))
        {
            ModelState.AddModelError("Filter.CategoryId", "The selected category no longer exists.");
            filter.CategoryId = null;
        }

        if (filter.ProductId.HasValue && !await _db.Products.AnyAsync(p => p.Id == filter.ProductId, ct))
        {
            ModelState.AddModelError("Filter.ProductId", "The selected product no longer exists.");
            filter.ProductId = null;
        }

        var model = new ReportsViewModel
        {
            Filter = filter,
            ProductOptions = await BuildProductOptionsAsync(ct),
            CategoryOptions = await BuildCategoryOptionsAsync(ct)
        };

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var query = _db.Sales.AsNoTracking();

        // Date pickers are local dates; sales are stored in UTC, so convert the day boundaries.
        if (filter.DateFrom.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(filter.DateFrom.Value.Date, DateTimeKind.Local).ToUniversalTime();
            query = query.Where(s => s.SaleDate >= fromUtc);
        }

        if (filter.DateTo.HasValue)
        {
            var toUtcExclusive = DateTime.SpecifyKind(filter.DateTo.Value.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
            query = query.Where(s => s.SaleDate < toUtcExclusive);
        }

        if (filter.SaleId.HasValue)
        {
            query = query.Where(s => s.Id == filter.SaleId.Value);
        }

        var productId = filter.ProductId ?? 0;
        var categoryId = filter.CategoryId ?? 0;

        // Keep only sales that contain at least one matching line.
        if (productId != 0)
        {
            query = query.Where(s => s.Items.Any(i => i.ProductId == productId));
        }

        if (categoryId != 0)
        {
            query = query.Where(s => s.Items.Any(i => i.Product.CategoryId == categoryId));
        }

        var sales = await query
            .OrderByDescending(s => s.SaleDate)
            .ThenByDescending(s => s.Id)
            .Take(MaxRows + 1)
            .Select(s => new
            {
                s.Id,
                s.SaleDate,
                s.Total,
                // With a product/category filter active, the row shows the matching lines only.
                Lines = s.Items
                    .Where(i => (productId == 0 || i.ProductId == productId)
                                && (categoryId == 0 || i.Product.CategoryId == categoryId))
                    .Select(i => new { i.Quantity, i.Total, CategoryName = i.Product.Category.Name })
                    .ToList()
            })
            .ToListAsync(ct);

        if (sales.Count > MaxRows)
        {
            sales.RemoveRange(MaxRows, sales.Count - MaxRows);
            ViewData["Truncated"] = MaxRows;
        }

        // Money is stored as integer cents behind a value converter, which SQL-side SUM cannot
        // translate. Totals are therefore aggregated in memory over the filtered rows.
        var rows = sales.Select(s => new ReportRowViewModel
        {
            SaleId = s.Id,
            SaleDateLocal = s.SaleDate.ToLocalTime(),
            ItemCount = s.Lines.Sum(l => l.Quantity),
            Total = productId == 0 && categoryId == 0 ? s.Total : s.Lines.Sum(l => l.Total),
            Categories = string.Join(", ", s.Lines.Select(l => l.CategoryName).Distinct().OrderBy(n => n))
        }).ToList();

        model.Sales = rows;
        model.SaleCount = rows.Count;
        model.ItemsSold = rows.Sum(r => r.ItemCount);
        model.TotalRevenue = rows.Sum(r => r.Total);

        if (filter.SaleId.HasValue && rows.Count == 0)
        {
            ViewData["Info"] = $"No sale found with ID #{filter.SaleId} in the selected range.";
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Sale(int id, CancellationToken ct)
    {
        var sale = await _db.Sales
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new SaleDetailsViewModel
            {
                SaleId = s.Id,
                SaleDateLocal = s.SaleDate,
                Total = s.Total,
                Lines = s.Items
                    .OrderBy(i => i.Id)
                    .Select(i => new SaleDetailsLineViewModel
                    {
                        ProductName = i.ProductName,
                        CategoryName = i.Product.Category.Name,
                        UnitPrice = i.UnitPrice,
                        Quantity = i.Quantity,
                        Total = i.Total
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (sale is null)
        {
            TempData["Error"] = $"Sale #{id} was not found.";
            return RedirectToAction(nameof(Index));
        }

        // SaleDateLocal is still UTC coming out of the projection; convert for display.
        var model = new SaleDetailsViewModel
        {
            SaleId = sale.SaleId,
            SaleDateLocal = sale.SaleDateLocal.ToLocalTime(),
            Total = sale.Total,
            Lines = sale.Lines
        };

        return View(model);
    }

    private async Task<IReadOnlyList<ReportProductOption>> BuildProductOptionsAsync(CancellationToken ct)
    {
        // Inactive products are included so historical sales remain searchable.
        return await _db.Products
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new ReportProductOption
            {
                Id = p.Id,
                Name = p.Name,
                CategoryId = p.CategoryId,
                IsActive = p.IsActive
            })
            .ToListAsync(ct);
    }

    private async Task<IEnumerable<SelectListItem>> BuildCategoryOptionsAsync(CancellationToken ct)
    {
        var categories = await _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.IsActive })
            .ToListAsync(ct);

        return categories.Select(c => new SelectListItem
        {
            Value = c.Id.ToString(),
            Text = c.IsActive ? c.Name : $"{c.Name} (inactive)"
        });
    }
}
