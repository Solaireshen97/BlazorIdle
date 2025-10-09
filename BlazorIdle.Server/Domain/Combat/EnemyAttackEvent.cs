using BlazorIdle.Server.Domain.Combat.Combatants;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorWebGame.Domain.Combat;

namespace BlazorIdle.Server.Domain.Combat;

/// <summary>
/// 怪物攻击事件：类似 AttackTickEvent，但作用于怪物攻击玩家
/// Phase 4: 怪物攻击能力
/// </summary>
public record EnemyAttackEvent(double ExecuteAt, EnemyCombatant Enemy, TrackState Track) : IGameEvent
{
    public string EventType => "EnemyAttack";

    public void Execute(BattleContext context)
    {
        // 检查怪物是否存活
        if (!Enemy.CanAct() || Enemy.IsDead)
        {
            // 怪物已死亡，不再调度下一次攻击
            return;
        }

        // 检查玩家是否存活（可被攻击）
        if (!context.Player.CanBeTargeted())
        {
            // 玩家死亡，暂停怪物攻击（不调度下一次）
            // 玩家复活时会在 PlayerReviveEvent 中恢复怪物攻击
            return;
        }

        // 计算伤害（基础值，不考虑暴击/穿透，保持简单）
        int damage = Enemy.Encounter.Enemy.BaseDamage;
        if (damage <= 0)
        {
            // 如果怪物没有配置攻击伤害，跳过
            Track.NextTriggerAt = ExecuteAt + Track.CurrentInterval;
            context.Scheduler.Schedule(new EnemyAttackEvent(Track.NextTriggerAt, Enemy, Track));
            return;
        }

        // 对玩家造成伤害
        var actualDamage = context.Player.ReceiveDamage(damage, Enemy.Encounter.Enemy.AttackDamageType, ExecuteAt);
        
        // 记录怪物攻击事件
        context.SegmentCollector.OnTag($"enemy_attack:{Enemy.Encounter.Enemy.Id}", 1);
        
        // 如果玩家刚死亡，调度死亡事件
        if (context.Player.ShouldTriggerDeathEvent())
        {
            context.Scheduler.Schedule(new PlayerDeathEvent(ExecuteAt));
            // 不继续调度下一次怪物攻击，等待玩家复活时恢复
            return;
        }

        // 调度下一次攻击
        Track.NextTriggerAt = ExecuteAt + Track.CurrentInterval;
        context.Scheduler.Schedule(new EnemyAttackEvent(Track.NextTriggerAt, Enemy, Track));
    }
}
