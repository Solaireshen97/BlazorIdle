using System.Collections.Generic;

namespace BlazorIdle.Server.Domain.Combat.Enemies;

public sealed class DungeonDefinition
{
    public string Id { get; }
    public string Name { get; }
    public IReadOnlyList<Wave> Waves { get; }

    // 新增：波次刷新与整轮刷新等待
    public double WaveRespawnDelaySeconds { get; }
    public double RunRespawnDelaySeconds { get; }

    public DungeonDefinition(string id, string name, IReadOnlyList<Wave> waves, double waveRespawnDelaySeconds = 3.0, double runRespawnDelaySeconds = 10.0)
    {
        Id = id;
        Name = name;
        Waves = waves;
        WaveRespawnDelaySeconds = waveRespawnDelaySeconds <= 0 ? 0 : waveRespawnDelaySeconds;
        RunRespawnDelaySeconds = runRespawnDelaySeconds <= 0 ? 0 : runRespawnDelaySeconds;
    }

    public sealed class Wave
    {
        public IReadOnlyList<(string enemyId, int count)> Enemies { get; }
        public Wave(IReadOnlyList<(string enemyId, int count)> enemies) => Enemies = enemies;
    }
}