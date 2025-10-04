using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorWebGame.Domain.Combat;
using System;

namespace BlazorIdle.Server.Domain.Combat;

public record AttackTickEvent(double ExecuteAt, TrackState Track) : IGameEvent
{
    public string EventType => "AttackTick";

    public void Execute(BattleContext context)
    {
        const int baseDamage = 10;

        // 解析最终暴击参数（结合 Buff 与全局 Crit）
        var (chance, mult) = context.Crit.ResolveWith(context.Buffs.Aggregate);
        bool isCrit = context.Rng.NextBool(chance);
        int finalDamage = isCrit ? (int)Math.Round(baseDamage * mult) : baseDamage;
        if (isCrit) context.SegmentCollector.OnTag("crit:basic_attack", 1);

        // 通过伤害计算器（物理）
        DamageCalculator.ApplyDamage(context, "basic_attack", finalDamage, Damage.DamageType.Physical);

        // 职业钩子（资源/标签）
        context.ProfessionModule.OnAttackTick(context, this);

        // 尝试自动放技能
        context.AutoCaster.TryAutoCast(context, ExecuteAt);

        Track.NextTriggerAt = ExecuteAt + Track.CurrentInterval;
        context.Scheduler.Schedule(new AttackTickEvent(Track.NextTriggerAt, Track));
    }
}