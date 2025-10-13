using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorIdle.Server.Migrations
{
    /// <inheritdoc />
    public partial class SyncModel_2025101302 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: "weapon_shop_iron_sword",
                column: "PriceJson",
                value: "{\"CurrencyType\":0,\"CurrencyId\":null,\"Amount\":5}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: "weapon_shop_iron_sword",
                column: "PriceJson",
                value: "{\"CurrencyType\":0,\"CurrencyId\":null,\"Amount\":500}");
        }
    }
}
