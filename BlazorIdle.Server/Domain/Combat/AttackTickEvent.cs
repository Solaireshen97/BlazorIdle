using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Procs;
using BlazorWebGame.Domain.Combat;
using System;

namespace BlazorIdle.Server.Domain.Combat;

public record AttackTickEvent(double ExecuteAt, TrackState Track) : IGameEvent
{
    public string EventType => "AttackTick";

    public void Execute(BattleContext context)
    {
        if (context.AutoCaster.IsCasting && context.AutoCaster.CastingSkillLocksAttack && ExecuteAt < context.AutoCaster.CastingUntil)
        {
            Track.NextTriggerAt = context.AutoCaster.CastingUntil;
            context.Scheduler.Schedule(new AttackTickEvent(Track.NextTriggerAt, Track));
            return;
        }

        const int baseDamage = 10;

        // 普攻暴击：使用面板基础（可被 BuffAggregate 叠加）
        var (chance, mult) = context.Crit.ResolveWith(
            context.Buffs.Aggregate,
            context.Stats.CritChance,
            context.Stats.CritMultiplier
        );
        bool isCrit = context.Rng.NextBool(chance);
        int finalDamage = isCrit ? (int)Math.Round(baseDamage * mult) : baseDamage;
        if (isCrit) context.SegmentCollector.OnTag("crit:basic_attack", 1);

        DamageCalculator.ApplyDamage(context, "basic_attack", finalDamage, DamageType.Physical);

        // Proc: OnHit/OnCrit（非 DoT），来源为普攻
        context.Procs.OnDirectHit(context, "basic_attack", DamageType.Physical, isCrit, isDot: false, DirectSourceKind.BasicAttack, context.Clock.CurrentTime);

        context.ProfessionModule.OnAttackTick(context, this);

        context.AutoCaster.TryAutoCast(context, ExecuteAt);

        Track.NextTriggerAt = ExecuteAt + Track.CurrentInterval;
        context.Scheduler.Schedule(new AttackTickEvent(Track.NextTriggerAt, Track));
    }
}