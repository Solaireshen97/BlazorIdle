using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Skills;

namespace BlazorIdle.Server.Domain.Combat.Procs;

public class ProcDefinition
{
    public string Id { get; }
    public string Name { get; }

    // 触发
    public ProcTriggerType Trigger { get; }
    public double Chance { get; }           // 用于 OnHit/OnCrit：0..1 概率
    public double Rppm { get; }             // 用于 RPPM：每分钟期望次数
    public double IcdSeconds { get; }       // 内置冷却（秒）

    // 条件
    public ProcSourceFilter SourceFilter { get; }
    public bool AllowFromDot { get; }       // DoT 是否参与 OnHit/OnCrit（默认 false）
    public DamageType? DamageTypeFilter { get; } // 仅当伤害类型匹配时触发（可空）

    // 动作（核心）
    public ProcActionType Action { get; }
    public string? ActionBuffId { get; }    // ApplyBuff 时使用
    public int ActionDamageValue { get; }   // DealDamage 时使用（基础伤害）
    public DamageType ActionDamageType { get; } // DealDamage 伤害类型

    // 新增：AoE 配置（当 Action=DealDamage 且 MaxTargets>1 时生效）
    public int MaxTargets { get; } = 1;                    // 1 = 单体
    public AoEMode AoEMode { get; } = AoEMode.None;        // CleaveFull 或 SplitEven
    public bool IncludePrimaryTarget { get; } = true;      // 是否包含主目标
    public bool SplitRemainderToPrimary { get; } = true;   // SplitEven 余数是否给主目标

    public ProcDefinition(
        string id,
        string name,
        ProcTriggerType trigger,
        double chance = 0,
        double rppm = 0,
        double icdSeconds = 0,
        ProcSourceFilter sourceFilter = ProcSourceFilter.Any,
        bool allowFromDot = false,
        DamageType? damageTypeFilter = null,
        ProcActionType action = ProcActionType.ApplyBuff,
        string? actionBuffId = null,
        int actionDamageValue = 0,
        DamageType actionDamageType = DamageType.Physical,
        // AoE 参数（保持向后兼容，默认单体）
        int maxTargets = 1,
        AoEMode aoeMode = AoEMode.None,
        bool includePrimaryTarget = true,
        bool splitRemainderToPrimary = true
    )
    {
        Id = id;
        Name = name;
        Trigger = trigger;
        Chance = chance;
        Rppm = rppm;
        IcdSeconds = icdSeconds;
        SourceFilter = sourceFilter;
        AllowFromDot = allowFromDot;
        DamageTypeFilter = damageTypeFilter;
        Action = action;
        ActionBuffId = actionBuffId;
        ActionDamageValue = actionDamageValue;
        ActionDamageType = actionDamageType;

        MaxTargets = maxTargets <= 0 ? 1 : maxTargets;
        AoEMode = aoeMode;
        IncludePrimaryTarget = includePrimaryTarget;
        SplitRemainderToPrimary = splitRemainderToPrimary;
    }
}