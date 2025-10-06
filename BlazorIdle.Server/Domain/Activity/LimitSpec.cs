namespace BlazorIdle.Server.Domain.Activity;

/// <summary>
/// 活动限制规格基类
/// </summary>
public abstract class LimitSpec
{
    /// <summary>
    /// 检查是否已达到限制
    /// </summary>
    public abstract bool IsReached(ActivityProgress progress);
    
    /// <summary>
    /// 获取限制类型名称
    /// </summary>
    public abstract string GetLimitType();
}

/// <summary>
/// 时长限制：按模拟时间（秒）
/// </summary>
public sealed class DurationLimit : LimitSpec
{
    public double DurationSeconds { get; init; }
    
    public DurationLimit(double seconds)
    {
        DurationSeconds = Math.Max(0, seconds);
    }
    
    public override bool IsReached(ActivityProgress progress)
    {
        return progress.SimulatedSeconds >= DurationSeconds;
    }
    
    public override string GetLimitType() => "Duration";
}

/// <summary>
/// 计数限制：按击杀数、采集次数等
/// </summary>
public sealed class CountLimit : LimitSpec
{
    public int TargetCount { get; init; }
    
    public CountLimit(int count)
    {
        TargetCount = Math.Max(1, count);
    }
    
    public override bool IsReached(ActivityProgress progress)
    {
        return progress.CompletedCount >= TargetCount;
    }
    
    public override string GetLimitType() => "Count";
}

/// <summary>
/// 无限制：需要手动停止
/// </summary>
public sealed class InfiniteLimit : LimitSpec
{
    public override bool IsReached(ActivityProgress progress)
    {
        return false; // 永远不会自动达到限制
    }
    
    public override string GetLimitType() => "Infinite";
}

/// <summary>
/// 活动进度追踪
/// </summary>
public sealed class ActivityProgress
{
    /// <summary>已模拟的秒数</summary>
    public double SimulatedSeconds { get; set; }
    
    /// <summary>已完成的计数（击杀数、采集次数等）</summary>
    public int CompletedCount { get; set; }
    
    /// <summary>其他自定义数据（JSON序列化）</summary>
    public Dictionary<string, object> CustomData { get; init; } = new();
}
