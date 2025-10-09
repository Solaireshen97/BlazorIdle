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
            magicResist: 0.1,
            baseDamage: 5,                              // Phase 4: Weak attack for training
            attackDamageType: Damage.DamageType.Physical,
            attackIntervalSeconds: 2.0
        ),
        ["tank"] = new EnemyDefinition(
            id: "tank",
            name: "Armored Golem",
            level: 10,
            maxHp: 500,
            armor: 400,
            magicResist: 0.15,
            baseDamage: 15,                             // Phase 4: Moderate attack, 7.5 DPS
            attackDamageType: Damage.DamageType.Physical,
            attackIntervalSeconds: 2.0
        ),
        ["magebane"] = new EnemyDefinition(
            id: "magebane",
            name: "Magebane Warden",
            level: 10,
            maxHp: 380,
            armor: 120,
            magicResist: 0.35,
            baseDamage: 12,                             // Phase 4: Magic attack, 6 DPS
            attackDamageType: Damage.DamageType.Magic,
            attackIntervalSeconds: 2.0
        ),
        ["paper"] = new EnemyDefinition(
            id: "paper",
            name: "Paper Slime",
            level: 1,
            maxHp: 80,
            armor: 0,
            magicResist: 0.0,
            vulnPhys: 0.1,
            vulnMagic: 0.1,
            baseDamage: 8,                              // Phase 4: Fast but weak attack, 5.3 DPS
            attackDamageType: Damage.DamageType.Physical,
            attackIntervalSeconds: 1.5
        ),
    };

    public static EnemyDefinition Resolve(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return _defs["dummy"];
        return _defs.TryGetValue(id, out var d) ? d : _defs["dummy"];
    }

    public static IEnumerable<EnemyDefinition> All() => _defs.Values;
}