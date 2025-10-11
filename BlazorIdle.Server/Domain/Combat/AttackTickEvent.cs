using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Procs;
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

        // 基础攻击伤害 = 基础值 + 攻击强度（装备影响）
        const int baseAttackDamage = 10;
        double preCritDamage = baseAttackDamage + context.Stats.AttackPower;

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
            DamageCalculator.ApplyDamageToTarget(context, enemyTarget.Encounter, "basic_attack", finalDamage, DamageType.Physical);
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

        Track.NextTriggerAt = ExecuteAt + Track.CurrentInterval;
        context.Scheduler.Schedule(new AttackTickEvent(Track.NextTriggerAt, Track));
    }
}