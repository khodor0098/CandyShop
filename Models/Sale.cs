namespace CandyShop.Models;

/// <summary>
/// A completed transaction. The total is always calculated server-side from the
/// line items and is never accepted from the browser.
/// </summary>
public class Sale
{
    public int Id { get; set; }

    /// <summary>UTC timestamp of when the sale was completed.</summary>
    public DateTime SaleDate { get; set; }

    /// <summary>Sum of all line totals. Stored as integer cents in SQLite.</summary>
    public decimal Total { get; set; }

    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
}
