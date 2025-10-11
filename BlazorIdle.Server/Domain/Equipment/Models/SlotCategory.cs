namespace BlazorIdle.Server.Domain.Equipment.Models;

/// <summary>
/// 槽位分类枚举 - 用于分组装备槽位
/// </summary>
public enum SlotCategory
{
    /// <summary>护甲类（可以有护甲值的装备）</summary>
    Armor,
    
    /// <summary>珠宝类（戒指、项链、饰品）</summary>
    Jewelry,
    
    /// <summary>武器类（主手、副手、双手）</summary>
    Weapon
}
