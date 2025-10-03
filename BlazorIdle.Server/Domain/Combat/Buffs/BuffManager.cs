using System;
using System.Collections.Generic;

namespace BlazorIdle.Server.Domain.Combat.Buffs;

public class BuffManager
{
    private readonly Dictionary<string, BuffInstance> _active = new();
    private readonly List<BuffDefinition> _definitions = new(); // 可替换为外部数据源
    public BuffAggregate Aggregate { get; private set; } = new();

    private readonly Action<string, int>? _tag;       // 用于记录 tagCounters
    private readonly Action<string, int>? _resource;  // ResourceFlow（periodic resource）
    private readonly Action<string, int>? _damage;    // Damage source

    public BuffManager(
        Action<string, int>? tagRecorder,
        Action<string, int>? resourceRecorder,
        Action<string, int>? damageRecorder)
    {
        _tag = tagRecorder;
        _resource = resourceRecorder;
        _damage = damageRecorder;
    }

    public void RegisterDefinition(BuffDefinition def)
    {
        _definitions.Add(def);
    }

    public BuffDefinition GetDefinition(string id)
        => _definitions.Find(d => d.Id == id) ?? throw new InvalidOperationException($"BuffDefinition {id} not found");

    public bool Has(string id) => _active.ContainsKey(id);

    public BuffInstance? TryGet(string id)
        => _active.TryGetValue(id, out var inst) ? inst : null;

    public BuffInstance Apply(string id, double now)
    {
        var def = GetDefinition(id);
        if (_active.TryGetValue(id, out var existing))
        {
            switch (def.StackPolicy)
            {
                case BuffStackPolicy.Refresh:
                    existing.Refresh(now);
                    _tag?.Invoke($"buff_refresh:{id}", 1);
                    break;
                case BuffStackPolicy.Stack:
                    existing.Stack(now);
                    _tag?.Invoke($"buff_stack:{id}", 1);
                    break;
                case BuffStackPolicy.Extend:
                    existing.Extend(now);
                    _tag?.Invoke($"buff_extend:{id}", 1);
                    break;
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
        // 周期 Tick & 过期检测
        var toRemove = new List<string>();
        foreach (var (id, inst) in _active)
        {
            // Periodic
            if (inst.NextTickAt.HasValue && now >= inst.NextTickAt.Value)
            {
                var def = inst.Definition;
                switch (def.PeriodicType)
                {
                    case BuffPeriodicType.Damage:
                        _damage?.Invoke("buff_tick:" + id, def.PeriodicValue);
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

            // Expire
            if (inst.IsExpired(now))
            {
                toRemove.Add(id);
            }
        }

        foreach (var rid in toRemove)
        {
            _active.Remove(rid);
            _tag?.Invoke($"buff_expire:{rid}", 1);
        }

        if (toRemove.Count > 0)
            RecalcAggregate();
    }

    private void RecalcAggregate()
    {
        var aggr = new BuffAggregate();
        foreach (var inst in _active.Values)
        {
            var def = inst.Definition;
            // Additive haste 累积 *层数*
            aggr.AdditiveHaste += def.AdditiveHaste * inst.Stacks;
            // Multiplicative haste 乘积
            if (def.MultiplicativeHaste > 0)
                aggr.MultiplicativeHasteFactor *= (1 + def.MultiplicativeHaste * inst.Stacks);
        }
        Aggregate = aggr;
    }

    public IEnumerable<BuffInstance> Active => _active.Values;
}