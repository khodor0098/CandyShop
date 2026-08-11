using CandyShop.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CandyShop.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();

    /// <summary>
    /// SQLite has no decimal type: its native options are REAL (lossy binary floating point)
    /// or TEXT (sorts and compares as a string). Money is therefore stored as an INTEGER
    /// number of cents, which is exact and still sorts/compares correctly in SQL.
    /// </summary>
    private static readonly ValueConverter<decimal, long> MoneyConverter = new(
        money => (long)Math.Round(money * 100m, MidpointRounding.AwayFromZero),
        cents => cents / 100m);

    /// <summary>
    /// All timestamps are stored in UTC. SQLite hands dates back with DateTimeKind.Unspecified,
    /// which would silently break ToLocalTime() on display, so the kind is restored on read.
    /// </summary>
    private static readonly ValueConverter<DateTime, DateTime> UtcConverter = new(
        value => value.ToUniversalTime(),
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> NullableUtcConverter = new(
        value => value.HasValue ? value.Value.ToUniversalTime() : null,
        value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(entity =>
        {
            entity.Property(c => c.Name).IsRequired().HasMaxLength(60);
            entity.Property(c => c.CreatedAt).HasConversion(UtcConverter).IsRequired();
            entity.Property(c => c.UpdatedAt).HasConversion(NullableUtcConverter);
            entity.HasIndex(c => c.Name).IsUnique();
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(p => p.Name).IsRequired().HasMaxLength(100);
            entity.Property(p => p.Price).HasConversion(MoneyConverter).IsRequired();
            entity.Property(p => p.CreatedAt).HasConversion(UtcConverter).IsRequired();
            entity.Property(p => p.UpdatedAt).HasConversion(NullableUtcConverter);
            entity.HasIndex(p => p.IsActive);

            // A category that owns products can never be deleted - the DB enforces it too.
            entity.HasOne(p => p.Category)
                  .WithMany(c => c.Products)
                  .HasForeignKey(p => p.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Sale>(entity =>
        {
            entity.Property(s => s.Total).HasConversion(MoneyConverter).IsRequired();
            entity.Property(s => s.SaleDate).HasConversion(UtcConverter).IsRequired();
            entity.HasIndex(s => s.SaleDate);
        });

        modelBuilder.Entity<SaleItem>(entity =>
        {
            entity.Property(i => i.ProductName).IsRequired().HasMaxLength(100);
            entity.Property(i => i.UnitPrice).HasConversion(MoneyConverter).IsRequired();
            entity.Property(i => i.Total).HasConversion(MoneyConverter).IsRequired();

            // Deleting a sale removes its lines.
            entity.HasOne(i => i.Sale)
                  .WithMany(s => s.Items)
                  .HasForeignKey(i => i.SaleId)
                  .OnDelete(DeleteBehavior.Cascade);

            // A product that has been sold can never be deleted - the DB enforces it too.
            entity.HasOne(i => i.Product)
                  .WithMany(p => p.SaleItems)
                  .HasForeignKey(i => i.ProductId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(i => i.ProductId);
        });

        base.OnModelCreating(modelBuilder);
    }
}
