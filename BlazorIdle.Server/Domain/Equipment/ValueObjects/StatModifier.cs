using BlazorIdle.Server.Domain.Equipment.Models;

namespace BlazorIdle.Server.Domain.Equipment.ValueObjects;

/// <summary>
/// 属性修饰符值对象
/// </summary>
public class StatModifier
{
    /// <summary>
    /// 属性类型
    /// </summary>
    public StatType StatType { get; set; }
    
    /// <summary>
    /// 修饰符类型
    /// </summary>
    public ModifierType ModifierType { get; set; }
    
    /// <summary>
    /// 修饰值
    /// </summary>
    public double Value { get; set; }
    
    public StatModifier()
    {
    }
    
    public StatModifier(StatType statType, ModifierType modifierType, double value)
    {
        StatType = statType;
        ModifierType = modifierType;
        Value = value;
    }
}
