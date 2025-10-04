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
        // 施法期间暂停普攻
        if (context.AutoCaster.IsCasting && context.AutoCaster.CastingSkillLocksAttack && ExecuteAt < context.AutoCaster.CastingUntil)
        {
            Track.NextTriggerAt = context.AutoCaster.CastingUntil;
            context.Scheduler.Schedule(new AttackTickEvent(Track.NextTriggerAt, Track));
            return;
        }

        const int baseDamage = 10;

        var (chance, mult) = context.Crit.ResolveWith(context.Buffs.Aggregate);
        bool isCrit = context.Rng.NextBool(chance);
        int finalDamage = isCrit ? (int)Math.Round(baseDamage * mult) : baseDamage;
        if (isCrit) context.SegmentCollector.OnTag("crit:basic_attack", 1);

        // 伤害
        DamageCalculator.ApplyDamage(context, "basic_attack", finalDamage, DamageType.Physical);

        // Proc: OnHit/OnCrit（非 DoT），来源为普攻
        context.Procs.OnDirectHit(context, "basic_attack", DamageType.Physical, isCrit, isDot: false, DirectSourceKind.BasicAttack, context.Clock.CurrentTime);

        // 职业钩子/资源
        context.ProfessionModule.OnAttackTick(context, this);

        // 自动施放技能
        context.AutoCaster.TryAutoCast(context, ExecuteAt);

        Track.NextTriggerAt = ExecuteAt + Track.CurrentInterval;
        context.Scheduler.Schedule(new AttackTickEvent(Track.NextTriggerAt, Track));
    }
}