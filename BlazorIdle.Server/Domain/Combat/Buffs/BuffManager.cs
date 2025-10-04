using System;
using System.Collections.Generic;
using BlazorIdle.Server.Domain.Combat.Damage;

namespace BlazorIdle.Server.Domain.Combat.Buffs;

public class BuffManager
{
    private readonly Dictionary<string, BuffInstance> _active = new();
    private readonly List<BuffDefinition> _definitions = new();
    public BuffAggregate Aggregate { get; private set; } = new();

    private readonly Action<string, int>? _tag;
    private readonly Action<string, int>? _resource;
    // 修改：周期伤害现在通过 DamageCalculator 结算，因此带 DamageType
    private readonly Action<string, int, DamageType>? _dealDamage;

    public BuffManager(
        Action<string, int>? tagRecorder,
        Action<string, int>? resourceRecorder,
        Action<string, int, DamageType>? damageApplier)
    {
        _tag = tagRecorder;
        _resource = resourceRecorder;
        _dealDamage = damageApplier;
    }

    public void RegisterDefinition(BuffDefinition def)
    {
        if (_definitions.Exists(d => d.Id == def.Id)) return;
        _definitions.Add(def);
    }

    public BuffDefinition GetDefinition(string id)
        => _definitions.Find(d => d.Id == id) ?? throw new InvalidOperationException($"BuffDefinition {id} not found");

    public bool Has(string id) => _active.ContainsKey(id);
    public BuffInstance? TryGet(string id) => _active.TryGetValue(id, out var inst) ? inst : null;

    public BuffInstance Apply(string id, double now)
    {
        var def = GetDefinition(id);
        if (_active.TryGetValue(id, out var existing))
        {
            switch (def.StackPolicy)
            {
                case BuffStackPolicy.Refresh: existing.Refresh(now); _tag?.Invoke($"buff_refresh:{id}", 1); break;
                case BuffStackPolicy.Stack: existing.Stack(now); _tag?.Invoke($"buff_stack:{id}", 1); break;
                case BuffStackPolicy.Extend: existing.Extend(now); _tag?.Invoke($"buff_extend:{id}", 1); break;
            }
            RecalcAggregate();
            return existing;
        }
        else
        {
            var inst = new BuffInstance(def, 1, now);
            _active[id] = inst;
            _tag?.Invoke($"buff_apply:{id}", 1);
            RecalcAggregate();
            return inst;
        }
    }

    public void Remove(string id, double now)
    {
        if (_active.Remove(id))
        {
            _tag?.Invoke($"buff_remove:{id}", 1);
            RecalcAggregate();
        }
    }

    public void Tick(double now)
    {
        var toRemove = new List<string>();
        foreach (var (id, inst) in _active)
        {
            if (inst.NextTickAt.HasValue && now >= inst.NextTickAt.Value)
            {
                var def = inst.Definition;
                switch (def.PeriodicType)
                {
                    case BuffPeriodicType.Damage:
                        // 通过 DamageCalculator 结算到目标
                        _dealDamage?.Invoke("buff_tick:" + id, def.PeriodicValue, def.PeriodicDamageType);
                        _tag?.Invoke("buff_tick:" + id, 1);
                        break;
                    case BuffPeriodicType.Resource:
                        if (def.PeriodicResourceId != null && def.PeriodicValue != 0)
                        {
                            _resource?.Invoke(def.PeriodicResourceId, def.PeriodicValue);
                            _tag?.Invoke("buff_tick:" + id, 1);
                        }
                        break;
                }

                if (def.PeriodicInterval.HasValue)
                    inst.NextTickAt = inst.NextTickAt.Value + def.PeriodicInterval.Value;
            }

            if (inst.IsExpired(now))
                toRemove.Add(id);
        }

        foreach (var rid in toRemove)
        {
            _active.Remove(rid);
            _tag?.Invoke($"buff_expire:{rid}", 1);
        }

        if (toRemove.Count > 0)
            RecalcAggregate();
    }

    private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);

    private void RecalcAggregate()
    {
        var aggr = new BuffAggregate();

        foreach (var inst in _active.Values)
        {
            var def = inst.Definition;
            var stacks = inst.Stacks;

            aggr.AdditiveHaste += def.AdditiveHaste * stacks;
            if (def.MultiplicativeHaste > 0)
                aggr.MultiplicativeHasteFactor *= (1 + def.MultiplicativeHaste * stacks);

            aggr.DamageMultiplierPhysical += def.DamageMultiplierPhysical * stacks;
            aggr.DamageMultiplierMagic += def.DamageMultiplierMagic * stacks;
            aggr.DamageMultiplierTrue += def.DamageMultiplierTrue * stacks;

            aggr.ArmorPenFlat += def.ArmorPenFlat * stacks;
            aggr.ArmorPenPct += def.ArmorPenPct * stacks;
            aggr.MagicPenFlat += def.MagicPenFlat * stacks;
            aggr.MagicPenPct += def.MagicPenPct * stacks;

            aggr.CritChanceBonus += def.CritChanceBonus * stacks;
            aggr.CritMultiplierBonus += def.CritMultiplierBonus * stacks;
        }

        aggr.ArmorPenPct = Clamp01(aggr.ArmorPenPct);
        aggr.MagicPenPct = Clamp01(aggr.MagicPenPct);

        Aggregate = aggr;
    }

    public IEnumerable<BuffInstance> Active => _active.Values;
}