using BlazorIdle.Server.Domain.Combat.Combatants;
using BlazorWebGame.Domain.Combat;

namespace BlazorIdle.Server.Domain.Combat;

/// <summary>
/// 玩家复活事件
/// Phase 3: 处理玩家复活逻辑
/// - 恢复满血
/// - 状态转换 Dead → Alive
/// - 恢复 Track 到当前时间（重新开始攻击循环）
/// </summary>
public record PlayerReviveEvent(double ExecuteAt, PlayerCombatant Player) : IGameEvent
{
    public string EventType => "PlayerRevive";

    public void Execute(BattleContext context)
    {
        // 复活玩家
        Player.OnRevive(ExecuteAt);

        // 记录复活事件
        context.SegmentCollector.OnTag("player_revive", 1);

        // 恢复所有 Track 到当前时间（重新开始攻击循环）
        foreach (var track in context.Tracks)
        {
            track.NextTriggerAt = ExecuteAt;
        }

        // 立即调度下一个攻击事件（根据 Track 类型）
        foreach (var track in context.Tracks)
        {
            if (track.CurrentInterval > 0)
            {
                // 根据 track 类型调度相应的事件
                // 假设第一个 track 是攻击轨道，第二个是特殊轨道
                var nextTrigger = ExecuteAt + track.CurrentInterval;
                track.NextTriggerAt = nextTrigger;
                
                // 这里需要根据 track 的实际类型来调度事件
                // 由于 TrackState 没有类型标识，我们通过 context.Tracks 的顺序来判断
                var trackIndex = context.Tracks.IndexOf(track);
                if (trackIndex == 0)
                {
                    // 攻击轨道
                    context.Scheduler.Schedule(new AttackTickEvent(nextTrigger, track));
                }
                else if (trackIndex == 1)
                {
                    // 特殊轨道
                    context.Scheduler.Schedule(new SpecialPulseEvent(nextTrigger, track));
                }
            }
        }
    }
}
