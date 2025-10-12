using BlazorIdle.Server.Domain.Shop;
using BlazorIdle.Server.Domain.Shop.ValueObjects;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BlazorIdle.Server.Infrastructure.Persistence;

/// <summary>
/// 商店系统种子数据
/// 提供初始商店定义和商品数据
/// </summary>
public static class ShopSeedData
{
    /// <summary>
    /// 为数据库添加商店系统种子数据
    /// </summary>
    public static void SeedShopData(this ModelBuilder modelBuilder)
    {
        // 使用静态日期以避免每次构建模型时产生变化
        var now = new DateTime(2025, 10, 12, 0, 0, 0, DateTimeKind.Utc);

        // 商店定义
        var shops = new List<ShopDefinition>
        {
            new ShopDefinition
            {
                Id = "general_shop",
                Name = "杂货铺",
                Type = ShopType.General,
                Icon = "🏪",
                Description = "出售各类日常用品和基础物资",
                IsEnabled = true,
                SortOrder = 1,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ShopDefinition
            {
                Id = "weapon_shop",
                Name = "武器店",
                Type = ShopType.General,
                Icon = "⚔️",
                Description = "专业武器装备商店",
                IsEnabled = true,
                SortOrder = 2,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ShopDefinition
            {
                Id = "alchemist_shop",
                Name = "炼金术士",
                Type = ShopType.General,
                Icon = "🧪",
                Description = "出售各类药剂和炼金材料",
                IsEnabled = true,
                SortOrder = 3,
                CreatedAt = now,
                UpdatedAt = now
            }
        };

        modelBuilder.Entity<ShopDefinition>().HasData(shops);

        // 商品定义
        var items = new List<ShopItem>
        {
            // 杂货铺商品
            new ShopItem
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000001"),
                ShopId = "general_shop",
                ItemType = ShopItemType.Consumable,
                ItemDefinitionId = "health_potion_small",
                DisplayName = "小型生命药水",
                Icon = "🧪",
                Description = "恢复100点生命值",
                PriceJson = JsonSerializer.Serialize(new Price { CurrencyType = CurrencyType.Gold, Amount = 50 }),
                StockLimit = -1,
                CurrentStock = -1,
                RequiredLevel = 1,
                IsEnabled = true,
                SortOrder = 1,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ShopItem
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000002"),
                ShopId = "general_shop",
                ItemType = ShopItemType.Consumable,
                ItemDefinitionId = "mana_potion_small",
                DisplayName = "小型魔法药水",
                Icon = "💙",
                Description = "恢复100点魔法值",
                PriceJson = JsonSerializer.Serialize(new Price { CurrencyType = CurrencyType.Gold, Amount = 50 }),
                StockLimit = -1,
                CurrentStock = -1,
                RequiredLevel = 1,
                IsEnabled = true,
                SortOrder = 2,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ShopItem
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000003"),
                ShopId = "general_shop",
                ItemType = ShopItemType.Material,
                ItemDefinitionId = "cloth",
                DisplayName = "布料",
                Icon = "🧵",
                Description = "基础制作材料",
                PriceJson = JsonSerializer.Serialize(new Price { CurrencyType = CurrencyType.Gold, Amount = 10 }),
                StockLimit = -1,
                CurrentStock = -1,
                RequiredLevel = 1,
                IsEnabled = true,
                SortOrder = 3,
                CreatedAt = now,
                UpdatedAt = now
            },

            // 武器店商品
            new ShopItem
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000001"),
                ShopId = "weapon_shop",
                ItemType = ShopItemType.Equipment,
                ItemDefinitionId = "sword_iron",
                DisplayName = "铁剑",
                Icon = "⚔️",
                Description = "基础单手剑",
                PriceJson = JsonSerializer.Serialize(new Price { CurrencyType = CurrencyType.Gold, Amount = 500 }),
                StockLimit = -1,
                CurrentStock = -1,
                RequiredLevel = 5,
                IsEnabled = true,
                SortOrder = 1,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ShopItem
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000002"),
                ShopId = "weapon_shop",
                ItemType = ShopItemType.Equipment,
                ItemDefinitionId = "shield_wood",
                DisplayName = "木盾",
                Icon = "🛡️",
                Description = "基础盾牌",
                PriceJson = JsonSerializer.Serialize(new Price { CurrencyType = CurrencyType.Gold, Amount = 400 }),
                StockLimit = -1,
                CurrentStock = -1,
                RequiredLevel = 5,
                IsEnabled = true,
                SortOrder = 2,
                CreatedAt = now,
                UpdatedAt = now
            },

            // 炼金术士商品
            new ShopItem
            {
                Id = Guid.Parse("30000000-0000-0000-0000-000000000001"),
                ShopId = "alchemist_shop",
                ItemType = ShopItemType.Consumable,
                ItemDefinitionId = "health_potion_medium",
                DisplayName = "中型生命药水",
                Icon = "🧪",
                Description = "恢复300点生命值",
                PriceJson = JsonSerializer.Serialize(new Price { CurrencyType = CurrencyType.Gold, Amount = 150 }),
                StockLimit = -1,
                CurrentStock = -1,
                RequiredLevel = 10,
                IsEnabled = true,
                SortOrder = 1,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ShopItem
            {
                Id = Guid.Parse("30000000-0000-0000-0000-000000000002"),
                ShopId = "alchemist_shop",
                ItemType = ShopItemType.Consumable,
                ItemDefinitionId = "elixir_strength",
                DisplayName = "力量药剂",
                Icon = "💪",
                Description = "提升力量属性10点，持续30分钟",
                PriceJson = JsonSerializer.Serialize(new Price { CurrencyType = CurrencyType.Gold, Amount = 200 }),
                PurchaseLimitJson = JsonSerializer.Serialize(new PurchaseLimit 
                { 
                    LimitType = LimitType.Daily, 
                    MaxPurchases = 5 
                }),
                StockLimit = -1,
                CurrentStock = -1,
                RequiredLevel = 15,
                IsEnabled = true,
                SortOrder = 2,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ShopItem
            {
                Id = Guid.Parse("30000000-0000-0000-0000-000000000003"),
                ShopId = "alchemist_shop",
                ItemType = ShopItemType.Material,
                ItemDefinitionId = "herb_rare",
                DisplayName = "稀有草药",
                Icon = "🌿",
                Description = "高级炼金材料",
                PriceJson = JsonSerializer.Serialize(new Price { CurrencyType = CurrencyType.Gold, Amount = 100 }),
                PurchaseLimitJson = JsonSerializer.Serialize(new PurchaseLimit 
                { 
                    LimitType = LimitType.Weekly, 
                    MaxPurchases = 10 
                }),
                StockLimit = 50,
                CurrentStock = 50,
                RequiredLevel = 20,
                IsEnabled = true,
                SortOrder = 3,
                CreatedAt = now,
                UpdatedAt = now
            }
        };

        modelBuilder.Entity<ShopItem>().HasData(items);
    }
}
