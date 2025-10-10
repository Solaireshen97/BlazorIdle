using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorWebGame.Domain.Combat;

namespace BlazorIdle.Server.Domain.Combat.Combatants;

/// <summary>
/// 敌人战斗单位包装类
/// 包装现有 Encounter，实现 ICombatant 接口
/// Phase 1: 基础封装，保持与现有 Encounter 一致的行为
/// Phase 4: 添加攻击轨道支持
/// </summary>
public class EnemyCombatant : ICombatant
{
    /// <summary>关联的战斗遭遇</summary>
    public Encounter Encounter { get; }
    
    /// <summary>唯一标识</summary>
    public string Id { get; }
    
    /// <summary>敌人名称</summary>
    public string Name => Encounter.Enemy.Name;
    
    /// <summary>当前生命值</summary>
    public int CurrentHp => Encounter.CurrentHp;
    
    /// <summary>最大生命值</summary>
    public int MaxHp => Encounter.Enemy.MaxHp;
    
    /// <summary>是否死亡</summary>
    public bool IsDead => Encounter.IsDead;
    
    /// <summary>战斗状态</summary>
    public CombatantState State => IsDead ? CombatantState.Dead : CombatantState.Alive;
    
    /// <summary>死亡时间</summary>
    public double? DeathTime => Encounter.KillTime;
    
    /// <summary>复活时间（敌人不复活）</summary>
    public double? ReviveAt => null;
    
    /// <summary>仇恨权重（默认 1.0）</summary>
    public double ThreatWeight { get; set; }
    
    /// <summary>Phase 4: 怪物攻击轨道（类似玩家的 AttackTrack）</summary>
    public TrackState? AttackTrack { get; set; }
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="id">敌人唯一标识</param>
    /// <param name="encounter">关联的战斗遭遇</param>
    public EnemyCombatant(string id, Encounter encounter)
    {
        Id = id;
        Encounter = encounter;
        ThreatWeight = 1.0;
    }
    
    /// <summary>
    /// 接收伤害
    /// </summary>
    /// <param name="amount">伤害数值</param>
    /// <param name="type">伤害类型</param>
    /// <param name="now">当前战斗时间</param>
    /// <returns>实际造成的伤害</returns>
    public int ReceiveDamage(int amount, DamageType type, double now)
    {
        // 委托给现有的 Encounter.ApplyDamage
        return Encounter.ApplyDamage(amount, now);
    }
    
    /// <summary>
    /// 是否可被选为目标
    /// </summary>
    /// <returns>存活且可被选中</returns>
    public bool CanBeTargeted()
    {
        return State == CombatantState.Alive && !IsDead;
    }
    
    /// <summary>
    /// 是否可执行动作
    /// </summary>
    /// <returns>存活且可执行动作</returns>
    public bool CanAct()
    {
        return State == CombatantState.Alive && !IsDead;
    }
}
