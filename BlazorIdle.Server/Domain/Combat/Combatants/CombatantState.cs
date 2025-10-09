namespace BlazorIdle.Server.Domain.Combat.Combatants;

/// <summary>
/// 战斗单位状态枚举
/// </summary>
public enum CombatantState
{
    /// <summary>存活状态，可以行动</summary>
    Alive = 0,
    
    /// <summary>死亡状态，无法行动</summary>
    Dead = 1,
    
    /// <summary>复活中状态，等待复活</summary>
    Reviving = 2
}
