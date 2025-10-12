using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using BlazorIdle.Server.Domain.Shop.ValueObjects;

namespace BlazorIdle.Server.Domain.Shop;

/// <summary>
/// 商品定义实体
/// </summary>
public class ShopItem
{
    /// <summary>
    /// 商品唯一标识
    /// </summary>
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 所属商店 ID
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ShopId { get; set; } = "";

    /// <summary>
    /// 商品类型
    /// </summary>
    [Required]
    public ShopItemType ItemType { get; set; }

    /// <summary>
    /// 物品定义 ID（关联到游戏内物品/装备定义）
    /// 示例: "potion_health_small", "weapon_iron_sword"
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ItemDefinitionId { get; set; } = "";

    /// <summary>
    /// 显示名称（可覆盖物品定义的名称）
    /// </summary>
    [MaxLength(200)]
    public string? DisplayName { get; set; }

    /// <summary>
    /// 图标（可覆盖物品定义的图标）
    /// </summary>
    [MaxLength(50)]
    public string? Icon { get; set; }

    /// <summary>
    /// 描述（可覆盖物品定义的描述）
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// 价格信息（JSON 格式）
    /// </summary>
    [Required]
    public string PriceJson { get; set; } = "";

    /// <summary>
    /// 库存限制（-1 表示无限）
    /// </summary>
    public int StockLimit { get; set; } = -1;

    /// <summary>
    /// 当前库存（仅当 StockLimit > 0 时有效）
    /// </summary>
    public int CurrentStock { get; set; } = 0;

    /// <summary>
    /// 购买限制信息（JSON 格式）
    /// </summary>
    public string? PurchaseLimitJson { get; set; }

    /// <summary>
    /// 等级要求
    /// </summary>
    public int RequiredLevel { get; set; } = 1;

    /// <summary>
    /// 解锁条件表达式（可选）
    /// </summary>
    [MaxLength(500)]
    public string? UnlockCondition { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 排序顺序
    /// </summary>
    public int SortOrder { get; set; } = 0;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ShopDefinition Shop { get; set; } = null!;

    // Helper methods
    public Price GetPrice()
    {
        return JsonSerializer.Deserialize<Price>(PriceJson)
            ?? throw new InvalidOperationException($"Invalid price JSON for item {Id}");
    }

    public void SetPrice(Price price)
    {
        PriceJson = JsonSerializer.Serialize(price);
    }

    public PurchaseLimit? GetPurchaseLimit()
    {
        if (string.IsNullOrEmpty(PurchaseLimitJson))
            return null;
        return JsonSerializer.Deserialize<PurchaseLimit>(PurchaseLimitJson);
    }

    public void SetPurchaseLimit(PurchaseLimit? limit)
    {
        PurchaseLimitJson = limit == null ? null : JsonSerializer.Serialize(limit);
    }
}

/// <summary>
/// 商品类型枚举
/// </summary>
public enum ShopItemType
{
    /// <summary>
    /// 消耗品（食物、药剂等）
    /// </summary>
    Consumable = 0,

    /// <summary>
    /// 装备（武器、防具等）
    /// </summary>
    Equipment = 1,

    /// <summary>
    /// 材料（制作材料等）
    /// </summary>
    Material = 2,

    /// <summary>
    /// 货币（特殊货币）
    /// </summary>
    Currency = 3,

    /// <summary>
    /// 特殊物品（任务道具、宠物等）
    /// </summary>
    Special = 4
}
