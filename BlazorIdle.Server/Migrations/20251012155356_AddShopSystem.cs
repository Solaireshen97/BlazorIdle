using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BlazorIdle.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShopSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "purchase_counters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ShopItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PeriodKey = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    PurchaseCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    LastPurchaseAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_counters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "purchase_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ShopId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ShopItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemDefinitionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    PriceJson = table.Column<string>(type: "TEXT", nullable: false),
                    PurchasedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EconomyEventId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "shop_definitions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "🏪"),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false, defaultValue: ""),
                    UnlockCondition = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_definitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "shop_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ShopId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ItemType = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemDefinitionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    PriceJson = table.Column<string>(type: "TEXT", nullable: false),
                    StockLimit = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: -1),
                    CurrentStock = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    PurchaseLimitJson = table.Column<string>(type: "TEXT", nullable: true),
                    RequiredLevel = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    UnlockCondition = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shop_items_shop_definitions_ShopId",
                        column: x => x.ShopId,
                        principalTable: "shop_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "shop_definitions",
                columns: new[] { "Id", "CreatedAt", "Description", "Icon", "IsEnabled", "Name", "SortOrder", "Type", "UnlockCondition", "UpdatedAt" },
                values: new object[,]
                {
                    { "alchemist_shop", new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "出售药剂和炼金材料", "🧪", true, "炼金术士", 3, 1, "level>=10", new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "general_shop", new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "出售基础物品和消耗品", "🏪", true, "杂货铺", 1, 0, null, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "weapon_shop", new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "出售各类武器和防具", "⚔️", true, "武器店", 2, 0, "level>=5", new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "shop_items",
                columns: new[] { "Id", "CreatedAt", "Description", "DisplayName", "Icon", "IsEnabled", "ItemDefinitionId", "ItemType", "PriceJson", "PurchaseLimitJson", "RequiredLevel", "ShopId", "SortOrder", "StockLimit", "UnlockCondition", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "恢复100点生命值", "小型生命药水", "🧪", true, "potion_health_small", 0, "{\"CurrencyType\":0,\"Amount\":50,\"CurrencyId\":null}", null, 1, "general_shop", 1, -1, null, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("10000000-0000-0000-0000-000000000002"), new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "恢复250点生命值", "中型生命药水", "🧪", true, "potion_health_medium", 0, "{\"CurrencyType\":0,\"Amount\":150,\"CurrencyId\":null}", null, 5, "general_shop", 2, -1, null, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("10000000-0000-0000-0000-000000000003"), new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "恢复饱食度", "面包", "🍞", true, "food_bread", 0, "{\"CurrencyType\":0,\"Amount\":10,\"CurrencyId\":null}", null, 1, "general_shop", 3, -1, null, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("10000000-0000-0000-0000-000000000101"), new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "基础单手武器", "铁剑", "⚔️", true, "weapon_iron_sword", 1, "{\"CurrencyType\":0,\"Amount\":500,\"CurrencyId\":null}", null, 5, "weapon_shop", 1, -1, null, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("10000000-0000-0000-0000-000000000102"), new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "基础胸甲", "皮甲", "🛡️", true, "armor_leather_chest", 1, "{\"CurrencyType\":0,\"Amount\":400,\"CurrencyId\":null}", null, 5, "weapon_shop", 2, -1, null, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "shop_items",
                columns: new[] { "Id", "CreatedAt", "CurrentStock", "Description", "DisplayName", "Icon", "IsEnabled", "ItemDefinitionId", "ItemType", "PriceJson", "PurchaseLimitJson", "RequiredLevel", "ShopId", "SortOrder", "StockLimit", "UnlockCondition", "UpdatedAt" },
                values: new object[] { new Guid("10000000-0000-0000-0000-000000000201"), new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), 10, "恢复500点生命值", "大型生命药水", "🧪", true, "potion_health_large", 0, "{\"CurrencyType\":0,\"Amount\":300,\"CurrencyId\":null}", "{\"LimitType\":1,\"MaxPurchases\":5,\"ResetHour\":0}", 10, "alchemist_shop", 1, 10, null, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "shop_items",
                columns: new[] { "Id", "CreatedAt", "Description", "DisplayName", "Icon", "IsEnabled", "ItemDefinitionId", "ItemType", "PriceJson", "PurchaseLimitJson", "RequiredLevel", "ShopId", "SortOrder", "StockLimit", "UnlockCondition", "UpdatedAt" },
                values: new object[] { new Guid("10000000-0000-0000-0000-000000000202"), new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "炼金材料", "普通草药", "🌿", true, "material_herb_common", 2, "{\"CurrencyType\":0,\"Amount\":20,\"CurrencyId\":null}", null, 10, "alchemist_shop", 2, -1, null, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_counters_CharacterId_ShopItemId_PeriodKey",
                table: "purchase_counters",
                columns: new[] { "CharacterId", "ShopItemId", "PeriodKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_counters_ExpiresAt",
                table: "purchase_counters",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_records_CharacterId",
                table: "purchase_records",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_records_CharacterId_PurchasedAt",
                table: "purchase_records",
                columns: new[] { "CharacterId", "PurchasedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_records_PurchasedAt",
                table: "purchase_records",
                column: "PurchasedAt");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_records_ShopId",
                table: "purchase_records",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_records_ShopItemId",
                table: "purchase_records",
                column: "ShopItemId");

            migrationBuilder.CreateIndex(
                name: "IX_shop_definitions_IsEnabled",
                table: "shop_definitions",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_shop_definitions_SortOrder",
                table: "shop_definitions",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_shop_definitions_Type",
                table: "shop_definitions",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_shop_items_IsEnabled",
                table: "shop_items",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_shop_items_ItemDefinitionId",
                table: "shop_items",
                column: "ItemDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_shop_items_ItemType",
                table: "shop_items",
                column: "ItemType");

            migrationBuilder.CreateIndex(
                name: "IX_shop_items_ShopId",
                table: "shop_items",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_shop_items_ShopId_SortOrder",
                table: "shop_items",
                columns: new[] { "ShopId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchase_counters");

            migrationBuilder.DropTable(
                name: "purchase_records");

            migrationBuilder.DropTable(
                name: "shop_items");

            migrationBuilder.DropTable(
                name: "shop_definitions");
        }
    }
}
