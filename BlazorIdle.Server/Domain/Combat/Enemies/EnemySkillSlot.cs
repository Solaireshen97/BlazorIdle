namespace BlazorIdle.Server.Domain.Combat.Enemies;

/// <summary>
/// 怪物技能槽：管理单个技能的冷却状态
/// Phase 5: 怪物技能系统
/// </summary>
public class EnemySkillSlot
{
    /// <summary>技能定义</summary>
    public EnemySkillDefinition Definition { get; }
    
    /// <summary>下次可用时间</summary>
    public double NextAvailableTime { get; private set; }
    
    /// <summary>是否已触发（用于 OnCombatTimeElapsed 类型的一次性触发）</summary>
    public bool HasTriggered { get; private set; }

    public EnemySkillSlot(EnemySkillDefinition definition)
    {
        Definition = definition;
        NextAvailableTime = 0.0; // 初始可用
        HasTriggered = false;
    }

    /// <summary>
    /// 检查技能是否就绪（冷却结束）
    /// </summary>
    /// <param name="now">当前战斗时间</param>
    /// <returns>是否就绪</returns>
    public bool IsReady(double now)
    {
        return now >= NextAvailableTime;
    }

    /// <summary>
    /// 标记技能已使用，设置冷却
    /// </summary>
    /// <param name="now">当前战斗时间</param>
    public void MarkUsed(double now)
    {
        NextAvailableTime = now + Definition.CooldownSeconds;
        
        // 如果是时间触发类型，标记为已触发
        if (Definition.Trigger == TriggerType.OnCombatTimeElapsed)
        {
            HasTriggered = true;
        }
    }
}
