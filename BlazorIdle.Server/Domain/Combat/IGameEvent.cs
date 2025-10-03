namespace BlazorWebGame.Domain.Combat;

/// <summary>
/// 战斗事件抽象：
/// 每个事件 = “在逻辑时间 ExecuteAt 发生一次可执行动作”
/// BattleRunner 通过 IEventScheduler 取出最早的事件 → 调用 Execute(context)。
/// 设计要点：
/// 1. 纯数据 + 行为接口（无状态本体，状态外置在 Battle / SegmentCollector 等）
/// 2. ExecuteAt 使用逻辑时间（double 秒）
/// 3. Execute 可再次调度后续事件（形成链）
/// 4. EventType 作为轻量分类/调试标签（便于统计/日志）
/// </summary>
public interface IGameEvent
{
    /// <summary>
    /// 该事件计划执行的逻辑时间（BattleRunner 会推进时钟至此再执行）
    /// </summary>
    double ExecuteAt { get; }

    /// <summary>
    /// 事件执行主体：
    /// - 读取上下文状态 (BattleContext)
    /// - 写入统计 (SegmentCollector.OnDamage / Buff 等)
    /// - 向调度器继续 Schedule 新事件（实现循环 / 连锁）
    /// 注意：不要在这里推进时钟；时间推进由 Runner 控制。
    /// </summary>
    void Execute(BattleContext context);

    /// <summary>
    /// 事件类型标签：用于日志 / 分析 / 前端展示（轻量分类）
    /// 建议统一常量或枚举映射，避免写错字符串。
    /// </summary>
    string EventType { get; }
}