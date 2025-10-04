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

    // 新增：施法/GCD 配置
    public double CastTimeSeconds { get; } = 0.0;   // 0 表示即时
    public double GcdSeconds { get; } = 1.0;        // 进入 GCD 的时长（OffGcd=false 时生效）
    public bool OffGcd { get; } = false;            // 是否不受 GCD 约束
    public bool LockAttackDuringCast { get; } = true; // 施法期间是否暂停普攻（仅 CastTime>0 有意义）

    // 资源扣除时机（默认开始施法时扣）
    public bool SpendCostOnCast { get; } = true;

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
        bool spendCostOnCast = true)
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
    }
}