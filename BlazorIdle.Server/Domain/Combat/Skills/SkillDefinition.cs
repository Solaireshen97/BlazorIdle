namespace BlazorIdle.Server.Domain.Combat.Skills;

public class SkillDefinition
{
    public string Id { get; }
    public string Name { get; }
    public string? CostResourceId { get; }
    public int CostAmount { get; }
    public double CooldownSeconds { get; }
    public int Priority { get; }
    public int BaseDamage { get; }

    public double? CritChance { get; }
    public double? CritMultiplier { get; }

    // 施法/GCD（已存在）
    public double CastTimeSeconds { get; } = 0.0;
    public double GcdSeconds { get; } = 1.0;
    public bool OffGcd { get; } = false;
    public bool LockAttackDuringCast { get; } = true;
    public bool SpendCostOnCast { get; } = true;

    // 打断（已存在）
    public bool Interruptible { get; } = true;
    public bool RefundCostOnInterrupt { get; } = true;
    public double RefundRatioOnInterrupt { get; } = 1.0;

    // 充能/恢复（已存在）
    public int MaxCharges { get; } = 1;
    public double RechargeSeconds { get; } = 0;
    public bool ConsumeChargeOnCast { get; } = true;
    public bool RefundChargeOnInterrupt { get; } = true;
    public bool RechargeAffectedByHaste { get; } = false;

    // 新：AoE 配置
    public int MaxTargets { get; } = 1;            // 1 表示单体
    public AoEMode AoEMode { get; } = AoEMode.None;
    public bool IncludePrimaryTarget { get; } = true; // 选择目标时是否包含主目标
    public bool SplitRemainderToPrimary { get; } = true; // SplitEven 时的余数分配策略

    public SkillDefinition(
        string id,
        string name,
        string? costResourceId,
        int costAmount,
        double cooldownSeconds,
        int priority,
        int baseDamage,
        double? critChance = null,
        double? critMultiplier = null,
        double castTimeSeconds = 0.0,
        double gcdSeconds = 1.0,
        bool offGcd = false,
        bool lockAttackDuringCast = true,
        bool spendCostOnCast = true,
        bool interruptible = true,
        bool refundCostOnInterrupt = true,
        double refundRatioOnInterrupt = 1.0,
        int maxCharges = 1,
        double rechargeSeconds = 0,
        bool consumeChargeOnCast = true,
        bool refundChargeOnInterrupt = true,
        bool rechargeAffectedByHaste = false,
        // AoE
        int maxTargets = 1,
        AoEMode aoeMode = AoEMode.None,
        bool includePrimaryTarget = true,
        bool splitRemainderToPrimary = true
    )
    {
        Id = id;
        Name = name;
        CostResourceId = costResourceId;
        CostAmount = costAmount;
        CooldownSeconds = cooldownSeconds;
        Priority = priority;
        BaseDamage = baseDamage;

        CritChance = critChance;
        CritMultiplier = critMultiplier;

        CastTimeSeconds = castTimeSeconds;
        GcdSeconds = gcdSeconds;
        OffGcd = offGcd;
        LockAttackDuringCast = lockAttackDuringCast;
        SpendCostOnCast = spendCostOnCast;

        Interruptible = interruptible;
        RefundCostOnInterrupt = refundCostOnInterrupt;
        RefundRatioOnInterrupt = refundRatioOnInterrupt;

        MaxCharges = maxCharges <= 0 ? 1 : maxCharges;
        RechargeSeconds = rechargeSeconds;
        ConsumeChargeOnCast = consumeChargeOnCast;
        RefundChargeOnInterrupt = refundChargeOnInterrupt;
        RechargeAffectedByHaste = rechargeAffectedByHaste;

        MaxTargets = maxTargets <= 0 ? 1 : maxTargets;
        AoEMode = aoeMode;
        IncludePrimaryTarget = includePrimaryTarget;
        SplitRemainderToPrimary = splitRemainderToPrimary;
    }
}