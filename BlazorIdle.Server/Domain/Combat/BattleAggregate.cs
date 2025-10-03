using System;

namespace BlazorIdle.Server.Domain.Combat;

public class Battle
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CharacterId { get; init; }
    public double AttackIntervalSeconds { get; init; } = 1.5;
    public double SpecialIntervalSeconds { get; init; } = 5.0; // 新增
    public bool IsFinished { get; private set; }
    public double StartedAt { get; init; }
    public double? EndedAt { get; private set; }

    public void Finish(double time)
    {
        if (IsFinished) return;
        IsFinished = true;
        EndedAt = time;
    }
}