using BlazorIdle.Server.Domain.Combat.Combatants;
using BlazorWebGame.Domain.Combat;

namespace BlazorIdle.Server.Domain.Combat;

/// <summary>
/// 玩家死亡事件
/// Phase 3: 处理玩家死亡逻辑
/// - 暂停玩家所有 Track（设置极大的 NextTriggerAt）
/// - 取消玩家技能施放
/// - 调度 PlayerReviveEvent（如果允许复活）
/// </summary>
public record PlayerDeathEvent(double ExecuteAt, PlayerCombatant Player) : IGameEvent
{
    public string EventType => "PlayerDeath";

    public void Execute(BattleContext context)
    {
        // 记录死亡事件
        context.SegmentCollector.OnTag("player_death", 1);

        // 暂停所有玩家 Track（攻击轨道和特殊轨道）
        const double FAR_FUTURE = 1e9;
        foreach (var track in context.Tracks)
        {
            track.NextTriggerAt = FAR_FUTURE;
        }

        // 取消正在施放的技能
        if (context.AutoCaster.IsCasting)
        {
            context.AutoCaster.RequestInterrupt(context, ExecuteAt, Skills.InterruptReason.Other);
        }

        // 调度复活事件（如果允许自动复活）
        if (Player.AutoReviveEnabled && Player.ReviveAt.HasValue)
        {
            context.Scheduler.Schedule(new PlayerReviveEvent(Player.ReviveAt.Value, Player));
        }
    }
}
