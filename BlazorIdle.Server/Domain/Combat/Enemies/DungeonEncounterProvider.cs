using System.Collections.Generic;

namespace BlazorIdle.Server.Domain.Combat.Enemies;

public sealed class DungeonEncounterProvider : IEncounterProvider
{
    private readonly DungeonDefinition _dungeon;
    private readonly bool _loop;
    private readonly double _waveDelay;
    private readonly double _runDelay;

    public EncounterGroup CurrentGroup { get; private set; }
    public int CurrentWaveIndex { get; private set; } = 1;
    public int CompletedRunCount { get; private set; } = 0;

    // 新增：对外暴露 DungeonId，便于同步 Runner 打 ctx 标签
    public string DungeonId => _dungeon.Id;
    
    // Phase 6: 对外暴露副本定义，用于读取强化配置
    public DungeonDefinition Dungeon => _dungeon;

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
            CurrentWaveIndex++;
            nextGroup = BuildGroupForWave(CurrentWaveIndex);
            CurrentGroup = nextGroup;
            return true;
        }

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
        return false;
    }

    public double GetRespawnDelaySeconds(bool runJustCompleted)
        => runJustCompleted ? _runDelay : _waveDelay;
}