namespace BlazorIdle.Server.Config;

/// <summary>
/// 战斗事件消息模板配置
/// </summary>
public sealed class BattleMessageOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "BattleMessages";
    
    /// <summary>
    /// 攻击开始消息模板
    /// 参数: {attacker}, {target}
    /// </summary>
    public string AttackStartedTemplate { get; set; } = "{attacker} 开始攻击 {target}";
    
    /// <summary>
    /// 造成伤害消息模板
    /// 参数: {attacker}, {target}, {damage}, {isCrit}
    /// </summary>
    public string DamageDealtTemplate { get; set; } = "{attacker} 对 {target} 造成 {damage} 点伤害{critSuffix}";
    
    /// <summary>
    /// 暴击后缀文本
    /// </summary>
    public string CritSuffix { get; set; } = "（暴击）";
    
    /// <summary>
    /// 受到伤害消息模板
    /// 参数: {target}, {attacker}, {damage}
    /// </summary>
    public string DamageReceivedTemplate { get; set; } = "{target} 受到来自 {attacker} 的 {damage} 点伤害";
    
    /// <summary>
    /// 敌人攻击开始消息模板
    /// 参数: {attacker}, {target}
    /// </summary>
    public string EnemyAttackStartedTemplate { get; set; } = "{attacker} 开始攻击 {target}";
    
    /// <summary>
    /// 是否启用攻击开始事件
    /// </summary>
    public bool EnableAttackStartedEvent { get; set; } = true;
    
    /// <summary>
    /// 是否启用伤害造成事件（包含消息）
    /// </summary>
    public bool EnableDamageDealtEvent { get; set; } = true;
    
    /// <summary>
    /// 是否启用伤害接收事件（包含消息）
    /// </summary>
    public bool EnableDamageReceivedEvent { get; set; } = true;
    
    /// <summary>
    /// 是否启用敌人攻击开始事件
    /// </summary>
    public bool EnableEnemyAttackStartedEvent { get; set; } = true;
    
    /// <summary>
    /// 玩家名称
    /// </summary>
    public string PlayerName { get; set; } = "玩家";
    
    /// <summary>
    /// 最大消息历史记录数
    /// </summary>
    public int MaxMessageHistory { get; set; } = 100;
}
