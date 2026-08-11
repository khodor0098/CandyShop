using System.ComponentModel.DataAnnotations;

namespace CandyShop.Models;

/// <summary>
/// Groups products (Chocolate, Gummies, ...). Like products, categories are never deleted,
/// only deactivated, so products and sales history keep a valid reference.
/// </summary>
public class Category
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Category name is required.")]
    [StringLength(60, ErrorMessage = "Category name cannot be longer than 60 characters.")]
    [Display(Name = "Category Name")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    /// <summary>UTC creation timestamp.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the last edit, null if never edited.</summary>
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
