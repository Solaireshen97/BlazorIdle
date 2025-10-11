using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Equipment.Services;

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
    /// Phase 4: 应用护甲减伤和格挡机制
    /// </summary>
    /// <param name="amount">伤害数值</param>
    /// <param name="type">伤害类型</param>
    /// <param name="now">当前战斗时间</param>
    /// <returns>实际造成的伤害（如果已死亡则返回 0）</returns>
    public int ReceiveDamage(int amount, DamageType type, double now)
    {
        if (State == CombatantState.Dead)
            return 0;

        int mitigatedDamage = amount;

        // Phase 4: 只对物理伤害应用护甲减伤
        if (type == DamageType.Physical)
        {
            mitigatedDamage = ApplyArmorReduction(amount);
        }

        // Phase 4: 格挡判定（所有伤害类型都可能被格挡）
        bool blocked = RollBlock();
        if (blocked)
        {
            mitigatedDamage = ApplyBlockReduction(mitigatedDamage);
        }

        var actualDamage = Math.Min(mitigatedDamage, CurrentHp);
        CurrentHp -= actualDamage;
        
        // Phase 3: 检测死亡
        if (CurrentHp <= 0 && State == CombatantState.Alive)
        {
            CurrentHp = 0;
            State = CombatantState.Dead;
            DeathTime = now;
            
            // 设置复活时间（如果启用自动复活）
            if (AutoReviveEnabled)
            {
                ReviveAt = now + ReviveDurationSeconds;
            }
            
            // 注意：死亡事件需要由调用者在检测到死亡后手动调度到事件系统
            // 这里只标记状态，不直接操作 BattleContext
        }
        
        return actualDamage;
    }

    /// <summary>
    /// 应用护甲减伤
    /// </summary>
    /// <param name="amount">原始伤害</param>
    /// <returns>减伤后伤害</returns>
    private int ApplyArmorReduction(int amount)
    {
        if (Stats.Armor <= 0)
            return amount;

        // 使用固定的攻击者等级（可以后续优化为传入参数）
        // 这里假设攻击者等级为玩家等级的近似值
        const int attackerLevel = 10; // 临时值，后续可从 BattleContext 获取

        // 护甲减伤公式：Armor / (Armor + K * AttackerLevel + C)
        // K = 50, C = 400
        const double K = 50.0;
        const double C = 400.0;
        double denominator = Stats.Armor + (K * attackerLevel + C);
        double reduction = Stats.Armor / denominator;
        
        // 限制最大减伤75%
        reduction = Math.Min(reduction, 0.75);
        
        int mitigatedDamage = (int)Math.Round(amount * (1.0 - reduction));
        return Math.Max(1, mitigatedDamage); // 至少造成1点伤害
    }

    /// <summary>
    /// 判断是否格挡成功
    /// </summary>
    /// <returns>是否格挡</returns>
    private bool RollBlock()
    {
        if (Stats.BlockChance <= 0)
            return false;

        // 使用随机数判断是否格挡
        return Random.Shared.NextDouble() < Stats.BlockChance;
    }

    /// <summary>
    /// 应用格挡减伤（30%）
    /// </summary>
    /// <param name="amount">原始伤害</param>
    /// <returns>格挡后伤害</returns>
    private int ApplyBlockReduction(int amount)
    {
        const double blockReduction = 0.30; // 格挡减伤30%
        return (int)Math.Round(amount * (1.0 - blockReduction));
    }
    
    /// <summary>
    /// 执行复活
    /// </summary>
    /// <param name="now">当前战斗时间</param>
    public void Revive(double now)
    {
        CurrentHp = MaxHp;
        State = CombatantState.Alive;
        DeathTime = null;
        ReviveAt = null;
    }
    
    /// <summary>
    /// 检查是否需要调度死亡事件
    /// 由外部调用以在伤害应用后触发死亡处理
    /// </summary>
    /// <returns>如果刚刚死亡且需要处理，返回 true</returns>
    public bool ShouldTriggerDeathEvent()
    {
        return State == CombatantState.Dead && DeathTime.HasValue;
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
