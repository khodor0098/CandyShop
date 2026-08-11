using System.ComponentModel.DataAnnotations;

namespace CandyShop.ViewModels;

/// <summary>Row in the products table.</summary>
public class ProductListItemViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;

    /// <summary>False when the owning category is deactivated, which also hides the product from Sales.</summary>
    public bool CategoryIsActive { get; init; }

    public decimal Price { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAtLocal { get; init; }
    public bool HasSales { get; init; }

    /// <summary>A product is only sellable when both it and its category are active.</summary>
    public bool IsSellable => IsActive && CategoryIsActive;
}

/// <summary>Create/edit form for a product.</summary>
public class ProductFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Product name is required.")]
    [StringLength(100, ErrorMessage = "Product name cannot be longer than 100 characters.")]
    [Display(Name = "Product Name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Category is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Please select a category.")]
    [Display(Name = "Category")]
    public int? CategoryId { get; set; }

    /// <summary>Options for the category dropdown, repopulated by the controller on every render.</summary>
    public IEnumerable<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> CategoryOptions { get; set; } = [];

    [Required(ErrorMessage = "Price is required.")]
    [Range(0.01, 99999.99, ErrorMessage = "Price must be greater than 0.")]
    [Display(Name = "Price")]
    public decimal? Price { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public bool IsEdit => Id != 0;
}
