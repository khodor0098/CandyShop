using CandyShop.Configuration;
using CandyShop.Data;
using CandyShop.Models;
using CandyShop.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CandyShop.Controllers;

[Authorize]
public class SalesController : Controller
{
    private const int MaxQuantityPerLine = 1000;

    private readonly ApplicationDbContext _db;
    private readonly IOptionsMonitor<StoreOptions> _store;
    private readonly ILogger<SalesController> _logger;

    public SalesController(ApplicationDbContext db, IOptionsMonitor<StoreOptions> store, ILogger<SalesController> logger)
    {
        _db = db;
        _store = store;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        // Sellable = active product in an active category.
        var products = await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive && p.Category.IsActive)
            .OrderBy(p => p.Category.Name)
            .ThenBy(p => p.Name)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Price,
                p.CategoryId,
                CategoryName = p.Category.Name
            })
            .ToListAsync(ct);

        var groups = products
            .GroupBy(p => new { p.CategoryId, p.CategoryName })
            .Select(g => new SaleCategoryGroupViewModel
            {
                CategoryId = g.Key.CategoryId,
                CategoryName = g.Key.CategoryName,
                Products = g.Select(p => new SellableProductViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Price = p.Price
                }).ToList()
            })
            .ToList();

        return View(new SaleViewModel { Groups = groups });
    }

    /// <summary>
    /// Persists a sale, then redirects to its invoice. The browser submits product ids and
    /// quantities only: unit prices and all totals are read from the database here, and
    /// Sale + SaleItems are written in one transaction so a partial sale can never be stored.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(CompleteSaleViewModel model, CancellationToken ct)
    {
        // Collapse duplicate lines for the same product before validating.
        var requested = (model.Items ?? [])
            .Where(i => i.ProductId > 0 && i.Quantity > 0)
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .ToList();

        if (requested.Count == 0)
        {
            TempData["Error"] = "The cart is empty. Add at least one product before completing a sale.";
            return RedirectToAction(nameof(Index));
        }

        if (requested.Any(i => i.Quantity > MaxQuantityPerLine))
        {
            TempData["Error"] = $"Quantity must be between 1 and {MaxQuantityPerLine} per product.";
            return RedirectToAction(nameof(Index));
        }

        var productIds = requested.Select(i => i.ProductId).ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id) && p.IsActive && p.Category.IsActive)
            .ToDictionaryAsync(p => p.Id, ct);

        // Anything missing was deactivated (directly or via its category) between page load and submission.
        if (products.Count != requested.Count)
        {
            TempData["Error"] = "One or more products are no longer available. The product list has been refreshed - please rebuild the sale.";
            return RedirectToAction(nameof(Index));
        }

        var sale = new Sale { SaleDate = DateTime.UtcNow, Total = 0m };

        foreach (var line in requested)
        {
            var product = products[line.ProductId];
            var lineTotal = product.Price * line.Quantity;

            sale.Items.Add(new SaleItem
            {
                ProductId = product.Id,
                ProductName = product.Name,   // historical snapshot
                UnitPrice = product.Price,    // historical snapshot
                Quantity = line.Quantity,
                Total = lineTotal
            });

            sale.Total += lineTotal;
        }

        try
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            _db.Sales.Add(sale);
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Failed to save sale.");
            TempData["Error"] = "The sale could not be saved because of a database error. Please try again.";
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = $"Sale #{sale.Id} completed successfully! Total: {sale.Total:C}";

        // The sale is committed before the invoice is rendered, so every invoice
        // corresponds to a real, saved sale.
        return RedirectToAction(nameof(Invoice), new { id = sale.Id, justCompleted = true });
    }

    /// <summary>Printable invoice/receipt for a saved sale.</summary>
    [HttpGet]
    public async Task<IActionResult> Invoice(int id, bool justCompleted, CancellationToken ct)
    {
        var sale = await _db.Sales
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new
            {
                s.Id,
                s.SaleDate,
                s.Total,
                Lines = s.Items
                    .OrderBy(i => i.Id)
                    .Select(i => new InvoiceLineViewModel
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

        var store = _store.CurrentValue;

        return View(new InvoiceViewModel
        {
            StoreName = string.IsNullOrWhiteSpace(store.Name) ? "CANDY VAN" : store.Name,
            StoreSubtitle = store.Subtitle,
            FooterMessage = store.FooterMessage,
            SaleId = sale.Id,
            SaleDateLocal = sale.SaleDate.ToLocalTime(),
            Total = sale.Total,
            Lines = sale.Lines,
            JustCompleted = justCompleted
        });
    }
}
