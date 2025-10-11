namespace BlazorIdle.Server.Domain.Equipment.Models;

/// <summary>
/// 修正类型枚举 - 定义属性修正的类型
/// </summary>
public enum ModifierType
{
    /// <summary>固定值加成</summary>
    Flat,
    
    /// <summary>百分比加成</summary>
    Percent,
    
    /// <summary>触发效果</summary>
    Proc
}
