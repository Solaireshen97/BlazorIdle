using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorIdle.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddItemCategoryAndRarityToShopItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ItemCategory",
                table: "shop_items",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rarity",
                table: "shop_items",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: "alchemist_shop_elixir",
                columns: new[] { "ItemCategory", "Rarity" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: "alchemist_shop_greater_health",
                columns: new[] { "ItemCategory", "Rarity" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: "alchemist_shop_rare_ingredient",
                columns: new[] { "ItemCategory", "Rarity" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: "alchemist_shop_scroll",
                columns: new[] { "ItemCategory", "Rarity" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: "general_shop_bread",
                columns: new[] { "ItemCategory", "Rarity" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: "general_shop_health_potion",
                columns: new[] { "ItemCategory", "Rarity" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: "general_shop_mana_potion",
                columns: new[] { "ItemCategory", "Rarity" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: "weapon_shop_iron_sword",
                columns: new[] { "ItemCategory", "Rarity" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: "weapon_shop_steel_sword",
                columns: new[] { "ItemCategory", "Rarity" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: "weapon_shop_wooden_shield",
                columns: new[] { "ItemCategory", "Rarity" },
                values: new object[] { null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ItemCategory",
                table: "shop_items");

            migrationBuilder.DropColumn(
                name: "Rarity",
                table: "shop_items");
        }
    }
}
