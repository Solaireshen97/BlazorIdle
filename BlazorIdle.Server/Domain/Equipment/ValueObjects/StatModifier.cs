using BlazorIdle.Server.Domain.Equipment.Models;

namespace BlazorIdle.Server.Domain.Equipment.ValueObjects;

/// <summary>
/// 属性修正值对象 - 用于表示一个属性修正（如套装加成）
/// </summary>
public class StatModifier
{
    /// <summary>属性类型</summary>
    public StatType StatType { get; set; }
    
    /// <summary>修正类型（固定值/百分比/触发）</summary>
    public ModifierType ModifierType { get; set; }
    
    /// <summary>修正值</summary>
    public double Value { get; set; }
    
    public StatModifier() { }
    
    public StatModifier(StatType statType, ModifierType modifierType, double value)
    {
        StatType = statType;
        ModifierType = modifierType;
        Value = value;
    }
}
