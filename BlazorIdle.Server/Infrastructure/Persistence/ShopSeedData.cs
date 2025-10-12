using Microsoft.EntityFrameworkCore;
using BlazorIdle.Server.Domain.Shop;
using BlazorIdle.Server.Domain.Shop.ValueObjects;
using System.Text.Json;

namespace BlazorIdle.Server.Infrastructure.Persistence;

/// <summary>
/// 商店系统种子数据
/// </summary>
public static class ShopSeedData
{
    public static void SeedShops(this ModelBuilder modelBuilder)
    {
        var now = new DateTime(2025, 10, 12, 0, 0, 0, DateTimeKind.Utc);

        // ===== 商店定义 =====
        modelBuilder.Entity<ShopDefinition>().HasData(
            // 杂货铺
            new ShopDefinition
            {
                Id = "general_shop",
                Name = "杂货铺",
                Type = ShopType.General,
                Icon = "🏪",
                Description = "出售各类日常消耗品和基础装备",
                UnlockCondition = null,
                IsEnabled = true,
                SortOrder = 1,
                CreatedAt = now,
                UpdatedAt = now
            },
            // 武器店
            new ShopDefinition
            {
                Id = "weapon_shop",
                Name = "武器店",
                Type = ShopType.General,
                Icon = "⚔️",
                Description = "专业的武器装备商店",
                UnlockCondition = null,
                IsEnabled = true,
                SortOrder = 2,
                CreatedAt = now,
                UpdatedAt = now
            },
            // 炼金术士
            new ShopDefinition
            {
                Id = "alchemist_shop",
                Name = "炼金术士",
                Type = ShopType.Special,
                Icon = "🧪",
                Description = "出售高级药剂和特殊物品",
                UnlockCondition = "level>=10",
                IsEnabled = true,
                SortOrder = 3,
                CreatedAt = now,
                UpdatedAt = now
            }
        );

        // ===== 商品定义 =====
        var items = new List<ShopItem>();

        // 杂货铺商品
        items.Add(CreateShopItem(
            id: "general_shop_health_potion",
            shopId: "general_shop",
            itemDefId: "health_potion_small",
            name: "小型生命药水",
            icon: "🧪",
            price: new Price { CurrencyType = CurrencyType.Gold, Amount = 50 },
            limit: new PurchaseLimit { Type = LimitType.Unlimited },
            minLevel: 1,
            sortOrder: 1,
            createdAt: now
        ));

        items.Add(CreateShopItem(
            id: "general_shop_mana_potion",
            shopId: "general_shop",
            itemDefId: "mana_potion_small",
            name: "小型魔法药水",
            icon: "💙",
            price: new Price { CurrencyType = CurrencyType.Gold, Amount = 50 },
            limit: new PurchaseLimit { Type = LimitType.Unlimited },
            minLevel: 1,
            sortOrder: 2,
            createdAt: now
        ));

        items.Add(CreateShopItem(
            id: "general_shop_bread",
            shopId: "general_shop",
            itemDefId: "bread",
            name: "面包",
            icon: "🍞",
            price: new Price { CurrencyType = CurrencyType.Gold, Amount = 10 },
            limit: new PurchaseLimit { Type = LimitType.Unlimited },
            minLevel: 1,
            sortOrder: 3,
            createdAt: now
        ));

        // 武器店商品
        items.Add(CreateShopItem(
            id: "weapon_shop_iron_sword",
            shopId: "weapon_shop",
            itemDefId: "iron_sword",
            name: "铁剑",
            icon: "⚔️",
            price: new Price { CurrencyType = CurrencyType.Gold, Amount = 500 },
            limit: new PurchaseLimit { Type = LimitType.Unlimited },
            minLevel: 1,
            sortOrder: 1,
            createdAt: now
        ));

        items.Add(CreateShopItem(
            id: "weapon_shop_steel_sword",
            shopId: "weapon_shop",
            itemDefId: "steel_sword",
            name: "钢剑",
            icon: "⚔️",
            price: new Price { CurrencyType = CurrencyType.Gold, Amount = 1500 },
            limit: new PurchaseLimit { Type = LimitType.Unlimited },
            minLevel: 5,
            sortOrder: 2,
            createdAt: now
        ));

        items.Add(CreateShopItem(
            id: "weapon_shop_wooden_shield",
            shopId: "weapon_shop",
            itemDefId: "wooden_shield",
            name: "木盾",
            icon: "🛡️",
            price: new Price { CurrencyType = CurrencyType.Gold, Amount = 300 },
            limit: new PurchaseLimit { Type = LimitType.Unlimited },
            minLevel: 1,
            sortOrder: 3,
            createdAt: now
        ));

        // 炼金术士商品
        items.Add(CreateShopItem(
            id: "alchemist_shop_greater_health",
            shopId: "alchemist_shop",
            itemDefId: "health_potion_greater",
            name: "高级生命药水",
            icon: "🧪",
            price: new Price { CurrencyType = CurrencyType.Gold, Amount = 200 },
            limit: new PurchaseLimit { Type = LimitType.Daily, MaxPurchases = 5 },
            minLevel: 10,
            sortOrder: 1,
            createdAt: now
        ));

        items.Add(CreateShopItem(
            id: "alchemist_shop_elixir",
            shopId: "alchemist_shop",
            itemDefId: "elixir_of_strength",
            name: "力量药剂",
            icon: "💪",
            price: new Price { CurrencyType = CurrencyType.Gold, Amount = 500 },
            limit: new PurchaseLimit { Type = LimitType.Weekly, MaxPurchases = 3 },
            minLevel: 15,
            sortOrder: 2,
            createdAt: now
        ));

        items.Add(CreateShopItem(
            id: "alchemist_shop_rare_ingredient",
            shopId: "alchemist_shop",
            itemDefId: "dragon_scale",
            name: "龙鳞",
            icon: "🐉",
            price: new Price { CurrencyType = CurrencyType.Gold, Amount = 5000 },
            limit: new PurchaseLimit { Type = LimitType.PerCharacter, MaxPurchases = 1 },
            minLevel: 20,
            sortOrder: 3,
            createdAt: now
        ));

        items.Add(CreateShopItem(
            id: "alchemist_shop_scroll",
            shopId: "alchemist_shop",
            itemDefId: "teleport_scroll",
            name: "传送卷轴",
            icon: "📜",
            price: new Price { CurrencyType = CurrencyType.Gold, Amount = 1000 },
            limit: new PurchaseLimit { Type = LimitType.Unlimited },
            minLevel: 10,
            sortOrder: 4,
            createdAt: now
        ));

        modelBuilder.Entity<ShopItem>().HasData(items);
    }

    private static ShopItem CreateShopItem(
        string id,
        string shopId,
        string itemDefId,
        string name,
        string icon,
        Price price,
        PurchaseLimit limit,
        int minLevel,
        int sortOrder,
        DateTime createdAt)
    {
        return new ShopItem
        {
            Id = id,
            ShopId = shopId,
            ItemDefinitionId = itemDefId,
            ItemName = name,
            ItemIcon = icon,
            PriceJson = JsonSerializer.Serialize(price),
            PurchaseLimitJson = JsonSerializer.Serialize(limit),
            StockQuantity = -1, // 无限库存
            MinLevel = minLevel,
            IsEnabled = true,
            SortOrder = sortOrder,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }
}
