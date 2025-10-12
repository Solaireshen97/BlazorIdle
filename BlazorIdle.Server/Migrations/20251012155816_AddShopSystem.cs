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
                    Id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ShopItemId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PurchaseCount = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodStartAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastPurchasedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_counters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "purchase_records",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ShopId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ShopItemId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ItemDefinitionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    PriceJson = table.Column<string>(type: "TEXT", nullable: false),
                    PurchasedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EconomyEventId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
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
                    Icon = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    UnlockCondition = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
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
                    Id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ShopId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ItemDefinitionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ItemName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ItemIcon = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    PriceJson = table.Column<string>(type: "TEXT", nullable: false),
                    PurchaseLimitJson = table.Column<string>(type: "TEXT", nullable: false),
                    StockQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    MinLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
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
                    { "alchemist_shop", new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "出售高级药剂和特殊物品", "🧪", true, "炼金术士", 3, 1, "level>=10", new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "general_shop", new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "出售各类日常消耗品和基础装备", "🏪", true, "杂货铺", 1, 0, null, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "weapon_shop", new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "专业的武器装备商店", "⚔️", true, "武器店", 2, 0, null, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "shop_items",
                columns: new[] { "Id", "CreatedAt", "IsEnabled", "ItemDefinitionId", "ItemIcon", "ItemName", "MinLevel", "PriceJson", "PurchaseLimitJson", "ShopId", "SortOrder", "StockQuantity", "UpdatedAt" },
                values: new object[,]
                {
                    { "alchemist_shop_elixir", new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), true, "elixir_of_strength", "💪", "力量药剂", 15, "{\"CurrencyType\":0,\"CurrencyId\":null,\"Amount\":500}", "{\"Type\":3,\"MaxPurchases\":3,\"ResetPeriodSeconds\":null}", "alchemist_shop", 2, -1, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "alchemist_shop_greater_health", new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), true, "health_potion_greater", "🧪", "高级生命药水", 10, "{\"CurrencyType\":0,\"CurrencyId\":null,\"Amount\":200}", "{\"Type\":2,\"MaxPurchases\":5,\"ResetPeriodSeconds\":null}", "alchemist_shop", 1, -1, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "alchemist_shop_rare_ingredient", new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), true, "dragon_scale", "🐉", "龙鳞", 20, "{\"CurrencyType\":0,\"CurrencyId\":null,\"Amount\":5000}", "{\"Type\":1,\"MaxPurchases\":1,\"ResetPeriodSeconds\":null}", "alchemist_shop", 3, -1, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "alchemist_shop_scroll", new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), true, "teleport_scroll", "📜", "传送卷轴", 10, "{\"CurrencyType\":0,\"CurrencyId\":null,\"Amount\":1000}", "{\"Type\":0,\"MaxPurchases\":0,\"ResetPeriodSeconds\":null}", "alchemist_shop", 4, -1, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "general_shop_bread", new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), true, "bread", "🍞", "面包", 1, "{\"CurrencyType\":0,\"CurrencyId\":null,\"Amount\":10}", "{\"Type\":0,\"MaxPurchases\":0,\"ResetPeriodSeconds\":null}", "general_shop", 3, -1, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "general_shop_health_potion", new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), true, "health_potion_small", "🧪", "小型生命药水", 1, "{\"CurrencyType\":0,\"CurrencyId\":null,\"Amount\":50}", "{\"Type\":0,\"MaxPurchases\":0,\"ResetPeriodSeconds\":null}", "general_shop", 1, -1, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "general_shop_mana_potion", new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), true, "mana_potion_small", "💙", "小型魔法药水", 1, "{\"CurrencyType\":0,\"CurrencyId\":null,\"Amount\":50}", "{\"Type\":0,\"MaxPurchases\":0,\"ResetPeriodSeconds\":null}", "general_shop", 2, -1, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "weapon_shop_iron_sword", new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), true, "iron_sword", "⚔️", "铁剑", 1, "{\"CurrencyType\":0,\"CurrencyId\":null,\"Amount\":500}", "{\"Type\":0,\"MaxPurchases\":0,\"ResetPeriodSeconds\":null}", "weapon_shop", 1, -1, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "weapon_shop_steel_sword", new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), true, "steel_sword", "⚔️", "钢剑", 5, "{\"CurrencyType\":0,\"CurrencyId\":null,\"Amount\":1500}", "{\"Type\":0,\"MaxPurchases\":0,\"ResetPeriodSeconds\":null}", "weapon_shop", 2, -1, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "weapon_shop_wooden_shield", new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), true, "wooden_shield", "🛡️", "木盾", 1, "{\"CurrencyType\":0,\"CurrencyId\":null,\"Amount\":300}", "{\"Type\":0,\"MaxPurchases\":0,\"ResetPeriodSeconds\":null}", "weapon_shop", 3, -1, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_counters_CharacterId",
                table: "purchase_counters",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_counters_CharacterId_ShopItemId",
                table: "purchase_counters",
                columns: new[] { "CharacterId", "ShopItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_counters_ShopItemId",
                table: "purchase_counters",
                column: "ShopItemId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_records_CharacterId",
                table: "purchase_records",
                column: "CharacterId");

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
                name: "IX_shop_items_ShopId",
                table: "shop_items",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_shop_items_SortOrder",
                table: "shop_items",
                column: "SortOrder");
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
