using BlazorIdle.Server.Domain.Combat.Damage;

namespace BlazorIdle.Server.Domain.Combat.Enemies;

/// <summary>
/// 怪物技能定义：轻量级，无复杂资源
/// Phase 5: 怪物技能系统
/// </summary>
public class EnemySkillDefinition
{
    public string Id { get; }
    public string Name { get; }
    
    /// <summary>冷却时间（秒）</summary>
    public double CooldownSeconds { get; }
    
    /// <summary>触发类型</summary>
    public TriggerType Trigger { get; }
    
    /// <summary>触发值（血量阈值 0.0-1.0 或时长秒数）</summary>
    public double TriggerValue { get; }
    
    /// <summary>触发概率（默认 1.0 必定触发）</summary>
    public double ActivationChance { get; }
    
    /// <summary>技能效果类型</summary>
    public SkillEffectType Effect { get; }
    
    /// <summary>效果数值</summary>
    public int EffectValue { get; }
    
    /// <summary>伤害类型（当效果为 Damage 时生效）</summary>
    public DamageType DamageType { get; }
    
    /// <summary>Buff ID（当效果为 ApplyBuff 时生效）</summary>
    public string? BuffId { get; }
    
    /// <summary>最大目标数量（默认 1）</summary>
    public int MaxTargets { get; }

    public EnemySkillDefinition(
        string id,
        string name,
        double cooldownSeconds,
        TriggerType trigger = TriggerType.OnCooldownReady,
        double triggerValue = 0.0,
        double activationChance = 1.0,
        SkillEffectType effect = SkillEffectType.Damage,
        int effectValue = 0,
        DamageType damageType = DamageType.Physical,
        string? buffId = null,
        int maxTargets = 1)
    {
        Id = id;
        Name = name;
        CooldownSeconds = cooldownSeconds;
        Trigger = trigger;
        TriggerValue = triggerValue;
        ActivationChance = activationChance;
        Effect = effect;
        EffectValue = effectValue;
        DamageType = damageType;
        BuffId = buffId;
        MaxTargets = maxTargets;
    }
}

/// <summary>
/// 技能触发类型
/// </summary>
public enum TriggerType
{
    /// <summary>CD 就绪即释放</summary>
    OnCooldownReady,
    
    /// <summary>血量低于阈值时触发</summary>
    OnHpBelow,
    
    /// <summary>战斗开始 X 秒后触发</summary>
    OnCombatTimeElapsed
}

/// <summary>
/// 技能效果类型
/// </summary>
public enum SkillEffectType
{
    /// <summary>造成伤害</summary>
    Damage,
    
    /// <summary>施加 Buff</summary>
    ApplyBuff,
    
    /// <summary>治疗</summary>
    Heal,
    
    /// <summary>召唤（未来扩展）</summary>
    Summon
}
