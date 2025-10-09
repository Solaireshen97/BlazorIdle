using BlazorIdle.Server.Domain.Combat.Combatants;
using BlazorWebGame.Domain.Combat;

namespace BlazorIdle.Server.Domain.Combat;

/// <summary>
/// 玩家死亡事件：暂停所有玩家轨道、取消技能、调度复活事件
/// Phase 3: 玩家死亡与复活系统
/// Phase 4: 怪物攻击会通过 CanBeTargeted() 检测自动暂停
/// </summary>
public record PlayerDeathEvent(double ExecuteAt) : IGameEvent
{
    public string EventType => "PlayerDeath";

    public void Execute(BattleContext context)
    {
        var player = context.Player;
        
        // 确保玩家处于死亡状态，避免重复处理
        if (player.State != CombatantState.Dead || !player.DeathTime.HasValue)
            return;
        
        // 暂停所有玩家轨道：设置 NextTriggerAt 到极大值
        // 这样 AttackTickEvent 和 SpecialPulseEvent 不会在死亡期间触发
        // Phase 4: 怪物攻击事件会检测 Player.CanBeTargeted() 并自动暂停
        const double FAR_FUTURE = 1e10;
        foreach (var track in context.Tracks)
        {
            track.NextTriggerAt = FAR_FUTURE;
        }
        
        // 取消正在施放的技能
        if (context.AutoCaster.IsCasting)
        {
            context.AutoCaster.ClearCasting();
        }
        
        // 如果启用自动复活，调度复活事件
        if (player.AutoReviveEnabled && player.ReviveAt.HasValue)
        {
            context.Scheduler.Schedule(new PlayerReviveEvent(player.ReviveAt.Value));
        }
        
        // 记录死亡事件
        context.SegmentCollector.OnTag("player_death", 1);
    }
}
