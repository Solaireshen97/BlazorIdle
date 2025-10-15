using BlazorIdle.Server.Domain.Combat.Combatants;
using BlazorWebGame.Domain.Combat;
using System.Linq;

namespace BlazorIdle.Server.Domain.Combat.Enemies;

/// <summary>
/// 怪物技能释放事件
/// Phase 5: 怪物技能系统
/// </summary>
public record EnemySkillCastEvent(
    double ExecuteAt,
    EnemyCombatant Caster,
    EnemySkillSlot SkillSlot) : IGameEvent
{
    public string EventType => "EnemySkillCast";

    public void Execute(BattleContext context)
    {
        // 检查施法者是否还存活且可以行动
        if (!Caster.CanAct())
        {
            return;
        }

        var skill = SkillSlot.Definition;
        
        // 根据技能效果类型执行不同的逻辑
        switch (skill.Effect)
        {
            case SkillEffectType.Damage:
                ExecuteDamage(context, skill);
                break;
                
            case SkillEffectType.ApplyBuff:
                ExecuteApplyBuff(context, skill);
                break;
                
            case SkillEffectType.Heal:
                ExecuteHeal(context, skill);
                break;
                
            case SkillEffectType.Summon:
                // 未来扩展
                break;
        }
        
        // 标记技能已使用，进入冷却
        SkillSlot.MarkUsed(ExecuteAt);
        
        // 记录统计
        context.SegmentCollector.OnTag($"enemy_skill_cast:{skill.Id}", 1);
    }

    /// <summary>
    /// 执行伤害效果
    /// </summary>
    private void ExecuteDamage(BattleContext context, EnemySkillDefinition skill)
    {
        // 选择目标（对玩家造成伤害）
        var targets = SelectTargets(context, skill.MaxTargets);
        
        foreach (var target in targets)
        {
            int damage = skill.EffectValue;
            
            if (damage > 0 && target is PlayerCombatant player)
            {
                // 对玩家造成伤害
                // Phase 8: 传递配置的默认攻击者等级
                var actualDamage = player.ReceiveDamage(
                    damage,
                    skill.DamageType,
                    ExecuteAt,
                    attackerLevel: null,  // TODO: 传递实际敌人等级
                    defaultAttackerLevel: context.CombatEngineOptions.DefaultAttackerLevel
                );
                
                // 记录统计
                context.SegmentCollector.OnTag($"enemy_skill_damage:{skill.Id}", actualDamage);
                context.SegmentCollector.OnTag("damage_taken", actualDamage);
                
                // 检查玩家是否刚刚死亡
                if (player.ShouldTriggerDeathEvent())
                {
                    context.Scheduler.Schedule(new PlayerDeathEvent(ExecuteAt));
                }
            }
        }
    }

    /// <summary>
    /// 执行施加 Buff 效果
    /// </summary>
    private void ExecuteApplyBuff(BattleContext context, EnemySkillDefinition skill)
    {
        if (string.IsNullOrEmpty(skill.BuffId))
        {
            return;
        }
        
        // 对施法者自己施加 Buff（怪物增益）
        if (Caster.Buffs != null)
        {
            try
            {
                Caster.Buffs.Apply(skill.BuffId, ExecuteAt);
                context.SegmentCollector.OnTag($"enemy_skill_buff:{skill.Id}", 1);
                context.SegmentCollector.OnTag($"enemy_buff_applied:{skill.BuffId}", 1);
            }
            catch (System.InvalidOperationException)
            {
                // Buff 定义未注册，记录警告标签
                context.SegmentCollector.OnTag($"enemy_buff_not_found:{skill.BuffId}", 1);
            }
        }
        else
        {
            // BuffManager 未初始化，仅记录标签（向后兼容）
            context.SegmentCollector.OnTag($"enemy_skill_buff:{skill.Id}", 1);
            context.SegmentCollector.OnTag($"enemy_buff_applied:{skill.BuffId}", 1);
        }
    }

    /// <summary>
    /// 执行治疗效果
    /// </summary>
    private void ExecuteHeal(BattleContext context, EnemySkillDefinition skill)
    {
        int healAmount = skill.EffectValue;
        
        if (healAmount > 0 && !Caster.IsDead)
        {
            // 使用 Encounter 的治疗方法
            int actualHeal = Caster.Encounter.ApplyHealing(healAmount);
            
            if (actualHeal > 0)
            {
                // 记录统计
                context.SegmentCollector.OnTag($"enemy_skill_heal:{skill.Id}", actualHeal);
                context.SegmentCollector.OnTag("enemy_healed", actualHeal);
            }
        }
    }

    /// <summary>
    /// 选择目标（当前只支持选择玩家）
    /// </summary>
    private ICombatant[] SelectTargets(BattleContext context, int maxTargets)
    {
        // 怪物技能主要针对玩家
        if (context.Player.CanBeTargeted())
        {
            return new ICombatant[] { context.Player };
        }
        
        return System.Array.Empty<ICombatant>();
    }
}
