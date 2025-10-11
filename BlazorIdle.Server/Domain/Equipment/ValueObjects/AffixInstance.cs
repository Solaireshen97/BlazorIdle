using BlazorIdle.Server.Domain.Equipment.Models;

namespace BlazorIdle.Server.Domain.Equipment.ValueObjects;

/// <summary>
/// 词条实例值对象 - 表示装备上的一个具体词条
/// </summary>
public class AffixInstance
{
    /// <summary>词条ID（引用Affix定义）</summary>
    public string AffixId { get; set; } = "";
    
    /// <summary>属性类型</summary>
    public StatType StatType { get; set; }
    
    /// <summary>修正类型</summary>
    public ModifierType ModifierType { get; set; }
    
    /// <summary>实际Roll出的值</summary>
    public double RolledValue { get; set; }
    
    /// <summary>显示文本（如"+50 攻击强度"）</summary>
    public string DisplayText { get; set; } = "";
    
    public AffixInstance() { }
    
    public AffixInstance(string affixId, StatType statType, ModifierType modifierType, double rolledValue, string displayText)
    {
        AffixId = affixId;
        StatType = statType;
        ModifierType = modifierType;
        RolledValue = rolledValue;
        DisplayText = displayText;
    }
}
