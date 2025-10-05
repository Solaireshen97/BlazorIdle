using System.Collections.Generic;
using System.Linq;

namespace BlazorIdle.Server.Domain.Combat.Enemies;

public sealed class DungeonEncounterProvider : IEncounterProvider
{
    private readonly DungeonDefinition _dungeon;
    private readonly bool _loop;

    public EncounterGroup CurrentGroup { get; private set; }
    public int CurrentWaveIndex { get; private set; } = 1; // 1-based
    public int CompletedRunCount { get; private set; } = 0;

    public DungeonEncounterProvider(DungeonDefinition dungeon, bool loop)
    {
        _dungeon = dungeon;
        _loop = loop;
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
}