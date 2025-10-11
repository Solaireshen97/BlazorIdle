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
    
    /// <summary>玩家总护甲值（从装备获取）</summary>
    public double TotalArmor { get; set; } = 0.0;
    
    /// <summary>格挡概率（装备盾牌时）</summary>
    public double BlockChance { get; set; } = 0.0;
    
    /// <summary>护甲计算服务（可选，用于计算减伤）</summary>
    private readonly ArmorCalculator? _armorCalculator;
    
    /// <summary>格挡计算服务（可选，用于格挡判定）</summary>
    private readonly BlockCalculator? _blockCalculator;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="id">角色唯一标识</param>
    /// <param name="name">角色名称</param>
    /// <param name="stats">角色统计数据</param>
    /// <param name="stamina">耐力值（用于计算最大生命值）</param>
    /// <param name="armorCalculator">护甲计算服务（可选）</param>
    /// <param name="blockCalculator">格挡计算服务（可选）</param>
    public PlayerCombatant(
        string id, 
        string name, 
        CharacterStats stats, 
        int stamina = 10,
        ArmorCalculator? armorCalculator = null,
        BlockCalculator? blockCalculator = null)
    {
        Id = id;
        Name = name;
        Stats = stats;
        MaxHp = stamina * 10; // 10 HP per Stamina
        CurrentHp = MaxHp;
        State = CombatantState.Alive;
        ThreatWeight = 1.0;
        _armorCalculator = armorCalculator;
        _blockCalculator = blockCalculator;
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
        
        // Phase 4: 应用物理伤害减免（护甲和格挡）
        if (type == DamageType.Physical)
        {
            // 1. 尝试格挡（如果装备盾牌）
            if (BlockChance > 0 && _blockCalculator != null)
            {
                if (_blockCalculator.RollBlock(BlockChance))
                {
                    mitigatedDamage = _blockCalculator.ApplyBlockReduction(mitigatedDamage);
                    // 格挡标记可以通过外部事件系统记录
                }
            }
            
            // 2. 应用护甲减伤
            if (TotalArmor > 0 && _armorCalculator != null)
            {
                // 使用默认攻击者等级50（典型怪物等级）
                // TODO: 在EnemyAttackEvent中传递实际敌人等级
                const int defaultAttackerLevel = 50;
                double armorReduction = _armorCalculator.CalculateArmorReduction(TotalArmor, defaultAttackerLevel);
                mitigatedDamage = (int)Math.Ceiling(mitigatedDamage * (1.0 - armorReduction));
            }
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
