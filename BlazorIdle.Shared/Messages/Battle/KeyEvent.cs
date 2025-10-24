namespace BlazorIdle.Shared.Messages.Battle;

/// <summary>
/// 关键事件 - 重要的战斗事件
/// 用于即时通知客户端重要事件（技能释放、击杀、Boss阶段切换等）
/// 关键事件使用Critical优先级立即推送，不等待下一个帧周期
/// </summary>
public class KeyEvent
{
    /// <summary>
    /// 版本号
    /// 与FrameTick共享版本空间，确保事件顺序
    /// </summary>
    public long Version { get; set; }
    
    /// <summary>
    /// 事件时间戳（Unix毫秒）
    /// 事件发生的精确服务器时间
    /// </summary>
    public long Timestamp { get; set; }
    
    /// <summary>
    /// 战斗ID
    /// 事件所属的战斗实例
    /// </summary>
    public string BattleId { get; set; } = string.Empty;
    
    /// <summary>
    /// 事件类型
    /// 定义事件的类别，用于客户端路由处理
    /// </summary>
    public KeyEventType Type { get; set; }
    
    /// <summary>
    /// 事件数据（JSON格式）
    /// 包含事件的详细信息，根据Type不同而变化
    /// 使用JSON格式提供灵活性，避免为每种事件创建单独的类
    /// </summary>
    public string Data { get; set; } = string.Empty;
}

/// <summary>
/// 关键事件类型枚举
/// 定义所有可能的关键事件类型
/// </summary>
public enum KeyEventType
{
    /// <summary>
    /// 技能释放
    /// Data包含：skillId, skillName, castTime等
    /// </summary>
    SkillCast = 0,
    
    /// <summary>
    /// 普通敌人击杀
    /// Data包含：enemyId, enemyName, experienceGained等
    /// </summary>
    EnemyKilled = 1,
    
    /// <summary>
    /// Boss死亡
    /// Data包含：bossId, bossName, rewards, achievementsUnlocked等
    /// </summary>
    BossDeath = 2,
    
    /// <summary>
    /// 玩家死亡
    /// Data包含：deathReason, killerName等
    /// </summary>
    PlayerDeath = 3,
    
    /// <summary>
    /// 特殊触发器
    /// Data包含：triggerId, triggerName, effects等
    /// 用于Boss阶段切换、环境事件等
    /// </summary>
    SpecialTrigger = 4
}
