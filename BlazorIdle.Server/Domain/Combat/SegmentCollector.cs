using System.Collections.Generic;

namespace BlazorIdle.Server.Domain.Combat;

public class SegmentCollector
{
    private readonly List<(string source, int dmg)> _damageEvents = new();
    private readonly Dictionary<string, int> _tagCounters = new();
    private readonly Dictionary<string, int> _resourceFlow = new(); // 新增
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

    public void OnDamage(string src, int dmg)
    {
        _damageEvents.Add((src, dmg));
        EventCount++;
    }

    public void OnTag(string tag, int count)
    {
        if (!_tagCounters.ContainsKey(tag)) _tagCounters[tag] = 0;
        _tagCounters[tag] += count;
        EventCount++;
    }

    // 新增：记录资源增量（净变化）
    public void OnResourceChange(string resourceId, int delta)
    {
        if (delta == 0) return;
        if (!_resourceFlow.ContainsKey(resourceId)) _resourceFlow[resourceId] = 0;
        _resourceFlow[resourceId] += delta;
    }

    public void Tick(double currentTime)
    {
        LastEventTime = currentTime;
    }

    public bool ShouldFlush(double currentTime) =>
        EventCount >= _maxEvents || (currentTime - SegmentStart) >= _maxDuration;

    public CombatSegment Flush(double currentTime)
    {
        var total = 0;
        var bySource = new Dictionary<string, int>();
        foreach (var (src, dmg) in _damageEvents)
        {
            total += dmg;
            if (!bySource.ContainsKey(src)) bySource[src] = 0;
            bySource[src] += dmg;
        }
        var seg = new CombatSegment
        {
            StartTime = SegmentStart,
            EndTime = currentTime,
            EventCount = EventCount,
            TotalDamage = total,
            DamageBySource = bySource,
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
    public Dictionary<string, int> TagCounters { get; set; } = new();
    public Dictionary<string, int> ResourceFlow { get; set; } = new(); // 新增
}