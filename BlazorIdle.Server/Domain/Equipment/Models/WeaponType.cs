namespace BlazorIdle.Server.Domain.Equipment.Models;

/// <summary>
/// 武器类型
/// </summary>
public enum WeaponType
{
    /// <summary>无（非武器装备）</summary>
    None,
    
    // === 单手武器 ===
    /// <summary>单手剑</summary>
    Sword,
    
    /// <summary>匕首</summary>
    Dagger,
    
    /// <summary>单手斧</summary>
    Axe,
    
    /// <summary>单手锤</summary>
    Mace,
    
    /// <summary>拳套</summary>
    Fist,
    
    /// <summary>魔杖</summary>
    Wand,
    
    // === 双手武器 ===
    /// <summary>双手剑</summary>
    TwoHandSword,
    
    /// <summary>双手斧</summary>
    TwoHandAxe,
    
    /// <summary>双手锤</summary>
    TwoHandMace,
    
    /// <summary>法杖</summary>
    Staff,
    
    /// <summary>长柄武器</summary>
    Polearm,
    
    /// <summary>弓</summary>
    Bow,
    
    /// <summary>弩</summary>
    Crossbow,
    
    /// <summary>枪械</summary>
    Gun,
    
    // === 防御装备 ===
    /// <summary>盾牌（副手专用）</summary>
    Shield
}
