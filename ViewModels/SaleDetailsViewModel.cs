namespace CandyShop.ViewModels;

public class SaleDetailsViewModel
{
    public int SaleId { get; init; }

    /// <summary>Sale timestamp already converted to local time for display.</summary>
    public DateTime SaleDateLocal { get; init; }

    public decimal Total { get; init; }

    public IReadOnlyList<SaleDetailsLineViewModel> Lines { get; init; } = [];

    public int ItemCount => Lines.Sum(l => l.Quantity);
}

public class SaleDetailsLineViewModel
{
    public string ProductName { get; init; } = string.Empty;

    /// <summary>
    /// The product's current category. Unlike name and price this is not a historical
    /// snapshot: moving a product to another category updates it everywhere.
    /// </summary>
    public string CategoryName { get; init; } = string.Empty;

    public decimal UnitPrice { get; init; }
    public int Quantity { get; init; }
    public decimal Total { get; init; }
}
