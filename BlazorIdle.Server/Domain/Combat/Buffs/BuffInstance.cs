namespace BlazorIdle.Server.Domain.Combat.Buffs;

public class BuffInstance
{
    public BuffDefinition Definition { get; }
    public int Stacks { get; private set; }
    public double ExpiresAt { get; private set; }
    public double? NextTickAt { get; set; }

    public BuffInstance(BuffDefinition def, int stacks, double now)
    {
        Definition = def;
        Stacks = stacks;
        ExpiresAt = now + def.DurationSeconds;
        if (def.PeriodicType != BuffPeriodicType.None && def.PeriodicInterval.HasValue)
            NextTickAt = now + def.PeriodicInterval.Value;
    }

    public void Refresh(double now)
    {
        ExpiresAt = now + Definition.DurationSeconds;
    }

    public void Stack(double now)
    {
        if (Stacks < Definition.MaxStacks) Stacks++;
        Refresh(now);
    }

    public void Extend(double now)
    {
        // 最大“有效可延长”上限：Duration * MaxStacks
        var maxTotal = Definition.DurationSeconds * Definition.MaxStacks;
        var remaining = ExpiresAt - now;
        var added = Definition.DurationSeconds;
        var newRemaining = remaining + added;
        if (newRemaining > maxTotal) newRemaining = maxTotal;
        ExpiresAt = now + newRemaining;
    }

    public bool IsExpired(double now) => now >= ExpiresAt;
}