using BlazorIdle.Server.Domain.Combat.Combatants;
using BlazorIdle.Server.Domain.Combat.Rng;
using System.Collections.Generic;
using System.Linq;

namespace BlazorIdle.Server.Domain.Combat.Enemies;

/// <summary>
/// 怪物技能管理器：管理怪物的所有技能槽，检查触发条件
/// Phase 5: 怪物技能系统
/// </summary>
public class EnemySkillManager
{
    /// <summary>技能槽列表</summary>
    public List<EnemySkillSlot> SkillSlots { get; }
    
    /// <summary>关联的怪物</summary>
    public EnemyCombatant Enemy { get; }
    
    /// <summary>战斗开始时间</summary>
    public double CombatStartTime { get; }
    
    /// <summary>随机数生成器</summary>
    private readonly RngContext _rng;

    public EnemySkillManager(
        EnemyCombatant enemy,
        List<EnemySkillDefinition> skillDefinitions,
        double combatStartTime,
        RngContext rng)
    {
        Enemy = enemy;
        CombatStartTime = combatStartTime;
        _rng = rng;
        
        // 初始化技能槽
        SkillSlots = skillDefinitions.Select(def => new EnemySkillSlot(def)).ToList();
    }

    /// <summary>
    /// 检查所有技能，返回可以触发的技能
    /// </summary>
    /// <param name="now">当前战斗时间</param>
    /// <returns>可触发的技能，如果没有返回 null</returns>
    public EnemySkillSlot? CheckForReadySkill(double now)
    {
        // 遍历所有技能，找到第一个满足条件的
        foreach (var slot in SkillSlots)
        {
            if (ShouldTrigger(slot, now))
            {
                return slot;
            }
        }
        
        return null;
    }

    /// <summary>
    /// 判断技能是否应该触发
    /// </summary>
    /// <param name="slot">技能槽</param>
    /// <param name="now">当前战斗时间</param>
    /// <returns>是否应该触发</returns>
    private bool ShouldTrigger(EnemySkillSlot slot, double now)
    {
        var skill = slot.Definition;
        
        // 检查冷却
        if (!slot.IsReady(now))
        {
            return false;
        }
        
        // 检查触发条件
        switch (skill.Trigger)
        {
            case TriggerType.OnCooldownReady:
                // CD 就绪即可触发
                break;
                
            case TriggerType.OnHpBelow:
                // 检查血量阈值
                double hpPercent = (double)Enemy.CurrentHp / Enemy.MaxHp;
                if (hpPercent > skill.TriggerValue)
                {
                    return false;
                }
                break;
                
            case TriggerType.OnCombatTimeElapsed:
                // 检查战斗时长
                double elapsed = now - CombatStartTime;
                if (elapsed < skill.TriggerValue)
                {
                    return false;
                }
                
                // 只触发一次
                if (slot.HasTriggered)
                {
                    return false;
                }
                break;
        }
        
        // 概率检查
        if (skill.ActivationChance < 1.0)
        {
            if (!_rng.NextBool(skill.ActivationChance))
            {
                return false;
            }
        }
        
        return true;
    }
}
