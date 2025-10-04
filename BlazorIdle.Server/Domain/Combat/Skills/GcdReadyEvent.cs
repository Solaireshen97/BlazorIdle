using BlazorWebGame.Domain.Combat;

namespace BlazorIdle.Server.Domain.Combat.Skills;

public record GcdReadyEvent(double ExecuteAt) : IGameEvent
{
    public string EventType => "GcdReady";

    public void Execute(BattleContext context)
    {
        // 优先尝试释放队列；若没有或失败，走常规自动施放
        if (!context.AutoCaster.TryAutoCast(context, ExecuteAt))
        {
            // 保底：不做额外处理；下一个事件（普攻/脉冲/施法完成）也会再次尝试
        }
    }
}