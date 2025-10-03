namespace BlazorIdle.Server.Domain.Combat.Skills;

public class SkillDefinition
{
    public string Id { get; }
    public string Name { get; }
    public string? CostResourceId { get; }
    public int CostAmount { get; }
    public double CooldownSeconds { get; }
    public int Priority { get; }          // 数值越小优先级越高
    public int BaseDamage { get; }        // 简化：固定伤害
    public bool SpendCostOnCast { get; } = true;

    public SkillDefinition(
        string id,
        string name,
        string? costResourceId,
        int costAmount,
        double cooldownSeconds,
        int priority,
        int baseDamage)
    {
        Id = id;
        Name = name;
        CostResourceId = costResourceId;
        CostAmount = costAmount;
        CooldownSeconds = cooldownSeconds;
        Priority = priority;
        BaseDamage = baseDamage;
    }
}