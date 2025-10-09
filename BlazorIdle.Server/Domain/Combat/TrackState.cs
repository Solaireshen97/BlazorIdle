namespace BlazorIdle.Server.Domain.Combat;

public class TrackState
{
    public TrackType TrackType { get; }
    public double BaseInterval { get; private set; }
    public double HasteFactor { get; private set; } = 1.0; // 最终间隔 = BaseInterval / HasteFactor
    public double NextTriggerAt { get; set; }
    
    // Phase 4: 暂停/恢复支持
    private double? _pausedAt;
    private double _pausedRemaining;

    public TrackState(TrackType trackType, double baseInterval, double startAt = 0)
    {
        TrackType = trackType;
        BaseInterval = baseInterval;
        NextTriggerAt = startAt;
    }

    public double CurrentInterval => BaseInterval / HasteFactor;

    public void SetHaste(double factor)
    {
        if (factor <= 0) factor = 0.0001;
        HasteFactor = factor;
    }
    
    /// <summary>
    /// 暂停轨道（记录剩余时间）
    /// Phase 4: 用于怪物攻击在玩家死亡时暂停
    /// </summary>
    public void Pause(double now)
    {
        if (_pausedAt.HasValue) return; // 已暂停
        
        _pausedRemaining = NextTriggerAt - now;
        _pausedAt = now;
        NextTriggerAt = double.MaxValue; // 永不触发
    }
    
    /// <summary>
    /// 恢复轨道（从暂停的剩余时间继续）
    /// Phase 4: 用于怪物攻击在玩家复活时恢复
    /// </summary>
    public void Resume(double now)
    {
        if (!_pausedAt.HasValue) return; // 未暂停
        
        NextTriggerAt = now + _pausedRemaining;
        _pausedAt = null;
    }
}