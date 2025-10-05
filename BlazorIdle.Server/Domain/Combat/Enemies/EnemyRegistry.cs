using System.Collections.Generic;

namespace BlazorIdle.Server.Domain.Combat.Enemies;

public static class EnemyRegistry
{
    private static readonly Dictionary<string, EnemyDefinition> _defs = new()
    {
        ["dummy"] = new EnemyDefinition(
            id: "dummy",
            name: "Training Dummy",
            level: 1,
            maxHp: 150,
            armor: 50,
            magicResist: 0.1
        ),
        ["tank"] = new EnemyDefinition(
            id: "tank",
            name: "Armored Golem",
            level: 10,
            maxHp: 500,
            armor: 400,
            magicResist: 0.15
        ),
        ["magebane"] = new EnemyDefinition(
            id: "magebane",
            name: "Magebane Warden",
            level: 10,
            maxHp: 380,
            armor: 120,
            magicResist: 0.35
        ),
        ["paper"] = new EnemyDefinition(
            id: "paper",
            name: "Paper Slime",
            level: 1,
            maxHp: 80,
            armor: 0,
            magicResist: 0.0,
            vulnPhys: 0.1,
            vulnMagic: 0.1
        ),
    };

    public static EnemyDefinition Resolve(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return _defs["dummy"];
        return _defs.TryGetValue(id, out var d) ? d : _defs["dummy"];
    }

    public static IEnumerable<EnemyDefinition> All() => _defs.Values;
}