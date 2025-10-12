using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BlazorIdle.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddShopSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PurchaseCounters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ShopItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PurchaseCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseCounters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ShopId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ShopItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemDefinitionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    GoldPaid = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemPaidId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ItemPaidQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    PurchasedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShopDefinitions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    UnlockCondition = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShopItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ShopId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ItemType = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemDefinitionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    PriceJson = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    StockLimit = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: -1),
                    CurrentStock = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: -1),
                    PurchaseLimitJson = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    RequiredLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    UnlockCondition = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShopItems_ShopDefinitions_ShopId",
                        column: x => x.ShopId,
                        principalTable: "ShopDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ShopDefinitions",
                columns: new[] { "Id", "CreatedAt", "Description", "Icon", "IsEnabled", "Name", "SortOrder", "Type", "UnlockCondition", "UpdatedAt" },
                values: new object[,]
                {
                    { "alchemist_shop", new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "出售各类药剂和炼金材料", "🧪", true, "炼金术士", 3, 0, null, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "general_shop", new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "出售各类日常用品和基础物资", "🏪", true, "杂货铺", 1, 0, null, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "weapon_shop", new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "专业武器装备商店", "⚔️", true, "武器店", 2, 0, null, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "ShopItems",
                columns: new[] { "Id", "CreatedAt", "CurrentStock", "Description", "DisplayName", "Icon", "IsEnabled", "ItemDefinitionId", "ItemType", "PriceJson", "PurchaseLimitJson", "RequiredLevel", "ShopId", "SortOrder", "StockLimit", "UnlockCondition", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), -1, "恢复100点生命值", "小型生命药水", "🧪", true, "health_potion_small", 0, "{\"currencyType\":0,\"amount\":50,\"itemId\":null,\"itemQuantity\":0}", null, 1, "general_shop", 1, -1, null, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("10000000-0000-0000-0000-000000000002"), new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), -1, "恢复100点魔法值", "小型魔法药水", "💙", true, "mana_potion_small", 0, "{\"currencyType\":0,\"amount\":50,\"itemId\":null,\"itemQuantity\":0}", null, 1, "general_shop", 2, -1, null, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("10000000-0000-0000-0000-000000000003"), new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), -1, "基础制作材料", "布料", "🧵", true, "cloth", 2, "{\"currencyType\":0,\"amount\":10,\"itemId\":null,\"itemQuantity\":0}", null, 1, "general_shop", 3, -1, null, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("20000000-0000-0000-0000-000000000001"), new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), -1, "基础单手剑", "铁剑", "⚔️", true, "sword_iron", 1, "{\"currencyType\":0,\"amount\":500,\"itemId\":null,\"itemQuantity\":0}", null, 5, "weapon_shop", 1, -1, null, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("20000000-0000-0000-0000-000000000002"), new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), -1, "基础盾牌", "木盾", "🛡️", true, "shield_wood", 1, "{\"currencyType\":0,\"amount\":400,\"itemId\":null,\"itemQuantity\":0}", null, 5, "weapon_shop", 2, -1, null, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("30000000-0000-0000-0000-000000000001"), new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), -1, "恢复300点生命值", "中型生命药水", "🧪", true, "health_potion_medium", 0, "{\"currencyType\":0,\"amount\":150,\"itemId\":null,\"itemQuantity\":0}", null, 10, "alchemist_shop", 1, -1, null, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("30000000-0000-0000-0000-000000000002"), new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), -1, "提升力量属性10点，持续30分钟", "力量药剂", "💪", true, "elixir_strength", 0, "{\"currencyType\":0,\"amount\":200,\"itemId\":null,\"itemQuantity\":0}", "{\"limitType\":2,\"maxPurchases\":5,\"resetPeriodSeconds\":null}", 15, "alchemist_shop", 2, -1, null, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("30000000-0000-0000-0000-000000000003"), new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), 50, "高级炼金材料", "稀有草药", "🌿", true, "herb_rare", 2, "{\"currencyType\":0,\"amount\":100,\"itemId\":null,\"itemQuantity\":0}", "{\"limitType\":3,\"maxPurchases\":10,\"resetPeriodSeconds\":null}", 20, "alchemist_shop", 3, 50, null, new DateTime(2025, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseCounters_CharacterId_ShopItemId_PeriodStart",
                table: "PurchaseCounters",
                columns: new[] { "CharacterId", "ShopItemId", "PeriodStart" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseCounters_PeriodEnd",
                table: "PurchaseCounters",
                column: "PeriodEnd");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRecords_CharacterId",
                table: "PurchaseRecords",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRecords_CharacterId_PurchasedAt",
                table: "PurchaseRecords",
                columns: new[] { "CharacterId", "PurchasedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRecords_PurchasedAt",
                table: "PurchaseRecords",
                column: "PurchasedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRecords_ShopItemId",
                table: "PurchaseRecords",
                column: "ShopItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopDefinitions_IsEnabled",
                table: "ShopDefinitions",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_ShopDefinitions_SortOrder",
                table: "ShopDefinitions",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_ShopDefinitions_Type",
                table: "ShopDefinitions",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_ShopItems_IsEnabled",
                table: "ShopItems",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_ShopItems_ItemType",
                table: "ShopItems",
                column: "ItemType");

            migrationBuilder.CreateIndex(
                name: "IX_ShopItems_ShopId",
                table: "ShopItems",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopItems_ShopId_SortOrder",
                table: "ShopItems",
                columns: new[] { "ShopId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ShopItems_SortOrder",
                table: "ShopItems",
                column: "SortOrder");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseCounters");

            migrationBuilder.DropTable(
                name: "PurchaseRecords");

            migrationBuilder.DropTable(
                name: "ShopItems");

            migrationBuilder.DropTable(
                name: "ShopDefinitions");
        }
    }
}
