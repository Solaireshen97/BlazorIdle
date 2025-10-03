using System.Collections.Generic;
using BlazorWebGame.Domain.Combat;   // 注意：命名空间前缀与当前项目(BlazorIdle)不一致，后续可统一

namespace BlazorWebGame.Application.Battles;

/// <summary>
/// BattleRunner = “同步模拟器”
/// 作用：给定一个 Battle（包含攻击节奏等数据），在内存中快速推进“事件时间线”直到达到指定的模拟时长，
/// 期间收集 CombatSegment（分段统计），最后返回所有段结果供持久化或返回给 API。
/// 关键特征：
///   * 使用“事件驱动 + 优先队列”模型 (EventScheduler)
///   * 用 GameClock 控制逻辑时间（与真实墙钟脱离）
///   * SegmentCollector 负责把离散事件聚合成可展示的统计段
///   * AttackTickEvent 是首个驱动事件，后续它本身或其它事件会继续调度新的事件
/// </summary>
public class BattleRunner
{
    /// <summary>
    /// 核心入口：执行一场战斗的“整段离线模拟”，返回分段统计列表
    /// </summary>
    /// <param name="battle">战斗领域对象（含 CharacterId、攻击间隔等）</param>
    /// <param name="durationSeconds">模拟的逻辑时间上限（到达后提前终止）</param>
    /// <returns>按时间顺序生成的 CombatSegment 列表</returns>
    public IReadOnlyList<CombatSegment> RunForDuration(Battle battle, double durationSeconds)
    {
        // 1. 构造模拟所需的运行时组件（这些对象彼此协作组成一个最小的“战斗执行上下文”）
        var clock = new GameClock();                // 管理当前逻辑时间（双精度秒）
        var scheduler = new EventScheduler();       // 事件调度器：内部一般是一个按执行时间排序的最小堆 / 优先队列
        var collector = new SegmentCollector();     // 用来分批聚合事件统计（例如每 X 秒或按事件数量打包）
        var context = new BattleContext(battle, clock, scheduler, collector); // 传给事件执行的上下文

        // 2. 预热：压入第一条事件（驱动后续链式调度）
        scheduler.Schedule(new AttackTickEvent(0));

        // 3. 结果容器
        var segments = new List<CombatSegment>();
        var endTarget = durationSeconds;            // 结束阈值（逻辑时间）

        // 4. 主循环：直到没有事件可执行
        while (scheduler.Count > 0)
        {
            var ev = scheduler.PopNext();           // 取出最早需要执行的事件
            if (ev == null) break;                  // 空安全（理论上不会出现）

            if (ev.ExecuteAt > endTarget)
            {
                // 若下一个事件的执行时间已经超出模拟窗口 → 提前结束
                battle.Finish(clock.CurrentTime);   // 标记战斗完成（结束时间 = 当前时钟）
                break;
            }

            // 4.1 推进逻辑时间到事件执行点
            clock.AdvanceTo(ev.ExecuteAt);

            // 4.2 执行事件（事件内部会可能：计算伤害、调度下一次 AttackTickEvent 等）
            ev.Execute(context);

            // 4.3 通知收集器时间推进（让它内部做窗口判断：是否到达 flush 条件）
            collector.Tick(clock.CurrentTime);

            // 4.4 如果达到“需要输出一个段”的条件 → 刷入结果
            if (collector.ShouldFlush(clock.CurrentTime))
            {
                segments.Add(collector.Flush(clock.CurrentTime));
            }
        }

        // 5. 循环结束后，如果还有尚未刷新的统计（尾段），再刷一次
        if (collector.EventCount > 0)
        {
            segments.Add(collector.Flush(clock.CurrentTime));
        }

        // 6. 返回只读视图（当前 List implements IReadOnlyList，可直接返回）
        return segments;
    }
}