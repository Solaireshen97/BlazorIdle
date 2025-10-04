using System.Collections.Generic;
using System.Linq;

namespace BlazorIdle.Server.Domain.Combat.Enemies;

public class EncounterGroup
{
    private readonly List<Encounter> _encounters = new();

    public EncounterGroup(IEnumerable<EnemyDefinition> enemies)
    {
        foreach (var e in enemies)
            _encounters.Add(new Encounter(e));
    }

    public static EncounterGroup FromSingle(Encounter single)
        => new EncounterGroup(new[] { single.Enemy });

    public IReadOnlyList<Encounter> All => _encounters;

    public Encounter? PrimaryAlive()
        => _encounters.FirstOrDefault(e => !e.IsDead) ?? _encounters.FirstOrDefault();

    public List<Encounter> SelectAlive(int maxTargets, bool includePrimary = true)
    {
        var list = new List<Encounter>();
        var alive = _encounters.Where(e => !e.IsDead).ToList();
        if (alive.Count == 0)
            alive = _encounters.ToList();

        if (!includePrimary)
            alive = alive.Skip(1).ToList();

        foreach (var t in alive)
        {
            list.Add(t);
            if (list.Count >= maxTargets) break;
        }

        return list;
    }
}