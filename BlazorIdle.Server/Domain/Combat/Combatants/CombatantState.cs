namespace BlazorIdle.Server.Domain.Combat.Combatants;

/// <summary>
/// 战斗单位的状态枚举
/// </summary>
public enum CombatantState
{
    /// <summary>
    /// 存活状态 - 可以行动和被攻击
    /// </summary>
    Alive,

    /// <summary>
    /// 死亡状态 - 无法行动，等待复活
    /// </summary>
    Dead,

    /// <summary>
    /// 复活中状态 - 正在复活倒计时
    /// </summary>
    Reviving
}
