namespace BlazorIdle.Server.Config;

/// <summary>
/// 战斗事件消息配置选项
/// </summary>
public sealed class BattleEventsOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "BattleEvents";
    
    /// <summary>
    /// 是否启用战斗事件消息
    /// </summary>
    public bool EnableBattleEventMessages { get; set; } = true;
    
    /// <summary>
    /// 消息模板配置
    /// </summary>
    public BattleEventMessages Messages { get; set; } = new();
    
    /// <summary>
    /// 伤害类型名称映射
    /// </summary>
    public Dictionary<string, string> DamageTypeNames { get; set; } = new()
    {
        { "Physical", "物理" },
        { "Magic", "魔法" },
        { "True", "真实" }
    };
}

/// <summary>
/// 战斗事件消息模板
/// </summary>
public sealed class BattleEventMessages
{
    /// <summary>
    /// 攻击开始消息配置
    /// </summary>
    public AttackStartMessages AttackStart { get; set; } = new();
    
    /// <summary>
    /// 伤害造成消息配置
    /// </summary>
    public DamageDealtMessages DamageDealt { get; set; } = new();
    
    /// <summary>
    /// 受到伤害消息配置
    /// </summary>
    public DamageReceivedMessages DamageReceived { get; set; } = new();
}

/// <summary>
/// 攻击开始消息模板
/// </summary>
public sealed class AttackStartMessages
{
    /// <summary>
    /// 是否启用攻击开始消息
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// 玩家攻击敌人消息模板
    /// 可用占位符：{attacker}, {target}
    /// </summary>
    public string PlayerAttacksEnemy { get; set; } = "{attacker} 开始攻击 {target}";
    
    /// <summary>
    /// 敌人攻击玩家消息模板
    /// 可用占位符：{attacker}, {target}
    /// </summary>
    public string EnemyAttacksPlayer { get; set; } = "{attacker} 向 {target} 发起攻击";
}

/// <summary>
/// 伤害造成消息模板
/// </summary>
public sealed class DamageDealtMessages
{
    /// <summary>
    /// 是否启用伤害造成消息
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// 普通伤害消息模板
    /// 可用占位符：{attacker}, {target}, {damage}, {damageType}
    /// </summary>
    public string Normal { get; set; } = "{attacker} 对 {target} 造成 {damage} 点{damageType}伤害";
    
    /// <summary>
    /// 暴击伤害消息模板
    /// 可用占位符：{attacker}, {target}, {damage}, {damageType}
    /// </summary>
    public string Critical { get; set; } = "{attacker} 对 {target} 造成 {damage} 点{damageType}暴击伤害！";
}

/// <summary>
/// 受到伤害消息模板
/// </summary>
public sealed class DamageReceivedMessages
{
    /// <summary>
    /// 是否启用受到伤害消息
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// 玩家受到伤害消息模板
    /// 可用占位符：{receiver}, {attacker}, {damage}, {damageType}, {currentHp}, {maxHp}
    /// </summary>
    public string Player { get; set; } = "{receiver} 受到 {attacker} 的 {damage} 点{damageType}伤害（剩余 {currentHp}/{maxHp}）";
    
    /// <summary>
    /// 敌人受到伤害消息模板
    /// 可用占位符：{receiver}, {damage}, {damageType}, {currentHp}, {maxHp}
    /// </summary>
    public string Enemy { get; set; } = "{receiver} 受到 {damage} 点{damageType}伤害（剩余 {currentHp}/{maxHp}）";
}
