using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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
    /// 所属商店ID
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
    /// 物品定义ID（指向游戏中的具体物品）
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ItemDefinitionId { get; set; } = "";

    /// <summary>
    /// 显示名称（可选，用于覆盖物品原始名称）
    /// </summary>
    [MaxLength(200)]
    public string? DisplayName { get; set; }

    /// <summary>
    /// 图标（可选，用于覆盖物品原始图标）
    /// </summary>
    [MaxLength(50)]
    public string? Icon { get; set; }

    /// <summary>
    /// 描述（可选）
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// 价格JSON（序列化的 Price 对象）
    /// </summary>
    [Required]
    [MaxLength(1000)]
    public string PriceJson { get; set; } = "{}";

    /// <summary>
    /// 库存限制（-1 表示无限库存）
    /// </summary>
    public int StockLimit { get; set; } = -1;

    /// <summary>
    /// 当前库存
    /// </summary>
    public int CurrentStock { get; set; } = -1;

    /// <summary>
    /// 购买限制JSON（序列化的 PurchaseLimit 对象，可选）
    /// </summary>
    [MaxLength(500)]
    public string? PurchaseLimitJson { get; set; }

    /// <summary>
    /// 需求等级
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

    // Navigation properties
    [ForeignKey(nameof(ShopId))]
    public ShopDefinition Shop { get; set; } = null!;

    // Helper methods
    /// <summary>
    /// 获取价格对象
    /// </summary>
    [NotMapped]
    public Price Price
    {
        get => JsonSerializer.Deserialize<Price>(PriceJson) ?? new Price();
        set => PriceJson = JsonSerializer.Serialize(value);
    }

    /// <summary>
    /// 获取购买限制对象
    /// </summary>
    [NotMapped]
    public PurchaseLimit? PurchaseLimit
    {
        get => string.IsNullOrEmpty(PurchaseLimitJson) 
            ? null 
            : JsonSerializer.Deserialize<PurchaseLimit>(PurchaseLimitJson);
        set => PurchaseLimitJson = value == null 
            ? null 
            : JsonSerializer.Serialize(value);
    }

    /// <summary>
    /// 检查是否有库存
    /// </summary>
    public bool HasStock()
    {
        return StockLimit == -1 || CurrentStock > 0;
    }

    /// <summary>
    /// 减少库存
    /// </summary>
    public bool DecreaseStock(int quantity = 1)
    {
        if (StockLimit == -1)
            return true;

        if (CurrentStock >= quantity)
        {
            CurrentStock -= quantity;
            return true;
        }

        return false;
    }
}

/// <summary>
/// 商品类型枚举
/// </summary>
public enum ShopItemType
{
    /// <summary>
    /// 消耗品
    /// </summary>
    Consumable = 0,

    /// <summary>
    /// 装备
    /// </summary>
    Equipment = 1,

    /// <summary>
    /// 材料
    /// </summary>
    Material = 2,

    /// <summary>
    /// 特殊物品
    /// </summary>
    Special = 3
}
