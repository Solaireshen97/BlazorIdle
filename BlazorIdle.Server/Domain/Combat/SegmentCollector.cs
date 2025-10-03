using System.Collections.Generic;

namespace BlazorWebGame.Domain.Combat;

/// <summary>
/// SegmentCollector = “战斗分段聚合器”
/// 作用：在事件循环中持续接收离散的伤害事件，
/// 按【事件数量阈值】或【时间跨度阈值】切分成 CombatSegment 段，
/// 提供 Flush() 生成聚合统计（总伤害 + 来源伤害汇总）。
/// 注意：它不是简单写 log，而是“按策略切块聚合数据”。
/// </summary>
public class SegmentCollector
{
    // 暂存本段内的所有原始伤害事件 (来源标签, 数值)
    private readonly List<(string source, int dmg)> _damageEvents = new();

    /// <summary> 当前段累计的伤害事件个数（等价于 _damageEvents.Count，但独立存是为了少一次属性访问） </summary>
    public int EventCount { get; private set; }

    /// <summary> 当前段的起始逻辑时间（上一段 Flush 后设为 Flush 时刻） </summary>
    public double SegmentStart { get; private set; }

    /// <summary> 最近一次 Tick() 记录的当前逻辑时间（可用于调试/对齐） </summary>
    public double LastEventTime { get; private set; }

    private readonly int _maxEvents;      // 事件数量阈值：达到就切段
    private readonly double _maxDuration; // 时间跨度阈值：超过就切段

    /// <param name="maxEvents">单段最大事件数上限（默认 200）</param>
    /// <param name="maxDuration">单段最大时间长度（秒，默认 5）</param>
    public SegmentCollector(int maxEvents = 200, double maxDuration = 5)
    {
        _maxEvents = maxEvents;
        _maxDuration = maxDuration;
        SegmentStart = 0;
    }

    /// <summary>
    /// 接收一次伤害事件（来源标签 + 数值）
    /// 不做统计聚合，先缓存，等 Flush 时再汇总。
    /// </summary>
    public void OnDamage(string src, int dmg)
    {
        _damageEvents.Add((src, dmg));
        EventCount++;
    }

    /// <summary>
    /// 在 BattleRunner 每个事件执行后调用，用来更新“当前时间”记录。
    /// （现在只记录，不触发逻辑；可扩展：Idle 超时切段等）
    /// </summary>
    public void Tick(double currentTime)
    {
        LastEventTime = currentTime;
    }

    /// <summary>
    /// 是否需要切段：
    /// 1. 达到事件数量阈值
    /// 2. 当前时间 - 段开始时间 >= 最大段时长
    /// 只要满足其一就可 Flush。
    /// </summary>
    public bool ShouldFlush(double currentTime) =>
        EventCount >= _maxEvents || (currentTime - SegmentStart) >= _maxDuration;

    /// <summary>
    /// 生成一个 CombatSegment 并重置内部累积状态，准备采集下一段。
    /// 统计逻辑：遍历本段原始事件列表 → 汇总总伤害、按来源聚合。
    /// 复杂度：O(本段事件数)。
    /// </summary>
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
            DamageBySource = bySource
        };

        // 清理并准备下一段
        _damageEvents.Clear();
        EventCount = 0;
        SegmentStart = currentTime;
        return seg;
    }
}

/// <summary>
/// 战斗分段统计结果：
/// - StartTime / EndTime：逻辑时间范围
/// - EventCount：段内伤害事件个数
/// - TotalDamage：段内总伤害
/// - DamageBySource：按来源聚合（来源 = 标签，如 basic_attack / skill_x）
/// </summary>
public class CombatSegment
{
    public double StartTime { get; set; }
    public double EndTime { get; set; }
    public int EventCount { get; set; }
    public int TotalDamage { get; set; }
    public Dictionary<string, int> DamageBySource { get; set; } = new();
}