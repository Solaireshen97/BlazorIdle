using BlazorIdle.Server.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Domain.Combat;

/// <summary>
/// 战斗系统常量 - 提供配置化的战斗常量访问
/// </summary>
/// <remarks>
/// 此类提供对战斗引擎配置的静态访问，避免在每个使用常量的地方都注入配置。
/// 在应用启动时应调用 Initialize 方法初始化配置。
/// </remarks>
public static class CombatConstants
{
    /// <summary>配置选项（通过依赖注入设置）</summary>
    private static CombatEngineOptions _options = new();

    /// <summary>
    /// 初始化战斗常量配置
    /// </summary>
    /// <param name="options">战斗引擎配置选项</param>
    /// <remarks>
    /// 此方法应在应用启动时调用一次。
    /// 如果不调用此方法，将使用默认值。
    /// </remarks>
    public static void Initialize(IOptions<CombatEngineOptions> options)
    {
        _options = options?.Value ?? new CombatEngineOptions();
    }

    /// <summary>
    /// 远未来时间戳，用于标记未激活的事件
    /// </summary>
    /// <remarks>
    /// 用于将事件设置为"不会发生"的状态，例如死亡角色的下次攻击时间。
    /// 默认值：1e10（约317年后）
    /// </remarks>
    public static double FarFutureTimestamp => _options.FarFutureTimestamp;

    /// <summary>
    /// 技能检查间隔（秒）
    /// </summary>
    /// <remarks>
    /// 战斗引擎检查技能是否就绪的时间间隔。
    /// 默认值：0.5秒
    /// </remarks>
    public static double SkillCheckIntervalSeconds => _options.SkillCheckIntervalSeconds;

    /// <summary>
    /// Buff刷新间隔（秒）
    /// </summary>
    /// <remarks>
    /// 持续性Buff（如生命恢复、持续伤害等）的触发间隔。
    /// 默认值：1.0秒
    /// </remarks>
    public static double BuffTickIntervalSeconds => _options.BuffTickIntervalSeconds;

    /// <summary>
    /// 基础攻击伤害
    /// </summary>
    /// <remarks>
    /// 角色在没有装备和属性加成时的基础伤害值。
    /// 默认值：10
    /// </remarks>
    public static int BaseAttackDamage => _options.BaseAttackDamage;

    /// <summary>
    /// 默认攻击者等级
    /// </summary>
    /// <remarks>
    /// 当攻击者等级未设置或无效时使用的默认值。
    /// 默认值：50
    /// </remarks>
    public static int DefaultAttackerLevel => _options.DefaultAttackerLevel;
}
