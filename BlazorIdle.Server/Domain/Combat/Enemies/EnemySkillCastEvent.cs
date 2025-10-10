using BlazorIdle.Server.Domain.Combat.Combatants;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorWebGame.Domain.Combat;

namespace BlazorIdle.Server.Domain.Combat.Enemies;

/// <summary>
/// 怪物技能释放事件
/// Phase 5: 怪物技能系统
/// </summary>
public record EnemySkillCastEvent(double ExecuteAt, EnemyCombatant Caster, EnemySkillSlot Skill) : IGameEvent
{
    public string EventType => "EnemySkillCast";

    public void Execute(BattleContext context)
    {
        // 检查施法者是否还存活
        if (!Caster.CanAct())
        {
            return;
        }

        // 再次检查技能是否冷却完毕（防止事件调度后状态改变）
        if (!Skill.IsReady(ExecuteAt))
        {
            return;
        }

        var def = Skill.Definition;

        // 执行技能效果
        switch (def.Effect)
        {
            case SkillEffectType.Damage:
                ExecuteDamage(context, def);
                break;

            case SkillEffectType.Heal:
                ExecuteHeal(context, def);
                break;

            case SkillEffectType.ApplyBuff:
                ExecuteApplyBuff(context, def);
                break;

            case SkillEffectType.Summon:
                // 未来扩展：召唤
                break;
        }

        // 消耗技能（设置冷却）
        Skill.Consume(ExecuteAt);

        // 记录统计
        context.SegmentCollector.OnTag($"enemy_skill_cast:{def.Id}", 1);
    }

    /// <summary>
    /// 执行伤害效果
    /// </summary>
    private void ExecuteDamage(BattleContext context, EnemySkillDefinition def)
    {
        int damage = def.EffectValue;
        
        if (damage <= 0)
        {
            return;
        }

        if (def.MaxTargets == 1)
        {
            // 单体伤害：直接对玩家造成伤害
            if (context.Player.CanBeTargeted())
            {
                var actualDamage = context.Player.ReceiveDamage(damage, def.DamageType, ExecuteAt);
                context.SegmentCollector.OnTag($"enemy_skill_damage:{def.Id}", actualDamage);
                
                // 检查玩家是否刚刚死亡
                if (context.Player.ShouldTriggerDeathEvent())
                {
                    context.Scheduler.Schedule(new PlayerDeathEvent(ExecuteAt));
                }
            }
        }
        else
        {
            // 多目标伤害：未来扩展（当前只有单个玩家）
            // 如果将来支持多玩家，可以在这里选择多个目标
            if (context.Player.CanBeTargeted())
            {
                var actualDamage = context.Player.ReceiveDamage(damage, def.DamageType, ExecuteAt);
                context.SegmentCollector.OnTag($"enemy_skill_damage:{def.Id}", actualDamage);
                
                if (context.Player.ShouldTriggerDeathEvent())
                {
                    context.Scheduler.Schedule(new PlayerDeathEvent(ExecuteAt));
                }
            }
        }
    }

    /// <summary>
    /// 执行治疗效果
    /// </summary>
    private void ExecuteHeal(BattleContext context, EnemySkillDefinition def)
    {
        int healAmount = def.EffectValue;
        
        if (healAmount <= 0)
        {
            return;
        }

        // 治疗自己
        var encounter = Caster.Encounter;
        if (!encounter.IsDead)
        {
            int oldHp = encounter.CurrentHp;
            int newHp = System.Math.Min(encounter.CurrentHp + healAmount, encounter.Enemy.MaxHp);
            int actualHeal = newHp - oldHp;
            
            // 直接修改 Encounter 的 CurrentHp（通过反射或直接访问）
            // 注意：Encounter.CurrentHp 是 get-only 属性，需要通过其他方式修改
            // 这里暂时记录统计，实际治疗效果需要 Encounter 提供修改接口
            if (actualHeal > 0)
            {
                context.SegmentCollector.OnTag($"enemy_skill_heal:{def.Id}", actualHeal);
            }
        }
    }

    /// <summary>
    /// 执行施加 Buff 效果
    /// </summary>
    private void ExecuteApplyBuff(BattleContext context, EnemySkillDefinition def)
    {
        // 未来扩展：需要怪物 Buff 系统支持
        // 当前记录统计
        if (!string.IsNullOrEmpty(def.BuffId))
        {
            context.SegmentCollector.OnTag($"enemy_skill_buff:{def.BuffId}", 1);
        }
    }
}
