using System.Collections.Generic;

namespace BlazorIdle.Server.Domain.Combat.Enemies;

public static class DungeonRegistry
{
    private static readonly Dictionary<string, DungeonDefinition> _defs = new()
    {
        // 示例：两波；波次刷新 3s；整轮刷新 10s
        ["intro_cave"] = new DungeonDefinition(
            id: "intro_cave",
            name: "Intro Cave",
            waves: new List<DungeonDefinition.Wave>
            {
                new DungeonDefinition.Wave(new List<(string,int)>
                {
                    ("dummy", 1),
                    ("paper", 2)
                }),
                new DungeonDefinition.Wave(new List<(string,int)>
                {
                    ("tank", 1)
                })
            },
            waveRespawnDelaySeconds: 3.0,
            runRespawnDelaySeconds: 10.0
        )
    };

    public static DungeonDefinition Resolve(string id)
        => _defs.TryGetValue(id, out var d) ? d : _defs["intro_cave"];

    public static IEnumerable<DungeonDefinition> All() => _defs.Values;
}