using BlazorIdle.Server.Domain.Combat.Combatants;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorWebGame.Domain.Combat;

namespace BlazorIdle.Server.Domain.Combat;

/// <summary>
/// 怪物攻击事件
/// Phase 4: 怪物攻击能力 - 怪物定期对玩家造成伤害
/// </summary>
public record EnemyAttackEvent(double ExecuteAt, EnemyCombatant Enemy) : IGameEvent
{
    public string EventType => "EnemyAttack";

    public void Execute(BattleContext context)
    {
        // 检查怪物是否还存活且可以行动
        if (!Enemy.CanAct())
        {
            return;
        }

        // 检查攻击轨道是否存在
        if (Enemy.AttackTrack == null)
        {
            return;
        }

        // 检查是否有可攻击的目标（玩家）
        if (!context.Player.CanBeTargeted())
        {
            // 玩家死亡，暂停怪物攻击（设置到很远的未来，不调度下次攻击）
            // 复活时会在 PlayerReviveEvent 中重新激活
            const double FAR_FUTURE = 1e10;
            Enemy.AttackTrack.NextTriggerAt = FAR_FUTURE;
            context.SegmentCollector.OnTag("enemy_attack_paused", 1);
            return;
        }

        // 计算伤害（基础值，Phase 4 保持简单，不考虑暴击/穿透）
        int damage = Enemy.Encounter.Enemy.BaseDamage;
        
        if (damage > 0)
        {
            // 对玩家造成伤害
            var actualDamage = context.Player.ReceiveDamage(
                damage, 
                Enemy.Encounter.Enemy.AttackDamageType, 
                ExecuteAt
            );

            // 记录统计
            context.SegmentCollector.OnTag("enemy_attack", 1);
            context.SegmentCollector.OnTag("damage_taken", actualDamage);
            
            // 检查玩家是否刚刚死亡
            if (context.Player.ShouldTriggerDeathEvent())
            {
                // 调度玩家死亡事件
                context.Scheduler.Schedule(new PlayerDeathEvent(ExecuteAt));
            }
        }

        // 调度下一次攻击
        Enemy.AttackTrack.NextTriggerAt = ExecuteAt + Enemy.AttackTrack.CurrentInterval;
        context.Scheduler.Schedule(new EnemyAttackEvent(Enemy.AttackTrack.NextTriggerAt, Enemy));
    }
}
