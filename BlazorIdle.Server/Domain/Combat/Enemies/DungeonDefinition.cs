using System.Collections.Generic;

namespace BlazorIdle.Server.Domain.Combat.Enemies;

public sealed class DungeonDefinition
{
    public string Id { get; }
    public string Name { get; }
    public IReadOnlyList<Wave> Waves { get; }

    public DungeonDefinition(string id, string name, IReadOnlyList<Wave> waves)
    {
        Id = id;
        Name = name;
        Waves = waves;
    }

    public sealed class Wave
    {
        public IReadOnlyList<(string enemyId, int count)> Enemies { get; }
        public Wave(IReadOnlyList<(string enemyId, int count)> enemies) => Enemies = enemies;
    }
}