using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CandyShop.Migrations
{
    /// <summary>
    /// Adds the Category table and makes every product belong to a category.
    ///
    /// The order of operations matters: the Categories table and a fallback category are
    /// created first, existing products are backfilled onto it, and only then is the foreign
    /// key added. Adding the constraint first would leave pre-existing rows pointing at
    /// CategoryId 0, which does not exist.
    ///
    /// (The invoice feature needs no schema change - it is rendered from the Sale/SaleItem
    /// rows that already exist.)
    /// </summary>
    public partial class AddCategoriesAndInvoiceSupport : Migration
    {
        /// <summary>Must stay in sync with DbInitializer.DefaultCategoryName.</summary>
        private const string DefaultCategoryName = "Uncategorized";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Only needed when the database already holds products; a fresh database is left
            // clean for the seeder to fill with real categories.
            migrationBuilder.Sql($"""
                INSERT INTO "Categories" ("Name", "IsActive", "CreatedAt")
                SELECT '{DefaultCategoryName}', 1, strftime('%Y-%m-%d %H:%M:%S.0000000', 'now')
                WHERE EXISTS (SELECT 1 FROM "Products")
                  AND NOT EXISTS (SELECT 1 FROM "Categories" WHERE "Name" = '{DefaultCategoryName}');
                """);

            migrationBuilder.Sql($"""
                UPDATE "Products"
                SET "CategoryId" = (SELECT "Id" FROM "Categories" WHERE "Name" = '{DefaultCategoryName}')
                WHERE "CategoryId" = 0;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Categories_CategoryId",
                table: "Products",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Categories_CategoryId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_CategoryId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Products");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}
