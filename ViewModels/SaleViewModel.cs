using System.ComponentModel.DataAnnotations;

namespace CandyShop.ViewModels;

/// <summary>Data shown on the Sales page: sellable products grouped by category.</summary>
public class SaleViewModel
{
    public IReadOnlyList<SaleCategoryGroupViewModel> Groups { get; init; } = [];

    public int ProductCount => Groups.Sum(g => g.Products.Count);
}

public class SaleCategoryGroupViewModel
{
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public IReadOnlyList<SellableProductViewModel> Products { get; init; } = [];
}

public class SellableProductViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal Price { get; init; }
}

/// <summary>
/// What the browser is allowed to submit: product ids and quantities only.
/// Prices and totals are always resolved server-side from the database.
/// </summary>
public class CompleteSaleViewModel
{
    public List<CartLineInput> Items { get; set; } = [];
}

public class CartLineInput
{
    [Range(1, int.MaxValue, ErrorMessage = "Invalid product.")]
    public int ProductId { get; set; }

    [Range(1, 1000, ErrorMessage = "Quantity must be between 1 and 1000.")]
    public int Quantity { get; set; }
}
