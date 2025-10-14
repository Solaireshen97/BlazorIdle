using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Procs;
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorWebGame.Domain.Combat;
using System;
using System.Linq;

namespace BlazorIdle.Server.Domain.Combat;

public record AttackTickEvent(double ExecuteAt, TrackState Track) : IGameEvent
{
    public string EventType => "AttackTick";

    public void Execute(BattleContext context)
    {
        // Phase 3: 检查玩家是否可以行动
        if (!context.Player.CanAct())
        {
            // 玩家死亡时不执行攻击，等待复活
            return;
        }
        
        // 检查攻击进度是否被重置（如切换目标或等待刷新）
        // 如果 Track.NextTriggerAt 大于当前事件的 ExecuteAt，说明进度已被重置，跳过执行并调度新事件
        if (Track.NextTriggerAt > ExecuteAt + 1e-9)
        {
            context.Scheduler.Schedule(new AttackTickEvent(Track.NextTriggerAt, Track));
            return;
        }

        if (context.AutoCaster.IsCasting && context.AutoCaster.CastingSkillLocksAttack && ExecuteAt < context.AutoCaster.CastingUntil)
        {
            Track.NextTriggerAt = context.AutoCaster.CastingUntil;
            context.Scheduler.Schedule(new AttackTickEvent(Track.NextTriggerAt, Track));
            return;
        }

        // Phase 2: 使用 TargetSelector 选择目标（如果有 EncounterGroup）
        Combatants.ICombatant? target = null;
        if (context.EncounterGroup != null)
        {
            // 将 EncounterGroup.All 包装为 ICombatant 列表
            var candidates = context.EncounterGroup.All
                .Select((enc, idx) => new Combatants.EnemyCombatant($"enemy_{idx}", enc))
                .ToList<Combatants.ICombatant>();
            
            target = context.TargetSelector.SelectTarget(candidates);
        }
        
        // 如果没有可选目标，跳过本次攻击
        if (target == null && context.Encounter == null)
        {
            Track.NextTriggerAt = ExecuteAt + Track.CurrentInterval;
            context.Scheduler.Schedule(new AttackTickEvent(Track.NextTriggerAt, Track));
            return;
        }
        
        // 战斗循环优化 Task 1.3: 设置当前攻击目标，供技能使用
        // 这确保攻击和技能在同一个攻击周期内使用同一个目标
        context.CurrentAttackTarget = target;

        // Phase 5: 基础攻击伤害计算（简化版，不访问数据库）
        // 在实际战斗中，武器信息已经通过 Stats.AttackPower 体现
        // 这里保持简单的计算逻辑以保证性能
        const int baseAttackDamage = 10;
        double preCritDamage = baseAttackDamage + context.Stats.AttackPower;
        
        // 注意：完整的双持武器伤害计算需要访问数据库获取武器类型
        // 在实时战斗循环中不适合进行数据库查询
        // 建议在构建 BattleContext 时预先计算武器相关的伤害加成并存入 Stats
        // 当前保持原有逻辑，weapon-specific multipliers 已通过装备属性反映在 AttackPower 中

        // 普攻暴击：使用面板基础（可被 BuffAggregate 叠加）
        var (chance, mult) = context.Crit.ResolveWith(
            context.Buffs.Aggregate,
            context.Stats.CritChance,
            context.Stats.CritMultiplier
        );
        bool isCrit = context.Rng.NextBool(chance);
        int finalDamage = isCrit ? (int)Math.Round(preCritDamage * mult) : (int)Math.Round(preCritDamage);
        if (isCrit) context.SegmentCollector.OnTag("crit:basic_attack", 1);

        // Phase 2: 对选中的目标应用伤害
        if (target is Combatants.EnemyCombatant enemyTarget)
        {
            DamageCalculator.ApplyDamageToTarget(context, enemyTarget.Encounter, "basic_attack", finalDamage, DamageType.Physical, isCrit);
        }
        else
        {
            // 向后兼容：使用旧的 ApplyDamage 方法
            DamageCalculator.ApplyDamage(context, "basic_attack", finalDamage, DamageType.Physical);
        }

        // Proc: OnHit/OnCrit（非 DoT），来源为普攻
        context.Procs.OnDirectHit(context, "basic_attack", DamageType.Physical, isCrit, isDot: false, DirectSourceKind.BasicAttack, context.Clock.CurrentTime);

        context.ProfessionModule.OnAttackTick(context, this);

        context.AutoCaster.TryAutoCast(context, ExecuteAt);
        
        // 战斗循环优化 Task 1.3: 清除当前攻击目标，避免状态泄漏
        context.CurrentAttackTarget = null;

        Track.NextTriggerAt = ExecuteAt + Track.CurrentInterval;
        context.Scheduler.Schedule(new AttackTickEvent(Track.NextTriggerAt, Track));
        
        // SignalR: 发送攻击触发轻量事件通知（用于前端进度条增量更新）
        if (context.NotificationService?.IsAvailable == true)
        {
            var eventDto = new BlazorIdle.Shared.Models.AttackTickEventDto
            {
                BattleId = context.Battle.Id,
                EventTime = ExecuteAt,
                EventType = "AttackTick",
                NextTriggerAt = Track.NextTriggerAt,
                Interval = Track.CurrentInterval
            };
            _ = context.NotificationService.NotifyEventAsync(context.Battle.Id, eventDto);
        }
        
        // 发送攻击开始事件（用于显示战斗日志）
        if (context.NotificationService?.IsAvailable == true && 
            context.MessageFormatter?.IsAttackStartedEnabled == true &&
            target != null)
        {
            var attackerName = context.MessageFormatter.GetPlayerName();
            var targetName = target is Combatants.EnemyCombatant ec ? ec.Encounter.Enemy.Name : "敌人";
            var message = context.MessageFormatter.FormatAttackStarted(attackerName, targetName);
            
            var attackStartedEvent = new BlazorIdle.Shared.Models.AttackStartedEventDto
            {
                BattleId = context.Battle.Id,
                EventTime = ExecuteAt,
                EventType = "AttackStarted",
                AttackerName = attackerName,
                TargetName = targetName,
                Message = message
            };
            _ = context.NotificationService.NotifyEventAsync(context.Battle.Id, attackStartedEvent);
        }
    }
}