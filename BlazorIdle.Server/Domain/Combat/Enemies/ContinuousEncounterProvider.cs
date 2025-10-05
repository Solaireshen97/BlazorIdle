using System.Collections.Generic;

namespace BlazorIdle.Server.Domain.Combat.Enemies;

public sealed class ContinuousEncounterProvider : IEncounterProvider
{
    private readonly EnemyDefinition _enemy;
    private readonly int _count;

    public EncounterGroup CurrentGroup { get; private set; }
    public int CurrentWaveIndex { get; private set; } = 1;
    public int CompletedRunCount { get; private set; } = 0;

    public ContinuousEncounterProvider(EnemyDefinition enemy, int count)
    {
        _enemy = enemy;
        _count = count <= 0 ? 1 : count;
        CurrentGroup = BuildGroup();
    }

    private EncounterGroup BuildGroup()
    {
        var defs = new List<EnemyDefinition>();
        for (int i = 0; i < _count; i++) defs.Add(_enemy);
        return new EncounterGroup(defs);
    }

    public bool TryAdvance(out EncounterGroup? nextGroup, out bool runCompleted)
    {
        // 连续模式：永远重生同配置
        nextGroup = BuildGroup();
        runCompleted = false;
        CurrentGroup = nextGroup;
        return true;
    }
}