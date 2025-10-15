namespace BlazorIdle.Server.Infrastructure.Configuration;

/// <summary>
/// 战斗引擎配置选项
/// </summary>
/// <remarks>
/// 包含战斗系统的核心时间参数、基础数值和伤害计算参数。
/// 所有参数都提供了合理的默认值，确保向后兼容。
/// </remarks>
public class CombatEngineOptions
{
    /// <summary>
    /// 远未来时间戳，用于标记未激活的事件
    /// </summary>
    /// <remarks>
    /// 用于将事件设置为"不会发生"的状态，例如：
    /// - 死亡角色的下次攻击时间
    /// - 被禁用的技能的下次触发时间
    /// 默认值：1e10（约317年后，实际上是一个永远不会到达的时间）
    /// </remarks>
    public double FarFutureTimestamp { get; set; } = 1e10;
    
    /// <summary>
    /// 技能检查间隔（秒）
    /// </summary>
    /// <remarks>
    /// 战斗引擎检查技能是否就绪的时间间隔。
    /// 较小的值会更精确但增加计算量，较大的值会降低技能释放的精确度。
    /// 默认值：0.5秒
    /// 建议范围：0.1 - 1.0秒
    /// </remarks>
    public double SkillCheckIntervalSeconds { get; set; } = 0.5;
    
    /// <summary>
    /// Buff刷新间隔（秒）
    /// </summary>
    /// <remarks>
    /// 持续性Buff（如生命恢复、持续伤害等）的触发间隔。
    /// 影响DoT/HoT效果的触发频率。
    /// 默认值：1.0秒
    /// 建议范围：0.5 - 2.0秒
    /// </remarks>
    public double BuffTickIntervalSeconds { get; set; } = 1.0;
    
    /// <summary>
    /// 基础攻击伤害
    /// </summary>
    /// <remarks>
    /// 角色在没有装备和属性加成时的基础伤害值。
    /// 主要用于测试场景和最低伤害保底。
    /// 默认值：10
    /// </remarks>
    public int BaseAttackDamage { get; set; } = 10;
    
    /// <summary>
    /// 默认攻击者等级
    /// </summary>
    /// <remarks>
    /// 当攻击者等级未设置或无效时使用的默认值。
    /// 用于某些特殊战斗场景（如测试、特殊事件、召唤物等）。
    /// 默认值：50
    /// </remarks>
    public int DefaultAttackerLevel { get; set; } = 50;
    
    /// <summary>
    /// 伤害减免参数
    /// </summary>
    public DamageReductionOptions DamageReduction { get; set; } = new();
}

/// <summary>
/// 伤害减免计算参数
/// </summary>
/// <remarks>
/// 用于计算防御力对伤害的减免效果。
/// 公式：减免率 = Defense / (Defense + K * Level + C)
/// 其中：
/// - Defense: 目标防御力
/// - Level: 攻击者等级
/// - K: 等级缩放系数
/// - C: 基础常量
/// </remarks>
public class DamageReductionOptions
{
    /// <summary>
    /// 伤害减免系数K（等级缩放系数）
    /// </summary>
    /// <remarks>
    /// 控制防御力随等级的缩放效果。
    /// - 较大的K值会降低高等级时的防御效果（需要更多防御力才能达到同样减免）
    /// - 较小的K值会增强高等级时的防御效果
    /// 默认值：50.0
    /// 建议范围：30.0 - 100.0
    /// </remarks>
    public double CoefficientK { get; set; } = 50.0;
    
    /// <summary>
    /// 伤害减免常量C（基础常量）
    /// </summary>
    /// <remarks>
    /// 控制低防御时的基础减免效果。
    /// - 较大的C值会降低低防御时的减免效果（需要更多防御力）
    /// - 较小的C值会增强低防御时的减免效果
    /// 默认值：400.0
    /// 建议范围：200.0 - 800.0
    /// </remarks>
    public double ConstantC { get; set; } = 400.0;
}
