using BlazorIdle.Server.Domain.Combat.Damage;

namespace BlazorIdle.Server.Domain.Combat.Combatants;

/// <summary>
/// 怪物战斗单位包装类
/// Phase 1: 包装现有 Encounter，实现 ICombatant 接口
/// </summary>
public class EnemyCombatant : ICombatant
{
    /// <summary>
    /// 包装的 Encounter
    /// </summary>
    public Encounter Encounter { get; }

    public EnemyCombatant(Encounter encounter)
    {
        Encounter = encounter;
    }

    // ICombatant 接口实现

    public string Id => Encounter.Enemy.Id;

    public string Name => Encounter.Enemy.Name;

    public int CurrentHp => Encounter.CurrentHp;

    public int MaxHp => Encounter.Enemy.MaxHp;

    public bool IsDead => Encounter.IsDead;

    /// <summary>
    /// Phase 1: 简单映射，死亡则为 Dead，否则为 Alive
    /// </summary>
    public CombatantState State => IsDead ? CombatantState.Dead : CombatantState.Alive;

    /// <summary>
    /// 死亡时间
    /// </summary>
    public double? DeathTime => Encounter.KillTime;

    /// <summary>
    /// Phase 1: 怪物不复活，总是 null
    /// </summary>
    public double? ReviveAt => null;

    /// <summary>
    /// 仇恨权重 - 默认 1.0
    /// Phase 1: 暂不使用，预留给 Phase 2
    /// </summary>
    public double ThreatWeight { get; set; } = 1.0;

    /// <summary>
    /// 接收伤害 - 委托给内部的 Encounter
    /// </summary>
    public int ReceiveDamage(int amount, DamageType type, double now)
    {
        // Phase 1: 直接委托给 Encounter
        // 忽略 type 参数，因为 Encounter.ApplyDamage 不需要它
        return Encounter.ApplyDamage(amount, now);
    }

    /// <summary>
    /// 是否可以被攻击 - 存活的怪物可以被攻击
    /// </summary>
    public bool CanBeTargeted() => !IsDead;

    /// <summary>
    /// Phase 1: 怪物暂不主动行动，总是返回 false
    /// Phase 4 将实现怪物攻击能力
    /// </summary>
    public bool CanAct() => false;
}
