namespace BlazorIdle.Server.Domain.Equipment.Models;

/// <summary>
/// 属性类型
/// </summary>
public enum StatType
{
    // === 基础属性 ===
    /// <summary>力量</summary>
    Strength,
    
    /// <summary>敏捷</summary>
    Agility,
    
    /// <summary>智力</summary>
    Intellect,
    
    /// <summary>耐力</summary>
    Stamina,
    
    // === 战斗属性 ===
    /// <summary>攻击强度</summary>
    AttackPower,
    
    /// <summary>法术强度</summary>
    SpellPower,
    
    /// <summary>护甲值</summary>
    Armor,
    
    /// <summary>急速等级</summary>
    Haste,
    
    /// <summary>暴击等级</summary>
    CritRating,
    
    /// <summary>命中等级</summary>
    HitRating,
    
    /// <summary>精通等级</summary>
    MasteryRating,
    
    /// <summary>格挡等级</summary>
    BlockRating,
    
    /// <summary>闪避等级</summary>
    DodgeRating,
    
    /// <summary>招架等级</summary>
    ParryRating,
    
    // === 百分比属性 ===
    /// <summary>急速百分比</summary>
    HastePercent,
    
    /// <summary>暴击百分比</summary>
    CritChance,
    
    /// <summary>格挡概率</summary>
    BlockChance,
    
    // === 资源属性 ===
    /// <summary>生命值</summary>
    Health,
    
    /// <summary>法力值</summary>
    Mana
}
