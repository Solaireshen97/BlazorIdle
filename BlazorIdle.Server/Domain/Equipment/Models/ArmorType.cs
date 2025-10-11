namespace BlazorIdle.Server.Domain.Equipment.Models;

/// <summary>
/// 护甲类型
/// </summary>
public enum ArmorType
{
    /// <summary>无护甲（饰品、颈部、披风等）</summary>
    None,
    
    /// <summary>布甲 - 0.5倍护甲系数</summary>
    Cloth,
    
    /// <summary>皮甲 - 1.0倍护甲系数</summary>
    Leather,
    
    /// <summary>锁甲 - 1.5倍护甲系数</summary>
    Mail,
    
    /// <summary>板甲 - 2.0倍护甲系数</summary>
    Plate
}
