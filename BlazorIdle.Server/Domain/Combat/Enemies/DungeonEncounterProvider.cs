using System.Collections.Generic;

namespace BlazorIdle.Server.Domain.Combat.Enemies;

public sealed class DungeonEncounterProvider : IEncounterProvider
{
    private readonly DungeonDefinition _dungeon;
    private readonly bool _loop;
    private readonly double _waveDelay;
    private readonly double _runDelay;

    public EncounterGroup CurrentGroup { get; private set; }
    public int CurrentWaveIndex { get; private set; } = 1; // 1-based
    public int CompletedRunCount { get; private set; } = 0;

    // 可选覆盖：waveDelayOverride / runDelayOverride
    public DungeonEncounterProvider(DungeonDefinition dungeon, bool loop, double? waveDelayOverride = null, double? runDelayOverride = null)
    {
        _dungeon = dungeon;
        _loop = loop;
        _waveDelay = (waveDelayOverride ?? dungeon.WaveRespawnDelaySeconds) <= 0 ? 0 : (waveDelayOverride ?? dungeon.WaveRespawnDelaySeconds);
        _runDelay = (runDelayOverride ?? dungeon.RunRespawnDelaySeconds) <= 0 ? 0 : (runDelayOverride ?? dungeon.RunRespawnDelaySeconds);
        CurrentGroup = BuildGroupForWave(CurrentWaveIndex);
    }

    private EncounterGroup BuildGroupForWave(int waveIndex)
    {
        var wave = _dungeon.Waves[waveIndex - 1];
        var defs = new List<EnemyDefinition>();
        foreach (var (id, count) in wave.Enemies)
        {
            var def = EnemyRegistry.Resolve(id);
            var c = count <= 0 ? 1 : count;
            for (int i = 0; i < c; i++) defs.Add(def);
        }
        return new EncounterGroup(defs);
    }

    public bool TryAdvance(out EncounterGroup? nextGroup, out bool runCompleted)
    {
        runCompleted = false;
        if (CurrentWaveIndex < _dungeon.Waves.Count)
        {
            // 下一波
            CurrentWaveIndex++;
            nextGroup = BuildGroupForWave(CurrentWaveIndex);
            CurrentGroup = nextGroup;
            return true;
        }

        // 最后一波已清空
        runCompleted = true;
        CompletedRunCount++;

        if (_loop)
        {
            CurrentWaveIndex = 1;
            nextGroup = BuildGroupForWave(CurrentWaveIndex);
            CurrentGroup = nextGroup;
            return true;
        }

        nextGroup = null;
        return false; // 非循环：不再有下一波
    }

    public double GetRespawnDelaySeconds(bool runJustCompleted)
        => runJustCompleted ? _runDelay : _waveDelay;
}