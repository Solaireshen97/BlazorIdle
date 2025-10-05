using System.Collections.Generic;

namespace BlazorIdle.Server.Domain.Combat.Enemies;

public sealed class ContinuousEncounterProvider : IEncounterProvider
{
    private readonly EnemyDefinition _enemy;
    private readonly int _count;
    private readonly double _respawnDelaySeconds;

    public EncounterGroup CurrentGroup { get; private set; }
    public int CurrentWaveIndex { get; private set; } = 1;
    public int CompletedRunCount { get; private set; } = 0;

    // respawnDelaySeconds: 击杀后刷新等待，默认 3s
    public ContinuousEncounterProvider(EnemyDefinition enemy, int count, double respawnDelaySeconds = 3.0)
    {
        _enemy = enemy;
        _count = count <= 0 ? 1 : count;
        _respawnDelaySeconds = respawnDelaySeconds <= 0 ? 0 : respawnDelaySeconds;
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
        // 持续模式：永远重生同配置
        nextGroup = BuildGroup();
        runCompleted = false;
        CurrentGroup = nextGroup;
        return true;
    }

    public double GetRespawnDelaySeconds(bool runJustCompleted) => _respawnDelaySeconds;
}