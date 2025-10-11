namespace BlazorIdle.Server.Domain.Equipment.Models;

/// <summary>
/// 护甲类型枚举 - 定义4种护甲类型
/// </summary>
public enum ArmorType
{
    /// <summary>无护甲（珠宝等）</summary>
    None,
    
    /// <summary>布甲 - 最轻，护甲值最低</summary>
    Cloth,
    
    /// <summary>皮甲 - 轻型，护甲值低</summary>
    Leather,
    
    /// <summary>锁甲 - 中型，护甲值中等</summary>
    Mail,
    
    /// <summary>板甲 - 重型，护甲值最高</summary>
    Plate
}
