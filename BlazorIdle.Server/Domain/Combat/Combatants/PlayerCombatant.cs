using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Damage;

namespace BlazorIdle.Server.Domain.Combat.Combatants;

/// <summary>
/// 玩家战斗单位包装类
/// Phase 1: 仅提供基础接口实现，保持兼容现有逻辑（HP 不变，不受伤）
/// </summary>
public class PlayerCombatant : ICombatant
{
    private const int HpPerStamina = 10;

    /// <summary>
    /// 包装的角色属性
    /// </summary>
    public CharacterStats Stats { get; }

    /// <summary>
    /// 角色ID（用于标识）
    /// </summary>
    public string CharacterId { get; }

    /// <summary>
    /// 角色名称
    /// </summary>
    public string CharacterName { get; }

    /// <summary>
    /// 耐力值（用于计算最大生命值）
    /// </summary>
    public int Stamina { get; }

    public PlayerCombatant(CharacterStats stats, string characterId, string characterName, int stamina)
    {
        Stats = stats;
        CharacterId = characterId;
        CharacterName = characterName;
        Stamina = stamina;
    }

    // ICombatant 接口实现

    public string Id => CharacterId;

    public string Name => CharacterName;

    /// <summary>
    /// Phase 1: 玩家始终满血（兼容现有逻辑）
    /// </summary>
    public int CurrentHp => MaxHp;

    /// <summary>
    /// 基于耐力值计算最大生命值
    /// </summary>
    public int MaxHp => Stamina * HpPerStamina;

    /// <summary>
    /// Phase 1: 玩家不会死亡
    /// </summary>
    public bool IsDead => false;

    /// <summary>
    /// Phase 1: 始终为 Alive 状态
    /// </summary>
    public CombatantState State => CombatantState.Alive;

    /// <summary>
    /// Phase 1: 不死亡，无死亡时间
    /// </summary>
    public double? DeathTime => null;

    /// <summary>
    /// Phase 1: 不死亡，无复活时间
    /// </summary>
    public double? ReviveAt => null;

    /// <summary>
    /// 仇恨权重 - 默认 1.0
    /// Phase 1: 暂不使用，预留给 Phase 2
    /// </summary>
    public double ThreatWeight { get; set; } = 1.0;

    /// <summary>
    /// 接收伤害 - Phase 1: 不实际扣血，保持现有逻辑
    /// </summary>
    public int ReceiveDamage(int amount, DamageType type, double now)
    {
        // Phase 1: 玩家不受伤害，返回 0
        // Phase 3 将实现实际的受伤逻辑
        return 0;
    }

    /// <summary>
    /// 是否可以被攻击 - Phase 1: 始终可以
    /// </summary>
    public bool CanBeTargeted() => true;

    /// <summary>
    /// 是否可以行动 - Phase 1: 始终可以
    /// </summary>
    public bool CanAct() => true;
}
