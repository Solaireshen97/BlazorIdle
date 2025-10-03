using System.Collections.Generic;

namespace BlazorWebGame.Domain.Combat;

/// <summary>
/// 事件调度器抽象：战斗模拟中的“时间轴优先队列”
/// 负责：
///   1. 接收未来要执行的游戏事件 (IGameEvent)
///   2. 按事件的 ExecuteAt (逻辑时间) 排序
///   3. 逐个弹出最早应执行的事件供 BattleRunner 处理
/// 约束：
///   - 不保证线程安全（当前 BattleRunner 单线程循环）
///   - 不做去重 / 合并；同一时间的多个事件按底层堆结构不稳定顺序弹出
/// </summary>
public interface IEventScheduler
{
    /// <summary>
    /// 投递一个事件；事件内部至少包含 ExecuteAt 时间戳。
    /// 时间可为当前时间之后（>= 当前逻辑时间），缺乏校验由调用方保证。
    /// </summary>
    void Schedule(IGameEvent ev);

    /// <summary>
    /// 弹出“最早需要执行”的事件；没有则返回 null。
    /// BattleRunner 主循环使用它推进模拟。
    /// </summary>
    IGameEvent? PopNext();

    /// <summary>
    /// 当前待执行事件数量。
    /// 用于循环终止条件 (Count == 0)。
    /// </summary>
    int Count { get; }
}

/// <summary>
/// 默认事件调度器实现：基于 .NET 内置的二叉堆 PriorityQueue。
/// Key = ExecuteAt (double)；Value = IGameEvent。
/// 复杂度：
///   - Schedule: O(log n)
///   - PopNext : O(log n)
///   - Count   : O(1)
/// 适用场景：事件数量中等（几十 ~ 几万），不需要稳定排序；
/// 若未来需要百万级事件或更强控制，可考虑自建结构或对象池减少 GC。
/// </summary>
public class EventScheduler : IEventScheduler
{
    // PriorityQueue<TElement, TPriority>: 最小堆；优先级越小越先出
    private readonly PriorityQueue<IGameEvent, double> _pq = new();

    /// <inheritdoc />
    public void Schedule(IGameEvent ev)
    {
        // 直接用事件的 ExecuteAt 作为优先级
        _pq.Enqueue(ev, ev.ExecuteAt);
    }

    /// <inheritdoc />
    public IGameEvent? PopNext()
    {
        // TryDequeue 返回 (element, priority)
        return _pq.TryDequeue(out var ev, out _) ? ev : null;
    }

    /// <inheritdoc />
    public int Count => _pq.Count;
}