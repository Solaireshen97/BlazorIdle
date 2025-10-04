using System.Collections.Generic;
using BlazorIdle.Server.Domain.Combat.Damage;

namespace BlazorIdle.Server.Domain.Combat;

public class SegmentCollector
{
    private readonly List<(string source, int dmg, DamageType type)> _damageEvents = new();
    private readonly Dictionary<string, int> _tagCounters = new();
    private readonly Dictionary<string, int> _resourceFlow = new();

    public int EventCount { get; private set; }
    public double SegmentStart { get; private set; }
    public double LastEventTime { get; private set; }
    private readonly int _maxEvents;
    private readonly double _maxDuration;

    public SegmentCollector(int maxEvents = 200, double maxDuration = 5)
    {
        _maxEvents = maxEvents;
        _maxDuration = maxDuration;
        SegmentStart = 0;
    }

    // 兼容旧签名（默认物理伤害）
    public void OnDamage(string src, int dmg) => OnDamage(src, dmg, DamageType.Physical);

    public void OnDamage(string src, int dmg, DamageType type)
    {
        _damageEvents.Add((src, dmg, type));
        EventCount++;
    }

    public void OnTag(string tag, int count)
    {
        if (!_tagCounters.ContainsKey(tag)) _tagCounters[tag] = 0;
        _tagCounters[tag] += count;
        EventCount++;
    }

    public void OnResourceChange(string resourceId, int delta)
    {
        if (delta == 0) return;
        if (!_resourceFlow.ContainsKey(resourceId)) _resourceFlow[resourceId] = 0;
        _resourceFlow[resourceId] += delta;
    }

    public void Tick(double currentTime) => LastEventTime = currentTime;

    public bool ShouldFlush(double currentTime) =>
        EventCount >= _maxEvents || (currentTime - SegmentStart) >= _maxDuration;

    public CombatSegment Flush(double currentTime)
    {
        var total = 0;
        var bySource = new Dictionary<string, int>();
        var byType = new Dictionary<string, int>();

        foreach (var (src, dmg, type) in _damageEvents)
        {
            total += dmg;
            if (!bySource.ContainsKey(src)) bySource[src] = 0;
            bySource[src] += dmg;

            var typeKey = type.ToString().ToLowerInvariant();
            if (!byType.ContainsKey(typeKey)) byType[typeKey] = 0;
            byType[typeKey] += dmg;
        }

        var seg = new CombatSegment
        {
            StartTime = SegmentStart,
            EndTime = currentTime,
            EventCount = EventCount,
            TotalDamage = total,
            DamageBySource = bySource,
            DamageByType = byType,
            TagCounters = new Dictionary<string, int>(_tagCounters),
            ResourceFlow = new Dictionary<string, int>(_resourceFlow)
        };

        _damageEvents.Clear();
        _tagCounters.Clear();
        _resourceFlow.Clear();
        EventCount = 0;
        SegmentStart = currentTime;
        return seg;
    }
}

public class CombatSegment
{
    public double StartTime { get; set; }
    public double EndTime { get; set; }
    public int EventCount { get; set; }
    public int TotalDamage { get; set; }
    public Dictionary<string, int> DamageBySource { get; set; } = new();
    public Dictionary<string, int> DamageByType { get; set; } = new(); // 新增：按类型统计
    public Dictionary<string, int> TagCounters { get; set; } = new();
    public Dictionary<string, int> ResourceFlow { get; set; } = new();
}