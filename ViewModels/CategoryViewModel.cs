using System.ComponentModel.DataAnnotations;

namespace CandyShop.ViewModels;

/// <summary>Row in the categories table.</summary>
public class CategoryListItemViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime CreatedAtLocal { get; init; }
    public int ProductCount { get; init; }
    public int ActiveProductCount { get; init; }
}

/// <summary>Create/edit form for a category.</summary>
public class CategoryFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Category name is required.")]
    [StringLength(60, ErrorMessage = "Category name cannot be longer than 60 characters.")]
    [Display(Name = "Category Name")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public bool IsEdit => Id != 0;
}
