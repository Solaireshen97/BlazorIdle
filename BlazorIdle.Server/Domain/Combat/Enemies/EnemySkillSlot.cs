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
    
    /// <summary>是否已触发过（用于一次性触发条件）</summary>
    public bool HasTriggered { get; private set; }

    public EnemySkillSlot(EnemySkillDefinition definition, double initialAvailableTime = 0.0)
    {
        Definition = definition;
        NextAvailableTime = initialAvailableTime;
        HasTriggered = false;
    }

    /// <summary>
    /// 检查技能是否冷却完毕
    /// </summary>
    /// <param name="now">当前战斗时间</param>
    /// <returns>是否可以使用</returns>
    public bool IsReady(double now)
    {
        return now >= NextAvailableTime;
    }

    /// <summary>
    /// 消耗技能（设置下次可用时间）
    /// </summary>
    /// <param name="now">当前战斗时间</param>
    public void Consume(double now)
    {
        NextAvailableTime = now + Definition.CooldownSeconds;
        HasTriggered = true;
    }

    /// <summary>
    /// 重置技能状态（用于怪物重生等场景）
    /// </summary>
    public void Reset(double availableTime = 0.0)
    {
        NextAvailableTime = availableTime;
        HasTriggered = false;
    }
}
