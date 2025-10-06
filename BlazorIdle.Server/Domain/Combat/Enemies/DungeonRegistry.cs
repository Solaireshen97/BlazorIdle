using System.Collections.Generic;

namespace BlazorIdle.Server.Domain.Combat.Enemies;

public static class DungeonRegistry
{
    private static readonly Dictionary<string, DungeonDefinition> _defs = new()
    {
        // intro_cave：两波；波间 3s；整轮 10s；经济：金币1.1倍、经验1.05倍、掉率1.05倍；
        // 每轮完成奖励：+50 金币，+30 经验，奖励表 loot_common 抽取 3 次
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
            runRespawnDelaySeconds: 10.0,
            goldMultiplier: 1.10,
            expMultiplier: 1.05,
            dropChanceMultiplier: 1.05,
            runRewardGold: 50,
            runRewardExp: 30,
            runRewardLootTableId: "loot_common",
            runRewardLootRolls: 3
        )
    };

    public static DungeonDefinition Resolve(string id)
        => _defs.TryGetValue(id, out var d) ? d : _defs["intro_cave"];

    public static IEnumerable<DungeonDefinition> All() => _defs.Values;
}