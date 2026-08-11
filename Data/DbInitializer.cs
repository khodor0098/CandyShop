using CandyShop.Models;
using Microsoft.EntityFrameworkCore;

namespace CandyShop.Data;

/// <summary>
/// Applies pending EF Core migrations at startup and seeds the starter categories and
/// products the first time the database is created.
/// </summary>
public static class DbInitializer
{
    /// <summary>
    /// Category assigned to products that arrived before categories existed, and the fallback
    /// used by the AddCategories migration. Must stay in sync with that migration.
    /// </summary>
    public const string DefaultCategoryName = "Uncategorized";

    /// <summary>
    /// Starter catalogue, grouped by category. Delete or edit these entries freely - they are
    /// only inserted when the Products table is completely empty, so an existing database is
    /// left untouched.
    /// </summary>
    private static readonly (string Category, (string Name, decimal Price)[] Products)[] SeedData =
    [
        ("Chocolate", [("Chocolate Bar", 1.50m)]),
        ("Gummies",   [("Gummy Bears", 1.00m), ("Candy Mix", 2.00m)]),
        ("Lollipops", [("Lollipop", 0.50m)]),
        ("Other",     [("Marshmallow", 1.25m)])
    ];

    public static async Task InitializeAsync(IServiceProvider services, bool seed, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DbInitializer));

        // Creates the SQLite file if missing and brings the schema up to the latest migration.
        await db.Database.MigrateAsync(ct);

        if (!seed || await db.Products.AnyAsync(ct))
        {
            return;
        }

        var now = DateTime.UtcNow;

        foreach (var (categoryName, products) in SeedData)
        {
            // A previous run (or the AddCategories migration) may already have created the category.
            var category = await db.Categories.FirstOrDefaultAsync(c => c.Name == categoryName, ct);
            if (category is null)
            {
                category = new Category { Name = categoryName, IsActive = true, CreatedAt = now };
                db.Categories.Add(category);
            }

            foreach (var (name, price) in products)
            {
                category.Products.Add(new Product
                {
                    Name = name,
                    Price = price,
                    IsActive = true,
                    CreatedAt = now
                });
            }
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Categories} starter categories and {Products} starter products.",
            SeedData.Length, SeedData.Sum(s => s.Products.Length));
    }
}
