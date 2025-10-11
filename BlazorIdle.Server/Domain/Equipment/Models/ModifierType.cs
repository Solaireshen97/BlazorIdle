namespace BlazorIdle.Server.Domain.Equipment.Models;

/// <summary>
/// 修饰符类型
/// </summary>
public enum ModifierType
{
    /// <summary>固定值加成（如 +50 攻击力）</summary>
    Flat,
    
    /// <summary>百分比加成（如 +5% 暴击）</summary>
    Percent,
    
    /// <summary>触发效果（如 攻击时有5%几率...）</summary>
    Proc
}
