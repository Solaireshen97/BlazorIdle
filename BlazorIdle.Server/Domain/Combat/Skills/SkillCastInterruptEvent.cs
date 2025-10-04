using BlazorWebGame.Domain.Combat;

namespace BlazorIdle.Server.Domain.Combat.Skills;

public record SkillCastInterruptEvent(double ExecuteAt, long CastId, InterruptReason Reason) : IGameEvent
{
    public string EventType => "SkillCastInterrupt";

    public void Execute(BattleContext context)
    {
        // 确认仍是同一段施法（防止误中断）
        if (!context.AutoCaster.IsCasting || context.AutoCaster.CurrentCastId is null || context.AutoCaster.CurrentCastId != CastId)
            return;

        context.AutoCaster.InterruptCasting(context, ExecuteAt, Reason);
    }
}