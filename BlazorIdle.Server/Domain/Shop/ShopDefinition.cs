using System.ComponentModel.DataAnnotations;

namespace BlazorIdle.Server.Domain.Shop;

/// <summary>
/// 商店定义实体
/// </summary>
public class ShopDefinition
{
    /// <summary>
    /// 商店唯一标识
    /// </summary>
    [Key]
    [MaxLength(100)]
    public string Id { get; set; } = "";

    /// <summary>
    /// 商店名称
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = "";

    /// <summary>
    /// 商店类型
    /// </summary>
    [Required]
    public ShopType Type { get; set; } = ShopType.General;

    /// <summary>
    /// 商店图标
    /// </summary>
    [MaxLength(50)]
    public string Icon { get; set; } = "🏪";

    /// <summary>
    /// 商店描述
    /// </summary>
    [MaxLength(1000)]
    public string Description { get; set; } = "";

    /// <summary>
    /// 解锁条件表达式（可选，未来支持 DSL）
    /// 示例: "level>=10" 或 "AND(level>=20, gold>=1000)"
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
    public ICollection<ShopItem> Items { get; set; } = new List<ShopItem>();
}

/// <summary>
/// 商店类型枚举
/// </summary>
public enum ShopType
{
    /// <summary>
    /// 通用商店（所有玩家可见）
    /// </summary>
    General = 0,

    /// <summary>
    /// 特殊商店（需要解锁）
    /// </summary>
    Special = 1,

    /// <summary>
    /// 限时商店（限定时间开放）
    /// </summary>
    Limited = 2,

    /// <summary>
    /// 个人商店（角色专属）
    /// </summary>
    Personal = 3
}
