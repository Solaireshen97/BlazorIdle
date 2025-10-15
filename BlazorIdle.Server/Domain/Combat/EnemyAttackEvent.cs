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
            // Phase 8: 使用配置化的远未来时间戳
            var farFuture = context.CombatEngineOptions.FarFutureTimestamp;
            Enemy.AttackTrack.NextTriggerAt = farFuture;
            context.SegmentCollector.OnTag("enemy_attack_paused", 1);
            return;
        }

        // 发送敌人攻击开始事件（用于显示战斗日志）
        if (context.NotificationService?.IsAvailable == true && 
            context.MessageFormatter?.IsEnemyAttackStartedEnabled == true)
        {
            var attackerName = Enemy.Encounter.Enemy.Name;
            var targetName = context.MessageFormatter.GetPlayerName();
            var message = context.MessageFormatter.FormatEnemyAttackStarted(attackerName, targetName);
            
            var attackStartedEvent = new BlazorIdle.Shared.Models.AttackStartedEventDto
            {
                BattleId = context.Battle.Id,
                EventTime = ExecuteAt,
                EventType = "EnemyAttackStarted",
                AttackerName = attackerName,
                TargetName = targetName,
                Message = message
            };
            _ = context.NotificationService.NotifyEventAsync(context.Battle.Id, attackStartedEvent);
        }
        
        // 计算伤害（应用 Buff 加成）
        int baseDamage = Enemy.Encounter.Enemy.BaseDamage;
        var damageType = Enemy.Encounter.Enemy.AttackDamageType;
        int damage = Enemy.GetAttackDamage(baseDamage, damageType);
        
        if (damage > 0)
        {
            // 对玩家造成伤害
            // Phase 8: 传递配置的默认攻击者等级
            var actualDamage = context.Player.ReceiveDamage(
                damage, 
                damageType, 
                ExecuteAt,
                attackerLevel: null,  // TODO: 传递实际敌人等级
                defaultAttackerLevel: context.CombatEngineOptions.DefaultAttackerLevel
            );

            // 记录统计
            context.SegmentCollector.OnTag("enemy_attack", 1);
            context.SegmentCollector.OnTag("damage_taken", actualDamage);
            
            // 发送玩家受到伤害事件（用于显示战斗日志）
            if (context.NotificationService?.IsAvailable == true && 
                context.MessageFormatter?.IsDamageReceivedEnabled == true)
            {
                var attackerName = Enemy.Encounter.Enemy.Name;
                var targetName = context.MessageFormatter.GetPlayerName();
                var message = context.MessageFormatter.FormatDamageReceived(targetName, attackerName, actualDamage);
                
                var damageReceivedEvent = new BlazorIdle.Shared.Models.DamageReceivedEventDto
                {
                    BattleId = context.Battle.Id,
                    EventTime = ExecuteAt,
                    EventType = "DamageReceived",
                    AttackerName = attackerName,
                    TargetName = targetName,
                    Damage = actualDamage,
                    TargetCurrentHp = context.Player.CurrentHp,
                    TargetMaxHp = context.Player.MaxHp,
                    Message = message
                };
                _ = context.NotificationService.NotifyEventAsync(context.Battle.Id, damageReceivedEvent);
            }
            
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
