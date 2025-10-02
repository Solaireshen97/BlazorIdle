using System;

namespace BlazorWebGame.Domain.Combat;

public class Battle
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CharacterId { get; init; }
    public double AttackIntervalSeconds { get; init; } = 1.5; // 后续可带急速
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