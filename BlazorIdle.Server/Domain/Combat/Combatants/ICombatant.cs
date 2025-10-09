using BlazorIdle.Server.Domain.Combat.Damage;

namespace BlazorIdle.Server.Domain.Combat.Combatants;

/// <summary>
/// 战斗单位抽象接口（玩家和怪物的共同基础）
/// 未来可统一为 Actor，当前保持最小侵入
/// </summary>
public interface ICombatant
{
    /// <summary>唯一标识</summary>
    string Id { get; }
    
    /// <summary>名称</summary>
    string Name { get; }
    
    /// <summary>当前生命值</summary>
    int CurrentHp { get; }
    
    /// <summary>最大生命值</summary>
    int MaxHp { get; }
    
    /// <summary>是否死亡</summary>
    bool IsDead { get; }
    
    /// <summary>战斗状态</summary>
    CombatantState State { get; }
    
    /// <summary>死亡时间（如果已死亡）</summary>
    double? DeathTime { get; }
    
    /// <summary>复活时间（如果正在复活）</summary>
    double? ReviveAt { get; }
    
    /// <summary>仇恨权重（默认 1.0，嘲讽可提高）</summary>
    double ThreatWeight { get; set; }
    
    /// <summary>
    /// 接收伤害
    /// </summary>
    /// <param name="amount">伤害数值</param>
    /// <param name="type">伤害类型</param>
    /// <param name="now">当前战斗时间</param>
    /// <returns>实际造成的伤害</returns>
    int ReceiveDamage(int amount, DamageType type, double now);
    
    /// <summary>
    /// 是否可被选为目标
    /// </summary>
    /// <returns>存活且可被选中</returns>
    bool CanBeTargeted();
    
    /// <summary>
    /// 是否可执行动作
    /// </summary>
    /// <returns>存活且可执行动作</returns>
    bool CanAct();
}
