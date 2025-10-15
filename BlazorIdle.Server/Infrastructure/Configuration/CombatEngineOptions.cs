namespace BlazorIdle.Server.Infrastructure.Configuration;

/// <summary>
/// 战斗引擎配置选项
/// 用于配置战斗引擎的核心参数，如时间戳、间隔、基础伤害等
/// </summary>
public class CombatEngineOptions
{
    /// <summary>
    /// 远未来时间戳，用于标记未激活的事件
    /// 默认值: 1e10
    /// </summary>
    public double FarFutureTimestamp { get; set; } = 1e10;
    
    /// <summary>
    /// 技能检查间隔（秒）
    /// 控制敌人技能触发检查的频率，影响响应速度和性能
    /// 默认值: 0.5 秒
    /// </summary>
    public double SkillCheckIntervalSeconds { get; set; } = 0.5;
    
    /// <summary>
    /// Buff刷新间隔（秒）
    /// 控制DoT/HoT等周期效果的刷新频率
    /// 默认值: 1.0 秒
    /// </summary>
    public double BuffTickIntervalSeconds { get; set; } = 1.0;
    
    /// <summary>
    /// 基础攻击伤害
    /// 玩家攻击的基础伤害值，实际伤害会加上攻击力
    /// 默认值: 10
    /// </summary>
    public int BaseAttackDamage { get; set; } = 10;
    
    /// <summary>
    /// 默认攻击者等级
    /// 用于护甲减伤计算时的默认敌人等级
    /// 默认值: 50
    /// </summary>
    public int DefaultAttackerLevel { get; set; } = 50;
    
    /// <summary>
    /// 伤害减免参数
    /// </summary>
    public DamageReductionOptions DamageReduction { get; set; } = new();
}

/// <summary>
/// 伤害减免配置选项
/// 用于配置护甲减伤公式的参数
/// </summary>
public class DamageReductionOptions
{
    /// <summary>
    /// 伤害减免系数K
    /// 用于护甲减伤公式：reduction = armor / (armor + K * level + C)
    /// 默认值: 50.0
    /// </summary>
    public double CoefficientK { get; set; } = 50.0;
    
    /// <summary>
    /// 伤害减免常量C
    /// 用于护甲减伤公式：reduction = armor / (armor + K * level + C)
    /// 默认值: 400.0
    /// </summary>
    public double ConstantC { get; set; } = 400.0;
}
