namespace BlazorIdle.Server.Domain.Combat.Buffs;

public class BuffDefinition
{
    public string Id { get; }
    public string Name { get; }
    public double DurationSeconds { get; }
    public int MaxStacks { get; }
    public BuffStackPolicy StackPolicy { get; }

    // 属性修改（简单示例）
    public double AdditiveHaste { get; }          // 直接加
    public double MultiplicativeHaste { get; }    // 乘性 (最终 HasteFactor *= (1 + value))

    // 周期（可选）
    public BuffPeriodicType PeriodicType { get; }
    public double? PeriodicInterval { get; }
    public int PeriodicValue { get; }             // 伤害或资源值
    public string? PeriodicResourceId { get; }

    public BuffDefinition(
        string id,
        string name,
        double durationSeconds,
        int maxStacks,
        BuffStackPolicy stackPolicy,
        double additiveHaste = 0,
        double multiplicativeHaste = 0,
        BuffPeriodicType periodicType = BuffPeriodicType.None,
        double? periodicInterval = null,
        int periodicValue = 0,
        string? periodicResourceId = null)
    {
        Id = id;
        Name = name;
        DurationSeconds = durationSeconds;
        MaxStacks = maxStacks;
        StackPolicy = stackPolicy;
        AdditiveHaste = additiveHaste;
        MultiplicativeHaste = multiplicativeHaste;
        PeriodicType = periodicType;
        PeriodicInterval = periodicInterval;
        PeriodicValue = periodicValue;
        PeriodicResourceId = periodicResourceId;
    }
}