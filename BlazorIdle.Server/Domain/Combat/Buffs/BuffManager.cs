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
    private readonly Action<string, int, DamageType>? _dealDamage;
    // 新增：DoT 触发 Proc 的回调（由上层连接到 Procs.OnDirectHit）
    private readonly Action<string, DamageType, double>? _onDotDirectHit;

    // 新增：由上层注入的解析委托
    private readonly Func<double> _resolveHasteFactor;
    private readonly Func<(double ap, double sp)> _resolveAPSP;
    // 兼容旧签名（不吃面板急速与 AP/SP，且不触发 DoT Proc）
    public BuffManager(
        Action<string, int>? tagRecorder,
        Action<string, int>? resourceRecorder,
        Action<string, int, DamageType>? damageApplier)
        : this(tagRecorder, resourceRecorder, damageApplier, () => 1.0, () => (0.0, 0.0), onDotDirectHit: null)
    { }

    // 新构造：吃面板急速与 AP/SP（推荐）+ 可选 DoT Proc
    public BuffManager(
        Action<string, int>? tagRecorder,
        Action<string, int>? resourceRecorder,
        Action<string, int, DamageType>? damageApplier,
        Func<double> resolveHasteFactor,
        Func<(double ap, double sp)> resolveApsp,
        Action<string, DamageType, double>? onDotDirectHit)
    {
        _tag = tagRecorder;
        _resource = resourceRecorder;
        _dealDamage = damageApplier;
        _resolveHasteFactor = resolveHasteFactor;
        _resolveAPSP = resolveApsp;
        _onDotDirectHit = onDotDirectHit;
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

    private double CurrentHasteFactor() => _resolveHasteFactor();

    public BuffInstance Apply(string id, double now)
    {
        var def = GetDefinition(id);
        var haste = CurrentHasteFactor();
        var (ap, sp) = _resolveAPSP();

        if (_active.TryGetValue(id, out var existing))
        {
            switch (def.StackPolicy)
            {
                case BuffStackPolicy.Refresh:
                    existing.Refresh(now, haste, ap, sp);
                    _tag?.Invoke($"buff_refresh:{id}", 1);
                    break;
                case BuffStackPolicy.Stack:
                    existing.Stack(now, haste, ap, sp);
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
            var inst = new BuffInstance(def, 1, now, haste, ap, sp);
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
                        var dmg = (int)Math.Round(inst.TickBasePerStack * inst.Stacks);
                        if (dmg > 0)
                        {
                            var src = "buff_tick:" + id;
                            _dealDamage?.Invoke(src, dmg, def.PeriodicDamageType);
                            _tag?.Invoke("buff_tick:" + id, 1);

                            // 新增：可选触发 Proc（在 ProcManager 内部根据 AllowFromDot 控制）
                            _onDotDirectHit?.Invoke(src, def.PeriodicDamageType, now);
                        }
                        break;

                    case BuffPeriodicType.Resource:
                        if (def.PeriodicResourceId != null && def.PeriodicValue != 0)
                        {
                            _resource?.Invoke(def.PeriodicResourceId, def.PeriodicValue);
                            _tag?.Invoke("buff_tick:" + id, 1);
                        }
                        break;
                }

                if (inst.TickIntervalSeconds > 0)
                {
                    inst.NextTickAt = inst.NextTickAt.Value + inst.TickIntervalSeconds;
                }
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

            // Haste
            aggr.AdditiveHaste += def.AdditiveHaste * stacks;
            if (def.MultiplicativeHaste > 0)
                aggr.MultiplicativeHasteFactor *= (1 + def.MultiplicativeHaste * stacks);

            // 最终伤害乘区
            aggr.DamageMultiplierPhysical += def.DamageMultiplierPhysical * stacks;
            aggr.DamageMultiplierMagic += def.DamageMultiplierMagic * stacks;
            aggr.DamageMultiplierTrue += def.DamageMultiplierTrue * stacks;

            // 穿透
            aggr.ArmorPenFlat += def.ArmorPenFlat * stacks;
            aggr.ArmorPenPct += def.ArmorPenPct * stacks;
            aggr.MagicPenFlat += def.MagicPenFlat * stacks;
            aggr.MagicPenPct += def.MagicPenPct * stacks;

            // 暴击
            aggr.CritChanceBonus += def.CritChanceBonus * stacks;
            aggr.CritMultiplierBonus += def.CritMultiplierBonus * stacks;
        }

        aggr.ArmorPenPct = Clamp01(aggr.ArmorPenPct);
        aggr.MagicPenPct = Clamp01(aggr.MagicPenPct);

        Aggregate = aggr;
    }

    public IEnumerable<BuffInstance> Active => _active.Values;
}