using BlazorIdle.Server.Domain.Combat.Combatants;
using System.Collections.Generic;
using System.Linq;

namespace BlazorIdle.Server.Domain.Combat.Enemies;

/// <summary>
/// 怪物技能管理器：管理怪物的所有技能槽
/// Phase 5: 怪物技能系统
/// </summary>
public class EnemySkillManager
{
    /// <summary>关联的敌人战斗单位</summary>
    public EnemyCombatant Owner { get; }
    
    /// <summary>技能槽列表</summary>
    public List<EnemySkillSlot> Skills { get; } = new();
    
    /// <summary>战斗开始时间（用于 OnCombatTimeElapsed 触发条件）</summary>
    private readonly double _combatStartTime;

    public EnemySkillManager(EnemyCombatant owner, double combatStartTime)
    {
        Owner = owner;
        _combatStartTime = combatStartTime;
    }

    /// <summary>
    /// 添加技能
    /// </summary>
    public void AddSkill(EnemySkillDefinition definition, double initialCooldown = 0.0)
    {
        var slot = new EnemySkillSlot(definition, _combatStartTime + initialCooldown);
        Skills.Add(slot);
    }

    /// <summary>
    /// 检查是否有技能可以触发
    /// </summary>
    /// <param name="now">当前战斗时间</param>
    /// <param name="rng">随机数生成器（用于激活概率）</param>
    /// <returns>可触发的技能槽，如果没有则返回 null</returns>
    public EnemySkillSlot? CheckTrigger(double now, Server.Domain.Combat.Rng.RngContext rng)
    {
        // 怪物必须存活才能释放技能
        if (!Owner.CanAct())
        {
            return null;
        }

        // 遍历所有技能，按优先级（定义顺序）检查
        foreach (var skill in Skills)
        {
            // 对于一次性触发（OnCombatTimeElapsed），先检查是否已触发
            if (skill.Definition.Trigger == TriggerType.OnCombatTimeElapsed && skill.HasTriggered)
            {
                continue;
            }

            if (!skill.IsReady(now))
            {
                continue;
            }

            // 检查触发条件
            if (!CheckTriggerCondition(skill, now))
            {
                continue;
            }

            // 检查激活概率
            if (skill.Definition.ActivationChance < 1.0)
            {
                if (!rng.NextBool(skill.Definition.ActivationChance))
                {
                    // 概率未命中，但仍然消耗冷却
                    skill.Consume(now);
                    continue;
                }
            }

            // 找到可触发的技能
            return skill;
        }

        return null;
    }

    /// <summary>
    /// 检查技能触发条件是否满足
    /// </summary>
    private bool CheckTriggerCondition(EnemySkillSlot skill, double now)
    {
        switch (skill.Definition.Trigger)
        {
            case TriggerType.OnCooldownReady:
                // 冷却就绪即可触发
                return true;

            case TriggerType.OnHpBelow:
                // 血量低于阈值才触发
                var hpPercent = Owner.MaxHp > 0 
                    ? (double)Owner.CurrentHp / Owner.MaxHp 
                    : 1.0;
                return hpPercent <= skill.Definition.TriggerValue;

            case TriggerType.OnCombatTimeElapsed:
                // 战斗时长达到要求才触发（仅触发一次）
                var elapsed = now - _combatStartTime;
                return elapsed >= skill.Definition.TriggerValue && !skill.HasTriggered;

            default:
                return false;
        }
    }
}
