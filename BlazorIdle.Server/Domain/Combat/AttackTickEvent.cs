using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Resources;
using BlazorWebGame.Domain.Combat;

namespace BlazorIdle.Server.Domain.Combat;

public record AttackTickEvent(double ExecuteAt, TrackState Track) : IGameEvent
{
    public string EventType => "AttackTick";

    public void Execute(BattleContext context)
    {
        const int dmg = 10; // 之后可抽离成公式
        //context.SegmentCollector.OnDamage("basic_attack", dmg);
        // 暴击计算（使用全局 Crit 配置）
        bool isCrit = context.Rng.NextBool(context.Crit.Chance);
        int finalDamage = isCrit? (int)Math.Round(dmg * context.Crit.Multiplier): dmg;
        context.SegmentCollector.OnDamage("basic_attack", finalDamage);
        if (isCrit) context.SegmentCollector.OnTag("crit:basic_attack", 1);
        // 通过伤害计算器对目标结算（默认物理）
        DamageCalculator.ApplyDamage(context, "basic_attack", finalDamage, DamageType.Physical);

        // 职业钩子
        context.ProfessionModule.OnAttackTick(context, this);
        // 技能自动施放（放在资源生成之后，让刚刚获得的资源可用于技能）
        context.AutoCaster.TryAutoCast(context, ExecuteAt);

        // 调度下一次
        Track.NextTriggerAt = ExecuteAt + Track.CurrentInterval;
        context.Scheduler.Schedule(new AttackTickEvent(Track.NextTriggerAt, Track));
    }
}