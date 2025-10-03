using System;

namespace BlazorWebGame.Domain.Combat;

/// <summary>
/// 抽象：战斗模拟所使用的“逻辑时钟”接口。
/// 说明：与真实系统时间 (DateTime.UtcNow) 脱离，允许在内存中快速推进到未来任意时刻。
/// 用途：事件驱动模拟中按 ExecuteAt 精确跳跃，而不是按真实帧逐步累加。
/// </summary>
public interface IGameClock
{
    /// <summary>
    /// 当前逻辑时间（单位：秒，使用 double 存储，支持小数部分）
    /// 由 BattleRunner 驱动推进，事件只能读取。
    /// </summary>
    double CurrentTime { get; }

    /// <summary>
    /// 将逻辑时间前移到指定时间点（必须 >= 当前时间）。
    /// 典型调用：事件循环中取出下一个事件 → clock.AdvanceTo(ev.ExecuteAt)。
    /// </summary>
    void AdvanceTo(double targetTime);
}

/// <summary>
/// 具体实现：最简内存逻辑时钟（单调递增）。
/// 不做时间精度截断，不做浮点误差矫正；假设单线程调用。
/// </summary>
public class GameClock : IGameClock
{
    /// <inheritdoc />
    public double CurrentTime { get; private set; } = 0;

    /// <inheritdoc />
    public void AdvanceTo(double targetTime)
    {
        // 防倒退：保证模拟时间单调性（事件被调度到过去说明逻辑错误）
        if (targetTime < CurrentTime) throw new InvalidOperationException("Cannot go backwards");
        CurrentTime = targetTime;
    }
}