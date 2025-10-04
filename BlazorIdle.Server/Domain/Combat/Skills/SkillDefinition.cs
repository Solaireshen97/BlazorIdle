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

    // 技能级暴击覆盖
    public double? CritChance { get; }
    public double? CritMultiplier { get; }

    // 施法/GCD
    public double CastTimeSeconds { get; } = 0.0;
    public double GcdSeconds { get; } = 1.0;
    public bool OffGcd { get; } = false;
    public bool LockAttackDuringCast { get; } = true;
    public bool SpendCostOnCast { get; } = true;

    // 新增：打断相关
    public bool Interruptible { get; } = true;          // 是否可被打断
    public bool RefundCostOnInterrupt { get; } = true;  // 若在开始时扣了资源，打断是否返还
    public double RefundRatioOnInterrupt { get; } = 1.0;// 返还比例（0..1）

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
        double refundRatioOnInterrupt = 1.0
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
    }
}