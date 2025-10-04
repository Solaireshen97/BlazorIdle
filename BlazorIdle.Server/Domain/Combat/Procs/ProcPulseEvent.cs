using BlazorWebGame.Domain.Combat;

namespace BlazorIdle.Server.Domain.Combat.Procs;

public record ProcPulseEvent(double ExecuteAt, double IntervalSeconds) : IGameEvent
{
    public string EventType => "ProcPulse";

    public void Execute(BattleContext context)
    {
        context.Procs.EvaluateRppm(context, ExecuteAt, IntervalSeconds);
        // 自我续订
        context.Scheduler.Schedule(new ProcPulseEvent(ExecuteAt + IntervalSeconds, IntervalSeconds));
    }
}