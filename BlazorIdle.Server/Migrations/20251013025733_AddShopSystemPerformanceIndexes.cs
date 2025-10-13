using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorIdle.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddShopSystemPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_shop_items_ItemCategory",
                table: "shop_items",
                column: "ItemCategory");

            migrationBuilder.CreateIndex(
                name: "IX_shop_items_Rarity",
                table: "shop_items",
                column: "Rarity");

            migrationBuilder.CreateIndex(
                name: "IX_ShopItems_IsEnabled_MinLevel",
                table: "shop_items",
                columns: new[] { "IsEnabled", "MinLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_ShopItems_ShopId_IsEnabled",
                table: "shop_items",
                columns: new[] { "ShopId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_ShopDefinitions_IsEnabled_SortOrder",
                table: "shop_definitions",
                columns: new[] { "IsEnabled", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRecords_CharacterId_PurchasedAt",
                table: "purchase_records",
                columns: new[] { "CharacterId", "PurchasedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_shop_items_ItemCategory",
                table: "shop_items");

            migrationBuilder.DropIndex(
                name: "IX_shop_items_Rarity",
                table: "shop_items");

            migrationBuilder.DropIndex(
                name: "IX_ShopItems_IsEnabled_MinLevel",
                table: "shop_items");

            migrationBuilder.DropIndex(
                name: "IX_ShopItems_ShopId_IsEnabled",
                table: "shop_items");

            migrationBuilder.DropIndex(
                name: "IX_ShopDefinitions_IsEnabled_SortOrder",
                table: "shop_definitions");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRecords_CharacterId_PurchasedAt",
                table: "purchase_records");
        }
    }
}
