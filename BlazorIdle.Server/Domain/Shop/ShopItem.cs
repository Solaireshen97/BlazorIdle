using System;
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
    [MaxLength(100)]
    public string Id { get; set; } = "";

    /// <summary>
    /// 所属商店 ID
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ShopId { get; set; } = "";

    /// <summary>
    /// 物品定义 ID（关联 ItemDefinition 或 GearDefinition）
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ItemDefinitionId { get; set; } = "";

    /// <summary>
    /// 物品名称（冗余，便于查询）
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string ItemName { get; set; } = "";

    /// <summary>
    /// 物品图标（冗余）
    /// </summary>
    [MaxLength(50)]
    public string ItemIcon { get; set; } = "";

    /// <summary>
    /// 价格（JSON 序列化）
    /// </summary>
    [Required]
    public string PriceJson { get; set; } = "";

    /// <summary>
    /// 购买限制（JSON 序列化）
    /// </summary>
    [Required]
    public string PurchaseLimitJson { get; set; } = "";

    /// <summary>
    /// 库存数量（-1 表示无限）
    /// </summary>
    public int StockQuantity { get; set; } = -1;

    /// <summary>
    /// 最低等级要求
    /// </summary>
    public int MinLevel { get; set; } = 1;

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
    public ShopDefinition? Shop { get; set; }

    // 辅助方法：反序列化价格
    public Price GetPrice()
    {
        return JsonSerializer.Deserialize<Price>(PriceJson) ?? new Price();
    }

    // 辅助方法：反序列化购买限制
    public PurchaseLimit GetPurchaseLimit()
    {
        return JsonSerializer.Deserialize<PurchaseLimit>(PurchaseLimitJson) ?? new PurchaseLimit();
    }

    // 辅助方法：序列化价格
    public void SetPrice(Price price)
    {
        PriceJson = JsonSerializer.Serialize(price);
    }

    // 辅助方法：序列化购买限制
    public void SetPurchaseLimit(PurchaseLimit limit)
    {
        PurchaseLimitJson = JsonSerializer.Serialize(limit);
    }
}
