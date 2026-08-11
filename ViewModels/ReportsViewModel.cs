using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CandyShop.ViewModels;

public class ReportsViewModel
{
    public ReportFilterViewModel Filter { get; set; } = new();

    /// <summary>
    /// Products for the filter dropdown (all products, including inactive ones, so history
    /// stays searchable). Carries the category id so the dropdown can narrow itself to the
    /// selected category in the browser.
    /// </summary>
    public IReadOnlyList<ReportProductOption> ProductOptions { get; set; } = [];

    /// <summary>Categories for the filter dropdown (all categories, including inactive ones).</summary>
    public IEnumerable<SelectListItem> CategoryOptions { get; set; } = [];

    public decimal TotalRevenue { get; set; }
    public int SaleCount { get; set; }
    public int ItemsSold { get; set; }

    public IReadOnlyList<ReportRowViewModel> Sales { get; set; } = [];

    /// <summary>
    /// True when a product or category filter is applied, so the UI can explain that the
    /// totals are subtotals for the matching lines rather than whole-sale totals.
    /// </summary>
    public bool IsProductFiltered => Filter.ProductId.HasValue || Filter.CategoryId.HasValue;

    public string RangeDescription => Filter.DateFrom == Filter.DateTo
        ? Filter.DateFrom?.ToString("yyyy-MM-dd") ?? "All time"
        : $"{Filter.DateFrom?.ToString("yyyy-MM-dd") ?? "start"} to {Filter.DateTo?.ToString("yyyy-MM-dd") ?? "today"}";
}

public class ReportFilterViewModel
{
    [DataType(DataType.Date)]
    [Display(Name = "Date From")]
    public DateTime? DateFrom { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Date To")]
    public DateTime? DateTo { get; set; }

    [Display(Name = "Category")]
    public int? CategoryId { get; set; }

    [Display(Name = "Product")]
    public int? ProductId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Sale ID must be a positive number.")]
    [Display(Name = "Sale ID")]
    public int? SaleId { get; set; }

    /// <summary>
    /// Set by the filter form. Without it an empty date range cannot be told apart from
    /// a first visit, which defaults to today.
    /// </summary>
    public bool Applied { get; set; }
}

public class ReportProductOption
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int CategoryId { get; init; }
    public bool IsActive { get; init; }
}

public class ReportRowViewModel
{
    public int SaleId { get; init; }

    /// <summary>Already converted to local time for display.</summary>
    public DateTime SaleDateLocal { get; init; }

    /// <summary>Number of units in the sale (restricted to the filtered product when one is selected).</summary>
    public int ItemCount { get; init; }

    /// <summary>Sale total, or the matching lines' subtotal when a product/category filter is applied.</summary>
    public decimal Total { get; init; }

    /// <summary>Comma-separated categories present in the (filtered) lines of this sale.</summary>
    public string Categories { get; init; } = string.Empty;
}
