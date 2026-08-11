namespace CandyShop.Models;

/// <summary>
/// One line of a sale. Name and unit price are copied from the product at the moment
/// of sale on purpose: editing or renaming a product must never alter sales history.
/// </summary>
public class SaleItem
{
    public int Id { get; set; }

    public int SaleId { get; set; }
    public Sale Sale { get; set; } = null!;

    /// <summary>Reference to the product for reporting/filtering. Restricted delete keeps history intact.</summary>
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    /// <summary>Product name as it was at the time of sale (historical snapshot).</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>Product price as it was at the time of sale (historical snapshot).</summary>
    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    /// <summary>UnitPrice * Quantity, calculated server-side.</summary>
    public decimal Total { get; set; }
}
