using BlazorIdle.Server.Domain.Equipment.Models;

namespace BlazorIdle.Server.Domain.Equipment.ValueObjects;

/// <summary>
/// 武器信息值对象
/// 用于在战斗系统中传递武器相关信息
/// </summary>
public class WeaponInfo
{
    /// <summary>
    /// 武器类型
    /// </summary>
    public WeaponType WeaponType { get; init; }
    
    /// <summary>
    /// 基础攻击速度（秒）
    /// </summary>
    public double BaseAttackSpeed { get; init; }
    
    /// <summary>
    /// 武器DPS系数
    /// </summary>
    public double DPSCoefficient { get; init; }
    
    /// <summary>
    /// 是否为双手武器
    /// </summary>
    public bool IsTwoHanded { get; init; }
    
    /// <summary>
    /// 是否正在双持
    /// </summary>
    public bool IsDualWielding { get; init; }
    
    /// <summary>
    /// 副手武器类型（双持时）
    /// </summary>
    public WeaponType? OffHandWeaponType { get; init; }
    
    /// <summary>
    /// 副手基础攻击速度（双持时）
    /// </summary>
    public double? OffHandBaseAttackSpeed { get; init; }
    
    /// <summary>
    /// 副手武器DPS系数（双持时）
    /// </summary>
    public double? OffHandDPSCoefficient { get; init; }
    
    /// <summary>
    /// 创建默认武器信息（空手）
    /// </summary>
    public static WeaponInfo Default => new()
    {
        WeaponType = WeaponType.None,
        BaseAttackSpeed = 2.5,
        DPSCoefficient = 0.42,
        IsTwoHanded = false,
        IsDualWielding = false
    };
}
