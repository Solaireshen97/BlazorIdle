using System;

namespace BlazorWebGame.Domain.Combat;

public interface IGameClock
{
    double CurrentTime { get; } // 秒（或毫秒 -> double）
    void AdvanceTo(double targetTime);
}

public class GameClock : IGameClock
{
    public double CurrentTime { get; private set; } = 0;
    public void AdvanceTo(double targetTime)
    {
        if (targetTime < CurrentTime) throw new InvalidOperationException("Cannot go backwards");
        CurrentTime = targetTime;
    }
}