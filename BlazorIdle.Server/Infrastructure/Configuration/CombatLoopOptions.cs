namespace BlazorIdle.Server.Infrastructure.Configuration;

/// <summary>
/// 战斗循环配置选项
/// 用于配置战斗循环的各项行为，如攻击初始延迟、轨道暂停等
/// </summary>
public class CombatLoopOptions
{
    /// <summary>
    /// 攻击轨道是否从完整间隔开始
    /// true: 战斗开始后等待完整攻击间隔才触发第一次攻击（推荐）
    /// false: 战斗开始时立即触发攻击（旧行为）
    /// 默认值: true
    /// </summary>
    public bool AttackStartsWithFullInterval { get; set; } = true;
    
    /// <summary>
    /// 特殊轨道是否从完整间隔开始
    /// true: 战斗开始后等待完整特殊间隔才触发第一次特殊事件
    /// false: 战斗开始时立即触发特殊事件
    /// 默认值: true（与攻击轨道保持一致）
    /// </summary>
    public bool SpecialStartsWithFullInterval { get; set; } = true;
    
    /// <summary>
    /// 无怪物时是否暂停玩家攻击轨道
    /// true: 怪物全部死亡进入刷新等待时，暂停玩家攻击（推荐）
    /// false: 攻击持续触发（旧行为，但会浪费资源）
    /// 默认值: true
    /// </summary>
    public bool PauseAttackWhenNoEnemies { get; set; } = true;
    
    /// <summary>
    /// 无怪物时特殊轨道的默认暂停行为
    /// true: 怪物全部死亡时，特殊轨道也暂停（默认行为）
    /// false: 特殊轨道持续触发，不受怪物存在影响
    /// 注意: 各职业可以通过 IProfessionModule.PauseSpecialWhenNoEnemies 属性覆盖此默认值
    /// 默认值: true
    /// </summary>
    public bool PauseSpecialWhenNoEnemiesByDefault { get; set; } = true;
    
    /// <summary>
    /// 特殊轨道是否在玩家复活时立即触发
    /// true: 复活后立即触发特殊事件（适合某些职业）
    /// false: 复活后等待完整间隔才触发（默认行为）
    /// 注意: 各职业可以通过 IProfessionModule.SpecialStartsImmediately 属性覆盖此默认值
    /// 默认值: false
    /// </summary>
    public bool SpecialStartsImmediatelyAfterReviveByDefault { get; set; } = false;
    
    /// <summary>
    /// 攻击和技能是否锁定同一目标
    /// true: 一次攻击周期内，攻击和技能使用同一目标（推荐）
    /// false: 攻击和技能各自选择目标（旧行为）
    /// 默认值: true
    /// </summary>
    public bool LockTargetForAttackCycle { get; set; } = true;
}
