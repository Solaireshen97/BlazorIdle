using BlazorIdle.Server.Domain.Combat.Damage;

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

    // 动作
    public ProcActionType Action { get; }
    public string? ActionBuffId { get; }    // ApplyBuff 时使用
    public int ActionDamageValue { get; }   // DealDamage 时使用（基础伤害）
    public DamageType ActionDamageType { get; } // DealDamage 伤害类型

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
        DamageType actionDamageType = DamageType.Physical
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
    }
}