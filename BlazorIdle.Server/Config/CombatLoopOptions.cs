namespace BlazorIdle.Server.Config;

/// <summary>
/// 战斗循环配置选项
/// 控制战斗循环的行为，包括轨道初始化、暂停/恢复策略等
/// </summary>
public sealed class CombatLoopOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "CombatLoop";
    
    /// <summary>
    /// 攻击轨道是否在战斗开始时立即触发
    /// false: 等待完整间隔后触发（默认，提供"准备-攻击"的感觉）
    /// true: 战斗开始时立即触发（旧行为）
    /// </summary>
    public bool AttackStartsImmediately { get; set; } = false;
    
    /// <summary>
    /// 特殊轨道是否在战斗开始时立即触发
    /// false: 等待完整间隔后触发（默认）
    /// true: 战斗开始时立即触发
    /// </summary>
    public bool SpecialStartsImmediately { get; set; } = false;
    
    /// <summary>
    /// 刷新等待时是否暂停攻击轨道
    /// true: 暂停（默认，节省计算资源）
    /// false: 继续触发（用于特殊职业机制）
    /// </summary>
    public bool PauseAttackDuringSpawnWait { get; set; } = true;
    
    /// <summary>
    /// 刷新等待时是否暂停特殊轨道
    /// true: 暂停（默认）
    /// false: 继续触发（用于特殊职业机制，如战士怒气积累）
    /// </summary>
    public bool PauseSpecialDuringSpawnWait { get; set; } = true;
    
    /// <summary>
    /// 当刷新延迟小于此阈值（秒）时，跳过暂停/恢复
    /// 避免极短延迟时的不必要操作
    /// </summary>
    public double MinimumSpawnDelayForPause { get; set; } = 0.001;
    
    /// <summary>
    /// 是否启用攻击和技能的目标一致性
    /// true: 技能优先使用攻击的目标（默认）
    /// false: 技能独立选择目标（旧行为）
    /// </summary>
    public bool EnableTargetConsistency { get; set; } = true;
    
    /// <summary>
    /// 职业特定配置覆盖
    /// 允许为特定职业设置不同的行为
    /// 键为职业名称（如 "Warrior", "Mage"），值为职业特定配置
    /// </summary>
    public Dictionary<string, ProfessionCombatConfig> ProfessionOverrides { get; set; } = new();
}

/// <summary>
/// 职业特定的战斗循环配置
/// 允许不同职业有不同的轨道行为
/// </summary>
public sealed class ProfessionCombatConfig
{
    /// <summary>
    /// 攻击轨道是否立即开始（覆盖全局设置）
    /// </summary>
    public bool? AttackStartsImmediately { get; set; }
    
    /// <summary>
    /// 特殊轨道是否立即开始（覆盖全局设置）
    /// </summary>
    public bool? SpecialStartsImmediately { get; set; }
    
    /// <summary>
    /// 刷新等待时是否暂停攻击轨道（覆盖全局设置）
    /// </summary>
    public bool? PauseAttackDuringSpawnWait { get; set; }
    
    /// <summary>
    /// 刷新等待时是否暂停特殊轨道（覆盖全局设置）
    /// </summary>
    public bool? PauseSpecialDuringSpawnWait { get; set; }
    
    /// <summary>
    /// 配置说明（可选，用于文档）
    /// </summary>
    public string? Description { get; set; }
}
