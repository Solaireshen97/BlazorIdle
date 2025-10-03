using BlazorIdle.Server.Domain.Combat;

namespace BlazorWebGame.Domain.Combat;

/// <summary>
/// 战斗执行上下文（Context）
/// 作用：把一场战斗模拟运行过程中需要被事件访问的“共同依赖”打包在一起，
/// 以便每个 IGameEvent.Execute(context) 时可以：
///   - 访问 Battle（战斗总体状态：角色、是否结束、AttackInterval 等）
///   - 读取/推进时间 (IGameClock)
///   - 向调度器继续投递后续事件 (IEventScheduler)
///   - 记录统计数据 / 伤害事件 (SegmentCollector)
/// 好处：避免在事件构造函数里注入一堆散乱参数，集中成为一个语义明确的容器；
/// 事件生命周期短 → 用 Context 聚合依赖更轻量。
/// 注意：此类本身保持“贫血”只做聚合，不做业务逻辑；业务仍由事件 / 收集器 / Battle 本身承担。
/// </summary>
public class BattleContext
{
    /// <summary>
    /// 当前这场战斗的领域对象（逻辑时间空间里存在）
    /// 只在内存中使用，不直接与 EF 持久化模型耦合。
    /// </summary>
    public Battle Battle { get; }

    /// <summary>
    /// 逻辑时钟：保存当前模拟时间（double 秒），
    /// 事件循环外部（BattleRunner）推进它，再由事件读取。
    /// </summary>
    public IGameClock Clock { get; }

    /// <summary>
    /// 事件调度器：事件执行中可以继续 Schedule 新事件（如下一次攻击、DOT Tick）。
    /// 内部通常是按 ExecuteAt 排序的优先队列。
    /// </summary>
    public IEventScheduler Scheduler { get; }

    /// <summary>
    /// 分段统计收集器：事件执行可写入伤害事件，BattleRunner 循环中调用 Tick / Flush。
    /// 用于把大量细粒度事件聚合成 CombatSegment。
    /// </summary>
    public SegmentCollector SegmentCollector { get; }


    public List<TrackState> Tracks { get; } = new(); // 新增
    /// <summary>
    /// 构造：将运行时协作者聚合。
    /// 所有参数都应为非空（当前未加校验，依赖调用方保证）。
    /// </summary>
    public BattleContext(Battle battle, IGameClock clock, IEventScheduler scheduler, SegmentCollector collector)
    {
        Battle = battle;
        Clock = clock;
        Scheduler = scheduler;
        SegmentCollector = collector;
    }
}