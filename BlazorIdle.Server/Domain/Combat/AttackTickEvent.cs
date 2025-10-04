using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorWebGame.Domain.Combat;
using System;

namespace BlazorIdle.Server.Domain.Combat;

public record AttackTickEvent(double ExecuteAt, TrackState Track) : IGameEvent
{
    public string EventType => "AttackTick";

    public void Execute(BattleContext context)
    {
        // 模式B：施法期间暂停普攻（仅当当前施法要求锁定普攻）
        if (context.AutoCaster.IsCasting && context.AutoCaster.CastingSkillLocksAttack && ExecuteAt < context.AutoCaster.CastingUntil)
        {
            Track.NextTriggerAt = context.AutoCaster.CastingUntil;
            context.Scheduler.Schedule(new AttackTickEvent(Track.NextTriggerAt, Track));
            return;
        }

        // 普攻伤害 + 暴击
        const int baseDamage = 10;
        var (chance, mult) = context.Crit.ResolveWith(context.Buffs.Aggregate);
        bool isCrit = context.Rng.NextBool(chance);
        int finalDamage = isCrit ? (int)Math.Round(baseDamage * mult) : baseDamage;
        if (isCrit) context.SegmentCollector.OnTag("crit:basic_attack", 1);

        DamageCalculator.ApplyDamage(context, "basic_attack", finalDamage, DamageType.Physical);

        // 职业钩子（资源/标签等）
        context.ProfessionModule.OnAttackTick(context, this);

        // 技能自动施放（放在资源生成之后）
        context.AutoCaster.TryAutoCast(context, ExecuteAt);

        // 调度下一次攻击
        Track.NextTriggerAt = ExecuteAt + Track.CurrentInterval;
        context.Scheduler.Schedule(new AttackTickEvent(Track.NextTriggerAt, Track));
    }
}