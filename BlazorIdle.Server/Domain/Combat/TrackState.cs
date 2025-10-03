namespace BlazorIdle.Server.Domain.Combat;

public class TrackState
{
    public TrackType TrackType { get; }
    public double BaseInterval { get; private set; }
    public double HasteFactor { get; private set; } = 1.0; // 最终间隔 = BaseInterval / HasteFactor
    public double NextTriggerAt { get; set; }

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
}