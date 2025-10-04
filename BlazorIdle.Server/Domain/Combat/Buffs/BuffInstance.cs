namespace BlazorIdle.Server.Domain.Combat.Buffs;

public sealed class BuffInstance
{
    public BuffDefinition Definition { get; }
    public int Stacks { get; private set; }
    public double AppliedAt { get; }
    public double ExpiresAt { get; private set; }
    public double? NextTickAt { get; set; }

    // DoT/HoT 强化：周期相关快照
    public double TickIntervalSeconds { get; private set; } // 0 表示无周期
    public double HasteSnapshot { get; private set; }       // 仅用于记录/调试

    public BuffInstance(BuffDefinition def, int stacks, double now, double hasteFactor)
    {
        Definition = def;
        Stacks = System.Math.Max(1, stacks);
        AppliedAt = now;

        ExpiresAt = now + def.DurationSeconds;

        if (def.PeriodicType != BuffPeriodicType.None && def.PeriodicInterval.HasValue && def.PeriodicInterval.Value > 0)
        {
            HasteSnapshot = hasteFactor <= 0 ? 1.0 : hasteFactor;
            TickIntervalSeconds = def.PeriodicHasteAffected
                ? System.Math.Max(0.01, def.PeriodicInterval.Value / HasteSnapshot)
                : def.PeriodicInterval.Value;

            NextTickAt = now + TickIntervalSeconds;
        }
        else
        {
            HasteSnapshot = 1.0;
            TickIntervalSeconds = 0;
            NextTickAt = null;
        }
    }

    public bool IsExpired(double now) => now >= ExpiresAt;

    // 刷新：Pandemic 规则 + 重置 tick 计时（按当前 haste 重新快照）
    public void Refresh(double now, double hasteFactor)
    {
        var baseDur = Definition.DurationSeconds;
        var remaining = System.Math.Max(0, ExpiresAt - now);
        var carryCap = baseDur * System.Math.Clamp(Definition.PandemicRatio, 0, 1);
        var newExpire = now + System.Math.Min(baseDur + remaining, baseDur + carryCap);
        ExpiresAt = newExpire;

        // 重新快照周期（若有）
        if (Definition.PeriodicType != BuffPeriodicType.None && Definition.PeriodicInterval.HasValue && Definition.PeriodicInterval.Value > 0)
        {
            HasteSnapshot = hasteFactor <= 0 ? 1.0 : hasteFactor;
            TickIntervalSeconds = Definition.PeriodicHasteAffected
                ? System.Math.Max(0.01, Definition.PeriodicInterval.Value / HasteSnapshot)
                : Definition.PeriodicInterval.Value;

            NextTickAt = now + TickIntervalSeconds;
        }
    }

    // 叠层：上限内 +1，并按刷新规则处理持续时间与 tick 重置
    public void Stack(double now, double hasteFactor)
    {
        Stacks = System.Math.Min(Definition.MaxStacks, Stacks + 1);
        Refresh(now, hasteFactor);
    }

    // 延长：直接延长持续时间（不变更 tick 间隔与下一跳时间）
    public void Extend(double now)
    {
        ExpiresAt += Definition.DurationSeconds;
    }
}