namespace BlazorIdle.Server.Domain.Equipment.Models;

/// <summary>
/// 属性类型枚举 - 定义装备可以提供的属性类型
/// </summary>
public enum StatType
{
    // 基础属性
    /// <summary>力量</summary>
    Strength,
    
    /// <summary>敏捷</summary>
    Agility,
    
    /// <summary>智力</summary>
    Intellect,
    
    /// <summary>耐力</summary>
    Stamina,
    
    // 战斗属性
    /// <summary>攻击强度</summary>
    AttackPower,
    
    /// <summary>法术强度</summary>
    SpellPower,
    
    /// <summary>护甲值</summary>
    Armor,
    
    /// <summary>最大生命值</summary>
    MaxHealth,
    
    /// <summary>最大法力值</summary>
    MaxMana,
    
    /// <summary>暴击率（百分比）</summary>
    CritChance,
    
    /// <summary>暴击伤害（百分比）</summary>
    CritDamage,
    
    /// <summary>急速（百分比）</summary>
    Haste,
    
    /// <summary>精通（百分比）</summary>
    Mastery,
    
    /// <summary>全能（百分比）</summary>
    Versatility,
    
    /// <summary>格挡率（百分比）</summary>
    BlockChance,
    
    /// <summary>格挡值</summary>
    BlockValue,
    
    /// <summary>闪避率（百分比）</summary>
    DodgeChance,
    
    /// <summary>招架率（百分比）</summary>
    ParryChance,
    
    /// <summary>攻击速度</summary>
    AttackSpeed
}
