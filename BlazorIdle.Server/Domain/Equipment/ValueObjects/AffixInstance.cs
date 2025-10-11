using BlazorIdle.Server.Domain.Equipment.Models;

namespace BlazorIdle.Server.Domain.Equipment.ValueObjects;

/// <summary>
/// 词条实例值对象（已Roll的词条）
/// </summary>
public class AffixInstance
{
    /// <summary>
    /// 词条定义ID
    /// </summary>
    public string AffixId { get; set; } = "";
    
    /// <summary>
    /// 属性类型
    /// </summary>
    public StatType StatType { get; set; }
    
    /// <summary>
    /// 修饰符类型
    /// </summary>
    public ModifierType ModifierType { get; set; }
    
    /// <summary>
    /// Roll后的数值
    /// </summary>
    public double RolledValue { get; set; }
    
    /// <summary>
    /// 显示文本（如"+50 攻击强度"）
    /// </summary>
    public string DisplayText { get; set; } = "";
    
    public AffixInstance()
    {
    }
    
    public AffixInstance(string affixId, StatType statType, ModifierType modifierType, double rolledValue)
    {
        AffixId = affixId;
        StatType = statType;
        ModifierType = modifierType;
        RolledValue = rolledValue;
        DisplayText = FormatDisplayText();
    }
    
    private string FormatDisplayText()
    {
        var statName = GetStatName(StatType);
        return ModifierType switch
        {
            ModifierType.Flat => $"+{RolledValue:F0} {statName}",
            ModifierType.Percent => $"+{RolledValue:F1}% {statName}",
            ModifierType.Proc => $"触发: {statName}",
            _ => $"+{RolledValue} {statName}"
        };
    }
    
    private string GetStatName(StatType stat)
    {
        return stat switch
        {
            StatType.Strength => "力量",
            StatType.Agility => "敏捷",
            StatType.Intellect => "智力",
            StatType.Stamina => "耐力",
            StatType.AttackPower => "攻击强度",
            StatType.SpellPower => "法术强度",
            StatType.Armor => "护甲",
            StatType.Haste => "急速",
            StatType.CritRating => "暴击",
            StatType.HitRating => "命中",
            StatType.MasteryRating => "精通",
            StatType.BlockRating => "格挡",
            StatType.DodgeRating => "闪避",
            StatType.ParryRating => "招架",
            StatType.HastePercent => "急速",
            StatType.CritChance => "暴击几率",
            StatType.BlockChance => "格挡几率",
            StatType.Health => "生命值",
            StatType.Mana => "法力值",
            _ => stat.ToString()
        };
    }
}
