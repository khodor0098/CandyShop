namespace CandyShop.ViewModels;

/// <summary>
/// Printable receipt for a sale that has already been saved. Every field comes from the
/// stored Sale/SaleItem rows, so an invoice always reflects a real, persisted sale.
/// </summary>
public class InvoiceViewModel
{
    public string StoreName { get; init; } = string.Empty;
    public string? StoreSubtitle { get; init; }
    public string FooterMessage { get; init; } = string.Empty;

    public int SaleId { get; init; }

    /// <summary>Sale timestamp converted to local time for printing.</summary>
    public DateTime SaleDateLocal { get; init; }

    public decimal Total { get; init; }

    public IReadOnlyList<InvoiceLineViewModel> Lines { get; init; } = [];

    public int ItemCount => Lines.Sum(l => l.Quantity);

    /// <summary>True when the invoice is shown straight after completing the sale.</summary>
    public bool JustCompleted { get; init; }
}

public class InvoiceLineViewModel
{
    public string ProductName { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
    public int Quantity { get; init; }
    public decimal Total { get; init; }
}
