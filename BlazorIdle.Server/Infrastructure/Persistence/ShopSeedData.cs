using BlazorIdle.Server.Domain.Shop;
using BlazorIdle.Server.Domain.Shop.ValueObjects;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BlazorIdle.Server.Infrastructure.Persistence;

/// <summary>
/// 商店系统种子数据
/// 提供初始商店定义和商品定义
/// </summary>
public static class ShopSeedData
{
    /// <summary>
    /// 为数据库添加商店系统种子数据
    /// </summary>
    public static void SeedShopData(this ModelBuilder modelBuilder)
    {
        SeedShopDefinitions(modelBuilder);
        SeedShopItems(modelBuilder);
    }

    /// <summary>
    /// 商店定义种子数据
    /// </summary>
    private static void SeedShopDefinitions(ModelBuilder modelBuilder)
    {
        // 使用静态日期以避免每次构建模型时产生变化
        var now = new DateTime(2025, 10, 12, 0, 0, 0, DateTimeKind.Utc);
        
        var shops = new List<ShopDefinition>
        {
            new ShopDefinition
            {
                Id = "general_shop",
                Name = "杂货铺",
                Type = ShopType.General,
                Icon = "🏪",
                Description = "出售基础物品和消耗品",
                UnlockCondition = null,
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
                Description = "出售各类武器和防具",
                UnlockCondition = "level>=5",
                IsEnabled = true,
                SortOrder = 2,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ShopDefinition
            {
                Id = "alchemist_shop",
                Name = "炼金术士",
                Type = ShopType.Special,
                Icon = "🧪",
                Description = "出售药剂和炼金材料",
                UnlockCondition = "level>=10",
                IsEnabled = true,
                SortOrder = 3,
                CreatedAt = now,
                UpdatedAt = now
            }
        };

        modelBuilder.Entity<ShopDefinition>().HasData(shops);
    }

    /// <summary>
    /// 商品定义种子数据
    /// </summary>
    private static void SeedShopItems(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2025, 10, 12, 0, 0, 0, DateTimeKind.Utc);
        
        var items = new List<ShopItem>
        {
            // 杂货铺商品
            new ShopItem
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000001"),
                ShopId = "general_shop",
                ItemType = ShopItemType.Consumable,
                ItemDefinitionId = "potion_health_small",
                DisplayName = "小型生命药水",
                Icon = "🧪",
                Description = "恢复100点生命值",
                PriceJson = JsonSerializer.Serialize(new Price { CurrencyType = CurrencyType.Gold, Amount = 50 }),
                StockLimit = -1,
                CurrentStock = 0,
                PurchaseLimitJson = null,
                RequiredLevel = 1,
                UnlockCondition = null,
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
                ItemDefinitionId = "potion_health_medium",
                DisplayName = "中型生命药水",
                Icon = "🧪",
                Description = "恢复250点生命值",
                PriceJson = JsonSerializer.Serialize(new Price { CurrencyType = CurrencyType.Gold, Amount = 150 }),
                StockLimit = -1,
                CurrentStock = 0,
                PurchaseLimitJson = null,
                RequiredLevel = 5,
                UnlockCondition = null,
                IsEnabled = true,
                SortOrder = 2,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ShopItem
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000003"),
                ShopId = "general_shop",
                ItemType = ShopItemType.Consumable,
                ItemDefinitionId = "food_bread",
                DisplayName = "面包",
                Icon = "🍞",
                Description = "恢复饱食度",
                PriceJson = JsonSerializer.Serialize(new Price { CurrencyType = CurrencyType.Gold, Amount = 10 }),
                StockLimit = -1,
                CurrentStock = 0,
                PurchaseLimitJson = null,
                RequiredLevel = 1,
                UnlockCondition = null,
                IsEnabled = true,
                SortOrder = 3,
                CreatedAt = now,
                UpdatedAt = now
            },
            
            // 武器店商品
            new ShopItem
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000101"),
                ShopId = "weapon_shop",
                ItemType = ShopItemType.Equipment,
                ItemDefinitionId = "weapon_iron_sword",
                DisplayName = "铁剑",
                Icon = "⚔️",
                Description = "基础单手武器",
                PriceJson = JsonSerializer.Serialize(new Price { CurrencyType = CurrencyType.Gold, Amount = 500 }),
                StockLimit = -1,
                CurrentStock = 0,
                PurchaseLimitJson = null,
                RequiredLevel = 5,
                UnlockCondition = null,
                IsEnabled = true,
                SortOrder = 1,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ShopItem
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000102"),
                ShopId = "weapon_shop",
                ItemType = ShopItemType.Equipment,
                ItemDefinitionId = "armor_leather_chest",
                DisplayName = "皮甲",
                Icon = "🛡️",
                Description = "基础胸甲",
                PriceJson = JsonSerializer.Serialize(new Price { CurrencyType = CurrencyType.Gold, Amount = 400 }),
                StockLimit = -1,
                CurrentStock = 0,
                PurchaseLimitJson = null,
                RequiredLevel = 5,
                UnlockCondition = null,
                IsEnabled = true,
                SortOrder = 2,
                CreatedAt = now,
                UpdatedAt = now
            },
            
            // 炼金术士商品
            new ShopItem
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000201"),
                ShopId = "alchemist_shop",
                ItemType = ShopItemType.Consumable,
                ItemDefinitionId = "potion_health_large",
                DisplayName = "大型生命药水",
                Icon = "🧪",
                Description = "恢复500点生命值",
                PriceJson = JsonSerializer.Serialize(new Price { CurrencyType = CurrencyType.Gold, Amount = 300 }),
                StockLimit = 10,
                CurrentStock = 10,
                PurchaseLimitJson = JsonSerializer.Serialize(new PurchaseLimit 
                { 
                    LimitType = PurchaseLimitType.Daily, 
                    MaxPurchases = 5, 
                    ResetHour = 0 
                }),
                RequiredLevel = 10,
                UnlockCondition = null,
                IsEnabled = true,
                SortOrder = 1,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ShopItem
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000202"),
                ShopId = "alchemist_shop",
                ItemType = ShopItemType.Material,
                ItemDefinitionId = "material_herb_common",
                DisplayName = "普通草药",
                Icon = "🌿",
                Description = "炼金材料",
                PriceJson = JsonSerializer.Serialize(new Price { CurrencyType = CurrencyType.Gold, Amount = 20 }),
                StockLimit = -1,
                CurrentStock = 0,
                PurchaseLimitJson = null,
                RequiredLevel = 10,
                UnlockCondition = null,
                IsEnabled = true,
                SortOrder = 2,
                CreatedAt = now,
                UpdatedAt = now
            }
        };

        modelBuilder.Entity<ShopItem>().HasData(items);
    }
}
