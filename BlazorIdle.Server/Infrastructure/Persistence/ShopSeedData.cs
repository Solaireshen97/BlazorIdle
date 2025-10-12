using Microsoft.EntityFrameworkCore;
using BlazorIdle.Server.Domain.Shop;
using BlazorIdle.Server.Domain.Shop.ValueObjects;
using BlazorIdle.Server.Domain.Shop.Configuration;
using System.Text.Json;

namespace BlazorIdle.Server.Infrastructure.Persistence;

/// <summary>
/// 商店系统种子数据
/// 注意：从配置文件加载数据，保持代码清洁
/// </summary>
public static class ShopSeedData
{
    public static void SeedShops(this ModelBuilder modelBuilder)
    {
        var now = new DateTime(2025, 10, 12, 0, 0, 0, DateTimeKind.Utc);
        
        // 从静态配置加载数据（用于EF迁移）
        var shopDefinitions = GetShopDefinitionsFromConfig();
        var shopItems = GetShopItemsFromConfig();

        // ===== 商店定义 =====
        var shops = shopDefinitions.Select(sd => new ShopDefinition
        {
            Id = sd.Id,
            Name = sd.Name,
            Type = Enum.Parse<ShopType>(sd.Type),
            Icon = sd.Icon,
            Description = sd.Description,
            UnlockCondition = sd.UnlockCondition,
            IsEnabled = sd.IsEnabled,
            SortOrder = sd.SortOrder,
            CreatedAt = now,
            UpdatedAt = now
        }).ToArray();
        
        modelBuilder.Entity<ShopDefinition>().HasData(shops);

        // ===== 商品定义 =====
        var items = shopItems.Select(si => CreateShopItem(
            id: si.Id,
            shopId: si.ShopId,
            itemDefId: si.ItemDefinitionId,
            name: si.ItemName,
            icon: si.ItemIcon,
            price: si.Price.ToPrice(),
            limit: si.PurchaseLimit.ToPurchaseLimit(),
            minLevel: si.MinLevel,
            sortOrder: si.SortOrder,
            stockQuantity: si.StockQuantity,
            createdAt: now
        )).ToList();

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
        int stockQuantity,
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
            StockQuantity = stockQuantity,
            MinLevel = minLevel,
            IsEnabled = true,
            SortOrder = sortOrder,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }
    
    /// <summary>
    /// 从配置文件获取商店定义（用于EF迁移时的静态数据）
    /// </summary>
    private static List<ShopDefinitionData> GetShopDefinitionsFromConfig()
    {
        // 配置文件路径相对于项目根目录
        var configPath = Path.Combine("Config", "Shop", "ShopDefinitions.json");
        
        if (!File.Exists(configPath))
        {
            // 如果配置文件不存在，返回默认配置
            return GetDefaultShopDefinitions();
        }
        
        try
        {
            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<ShopDefinitionsConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return config?.Shops ?? GetDefaultShopDefinitions();
        }
        catch
        {
            // 如果读取失败，返回默认配置
            return GetDefaultShopDefinitions();
        }
    }
    
    /// <summary>
    /// 从配置文件获取商品数据（用于EF迁移时的静态数据）
    /// </summary>
    private static List<ShopItemData> GetShopItemsFromConfig()
    {
        // 配置文件路径相对于项目根目录
        var configPath = Path.Combine("Config", "Shop", "ShopItems.json");
        
        if (!File.Exists(configPath))
        {
            // 如果配置文件不存在，返回默认配置
            return GetDefaultShopItems();
        }
        
        try
        {
            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<ShopItemsConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return config?.Items ?? GetDefaultShopItems();
        }
        catch
        {
            // 如果读取失败，返回默认配置
            return GetDefaultShopItems();
        }
    }
    
    /// <summary>
    /// 获取默认商店定义（后备方案）
    /// </summary>
    private static List<ShopDefinitionData> GetDefaultShopDefinitions()
    {
        return new List<ShopDefinitionData>
        {
            new() { Id = "general_shop", Name = "杂货铺", Type = "General", Icon = "🏪", 
                    Description = "出售各类日常消耗品和基础装备", UnlockCondition = null, IsEnabled = true, SortOrder = 1 },
            new() { Id = "weapon_shop", Name = "武器店", Type = "General", Icon = "⚔️", 
                    Description = "专业的武器装备商店", UnlockCondition = null, IsEnabled = true, SortOrder = 2 },
            new() { Id = "alchemist_shop", Name = "炼金术士", Type = "Special", Icon = "🧪", 
                    Description = "出售高级药剂和特殊物品", UnlockCondition = "level>=10", IsEnabled = true, SortOrder = 3 }
        };
    }
    
    /// <summary>
    /// 获取默认商品数据（后备方案）
    /// </summary>
    private static List<ShopItemData> GetDefaultShopItems()
    {
        return new List<ShopItemData>
        {
            new() { Id = "general_shop_health_potion", ShopId = "general_shop", ItemDefinitionId = "health_potion_small",
                    ItemName = "小型生命药水", ItemIcon = "🧪", Price = new PriceData { CurrencyType = "Gold", Amount = 50 },
                    PurchaseLimit = new PurchaseLimitData { Type = "Unlimited" }, StockQuantity = -1, MinLevel = 1, IsEnabled = true, SortOrder = 1 },
            new() { Id = "general_shop_mana_potion", ShopId = "general_shop", ItemDefinitionId = "mana_potion_small",
                    ItemName = "小型魔法药水", ItemIcon = "💙", Price = new PriceData { CurrencyType = "Gold", Amount = 50 },
                    PurchaseLimit = new PurchaseLimitData { Type = "Unlimited" }, StockQuantity = -1, MinLevel = 1, IsEnabled = true, SortOrder = 2 },
            new() { Id = "general_shop_bread", ShopId = "general_shop", ItemDefinitionId = "bread",
                    ItemName = "面包", ItemIcon = "🍞", Price = new PriceData { CurrencyType = "Gold", Amount = 10 },
                    PurchaseLimit = new PurchaseLimitData { Type = "Unlimited" }, StockQuantity = -1, MinLevel = 1, IsEnabled = true, SortOrder = 3 },
            new() { Id = "weapon_shop_iron_sword", ShopId = "weapon_shop", ItemDefinitionId = "iron_sword",
                    ItemName = "铁剑", ItemIcon = "⚔️", Price = new PriceData { CurrencyType = "Gold", Amount = 500 },
                    PurchaseLimit = new PurchaseLimitData { Type = "Unlimited" }, StockQuantity = -1, MinLevel = 1, IsEnabled = true, SortOrder = 1 },
            new() { Id = "weapon_shop_steel_sword", ShopId = "weapon_shop", ItemDefinitionId = "steel_sword",
                    ItemName = "钢剑", ItemIcon = "⚔️", Price = new PriceData { CurrencyType = "Gold", Amount = 1500 },
                    PurchaseLimit = new PurchaseLimitData { Type = "Unlimited" }, StockQuantity = -1, MinLevel = 5, IsEnabled = true, SortOrder = 2 },
            new() { Id = "weapon_shop_wooden_shield", ShopId = "weapon_shop", ItemDefinitionId = "wooden_shield",
                    ItemName = "木盾", ItemIcon = "🛡️", Price = new PriceData { CurrencyType = "Gold", Amount = 300 },
                    PurchaseLimit = new PurchaseLimitData { Type = "Unlimited" }, StockQuantity = -1, MinLevel = 1, IsEnabled = true, SortOrder = 3 },
            new() { Id = "alchemist_shop_greater_health", ShopId = "alchemist_shop", ItemDefinitionId = "health_potion_greater",
                    ItemName = "高级生命药水", ItemIcon = "🧪", Price = new PriceData { CurrencyType = "Gold", Amount = 200 },
                    PurchaseLimit = new PurchaseLimitData { Type = "Daily", MaxPurchases = 5 }, StockQuantity = -1, MinLevel = 10, IsEnabled = true, SortOrder = 1 },
            new() { Id = "alchemist_shop_elixir", ShopId = "alchemist_shop", ItemDefinitionId = "elixir_of_strength",
                    ItemName = "力量药剂", ItemIcon = "💪", Price = new PriceData { CurrencyType = "Gold", Amount = 500 },
                    PurchaseLimit = new PurchaseLimitData { Type = "Weekly", MaxPurchases = 3 }, StockQuantity = -1, MinLevel = 15, IsEnabled = true, SortOrder = 2 },
            new() { Id = "alchemist_shop_rare_ingredient", ShopId = "alchemist_shop", ItemDefinitionId = "dragon_scale",
                    ItemName = "龙鳞", ItemIcon = "🐉", Price = new PriceData { CurrencyType = "Gold", Amount = 5000 },
                    PurchaseLimit = new PurchaseLimitData { Type = "PerCharacter", MaxPurchases = 1 }, StockQuantity = -1, MinLevel = 20, IsEnabled = true, SortOrder = 3 },
            new() { Id = "alchemist_shop_scroll", ShopId = "alchemist_shop", ItemDefinitionId = "teleport_scroll",
                    ItemName = "传送卷轴", ItemIcon = "📜", Price = new PriceData { CurrencyType = "Gold", Amount = 1000 },
                    PurchaseLimit = new PurchaseLimitData { Type = "Unlimited" }, StockQuantity = -1, MinLevel = 10, IsEnabled = true, SortOrder = 4 }
        };
    }
}
