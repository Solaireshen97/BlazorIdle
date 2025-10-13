using BlazorIdle.Server.Domain.Combat.Combatants;
using BlazorWebGame.Domain.Combat;

namespace BlazorIdle.Server.Domain.Combat;

/// <summary>
/// 玩家复活事件：恢复满血、恢复存活状态、恢复轨道
/// Phase 3: 玩家死亡与复活系统
/// Phase 4: 怪物攻击会通过 CanBeTargeted() 检测自动恢复
/// </summary>
public record PlayerReviveEvent(double ExecuteAt) : IGameEvent
{
    public string EventType => "PlayerRevive";

    public void Execute(BattleContext context)
    {
        var player = context.Player;
        
        // 执行复活
        player.Revive(ExecuteAt);
        
        // 恢复所有玩家轨道：重新开始攻击循环
        // 设置 NextTriggerAt 为当前时间 + 完整间隔
        foreach (var track in context.Tracks)
        {
            track.NextTriggerAt = ExecuteAt + track.CurrentInterval;
            
            // 重新调度对应的事件
            if (track.TrackType == TrackType.Attack)
            {
                context.Scheduler.Schedule(new AttackTickEvent(track.NextTriggerAt, track));
            }
            else if (track.TrackType == TrackType.Special)
            {
                context.Scheduler.Schedule(new SpecialPulseEvent(track.NextTriggerAt, track));
            }
        }
        
        // Phase 4: 恢复所有怪物攻击轨道
        foreach (var enemy in context.EnemyCombatants)
        {
            if (enemy.AttackTrack != null && enemy.CanAct())
            {
                // 重新激活怪物攻击：设置下次攻击时间为当前时间 + 攻击间隔
                enemy.AttackTrack.NextTriggerAt = ExecuteAt + enemy.AttackTrack.CurrentInterval;
                context.Scheduler.Schedule(new EnemyAttackEvent(enemy.AttackTrack.NextTriggerAt, enemy));
                context.SegmentCollector.OnTag("enemy_attack_resumed", 1);
            }
        }
        
        // 记录复活事件
        context.SegmentCollector.OnTag("player_revive", 1);
        
        // SignalR Phase 2: 发送玩家复活通知
        context.NotificationService?.NotifyStateChangeAsync(context.Battle.Id, "PlayerRevive");
    }
}
