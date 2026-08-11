using System.ComponentModel.DataAnnotations;

namespace CandyShop.Models;

/// <summary>
/// A candy/sweet item that can be sold. Products are never deleted, only deactivated,
/// so that historical sales keep a valid reference.
/// </summary>
public class Product
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Product name is required.")]
    [StringLength(100, ErrorMessage = "Product name cannot be longer than 100 characters.")]
    [Display(Name = "Product Name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Current selling price. Stored as integer cents in SQLite (see ApplicationDbContext).</summary>
    [Range(0.01, 99999.99, ErrorMessage = "Price must be greater than 0.")]
    [DataType(DataType.Currency)]
    public decimal Price { get; set; }

    /// <summary>Every product belongs to exactly one category.</summary>
    [Display(Name = "Category")]
    public int CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    /// <summary>UTC creation timestamp.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the last edit, null if never edited.</summary>
    public DateTime? UpdatedAt { get; set; }

    public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
}
