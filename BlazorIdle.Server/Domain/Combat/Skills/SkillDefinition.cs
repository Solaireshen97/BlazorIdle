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

    // 技能级暴击覆盖（可选）
    public double? CritChance { get; }
    public double? CritMultiplier { get; }

    // 施法/GCD
    public double CastTimeSeconds { get; } = 0.0;   // 0 表示即时
    public double GcdSeconds { get; } = 1.0;        // OffGcd=false 时生效
    public bool OffGcd { get; } = false;
    public bool LockAttackDuringCast { get; } = true;

    // 资源扣除时机
    public bool SpendCostOnCast { get; } = true;

    // 打断配置
    public bool Interruptible { get; } = true;
    public bool RefundCostOnInterrupt { get; } = true;
    public double RefundRatioOnInterrupt { get; } = 1.0;

    // 充能/恢复（新增）
    public int MaxCharges { get; } = 1;                 // 1 = 无充能，走传统冷却
    public double RechargeSeconds { get; } = 0;         // >0 才有意义（MaxCharges>1 时使用）
    public bool ConsumeChargeOnCast { get; } = true;    // true=开始施法即消耗；false=完成时消耗
    public bool RefundChargeOnInterrupt { get; } = true;// 若已在开始消耗，打断时是否返还
    public bool RechargeAffectedByHaste { get; } = false;// 充能恢复是否受 Haste 影响（按开始时快照）

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
        // 充能相关
        int maxCharges = 1,
        double rechargeSeconds = 0,
        bool consumeChargeOnCast = true,
        bool refundChargeOnInterrupt = true,
        bool rechargeAffectedByHaste = false
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
    }
}