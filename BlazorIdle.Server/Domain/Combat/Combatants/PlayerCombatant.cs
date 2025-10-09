using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Damage;

namespace BlazorIdle.Server.Domain.Combat.Combatants;

/// <summary>
/// 玩家战斗单位包装类
/// 包装现有 CharacterStats，实现 ICombatant 接口
/// Phase 1: 初始状态始终为 Alive，HP 不变（兼容现有逻辑）
/// </summary>
public class PlayerCombatant : ICombatant
{
    /// <summary>角色统计数据</summary>
    public CharacterStats Stats { get; }
    
    /// <summary>唯一标识</summary>
    public string Id { get; }
    
    /// <summary>角色名称</summary>
    public string Name { get; }
    
    /// <summary>当前生命值（Phase 1: 固定为 MaxHp，不受伤）</summary>
    public int CurrentHp { get; private set; }
    
    /// <summary>最大生命值（基于 Stamina 计算，10 HP per Stamina）</summary>
    public int MaxHp { get; }
    
    /// <summary>是否死亡（Phase 1: 始终为 false）</summary>
    public bool IsDead => CurrentHp <= 0;
    
    /// <summary>战斗状态（Phase 1: 始终为 Alive）</summary>
    public CombatantState State { get; private set; }
    
    /// <summary>死亡时间（Phase 1: 始终为 null）</summary>
    public double? DeathTime { get; private set; }
    
    /// <summary>复活时间（Phase 1: 始终为 null）</summary>
    public double? ReviveAt { get; private set; }
    
    /// <summary>仇恨权重（默认 1.0）</summary>
    public double ThreatWeight { get; set; }
    
    /// <summary>复活持续时间（秒）</summary>
    public double ReviveDurationSeconds { get; set; } = 10.0;
    
    /// <summary>是否允许自动复活</summary>
    public bool AutoReviveEnabled { get; set; } = true;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="id">角色唯一标识</param>
    /// <param name="name">角色名称</param>
    /// <param name="stats">角色统计数据</param>
    /// <param name="stamina">耐力值（用于计算最大生命值）</param>
    public PlayerCombatant(string id, string name, CharacterStats stats, int stamina = 10)
    {
        Id = id;
        Name = name;
        Stats = stats;
        MaxHp = stamina * 10; // 10 HP per Stamina
        CurrentHp = MaxHp;
        State = CombatantState.Alive;
        ThreatWeight = 1.0;
    }
    
    /// <summary>
    /// 接收伤害
    /// Phase 1: 不实际扣血，保持兼容性
    /// </summary>
    /// <param name="amount">伤害数值</param>
    /// <param name="type">伤害类型</param>
    /// <param name="now">当前战斗时间</param>
    /// <returns>实际造成的伤害（Phase 1: 始终返回 0）</returns>
    public int ReceiveDamage(int amount, DamageType type, double now)
    {
        // Phase 1: 玩家不受伤害，保持现有逻辑
        return 0;
    }
    
    /// <summary>
    /// 是否可被选为目标
    /// </summary>
    /// <returns>存活且可被选中</returns>
    public bool CanBeTargeted()
    {
        return State == CombatantState.Alive;
    }
    
    /// <summary>
    /// 是否可执行动作
    /// </summary>
    /// <returns>存活且可执行动作</returns>
    public bool CanAct()
    {
        return State == CombatantState.Alive;
    }
}
