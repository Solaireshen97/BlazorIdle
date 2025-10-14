namespace BlazorIdle.Models;

/// <summary>
/// 战斗日志配置模型
/// </summary>
public class BattleLogConfig
{
    public BattleLogSettings BattleLog { get; set; } = new();
}

/// <summary>
/// 战斗日志显示设置
/// </summary>
public class BattleLogSettings
{
    /// <summary>是否启用战斗日志</summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>最大保存消息数</summary>
    public int MaxMessages { get; set; } = 50;
    
    /// <summary>显示最近消息数量</summary>
    public int DisplayLatestCount { get; set; } = 20;
    
    /// <summary>自动滚动到最新消息</summary>
    public bool AutoScroll { get; set; } = true;
    
    /// <summary>显示时间戳</summary>
    public bool ShowTimestamps { get; set; } = true;
    
    /// <summary>时间戳格式</summary>
    public string TimestampFormat { get; set; } = "HH:mm:ss";
    
    /// <summary>新消息动画效果</summary>
    public bool AnimateNewMessages { get; set; } = true;
    
    /// <summary>事件类型控制</summary>
    public EventTypesSettings EventTypes { get; set; } = new();
    
    /// <summary>UI样式设置</summary>
    public UISettings UI { get; set; } = new();
    
    /// <summary>颜色主题</summary>
    public ColorsSettings Colors { get; set; } = new();
}

/// <summary>
/// 事件类型启用设置
/// </summary>
public class EventTypesSettings
{
    /// <summary>启用攻击开始事件</summary>
    public bool EnableAttackStarted { get; set; } = true;
    
    /// <summary>启用造成伤害事件</summary>
    public bool EnableDamageApplied { get; set; } = true;
    
    /// <summary>启用受到伤害事件</summary>
    public bool EnableDamageReceived { get; set; } = true;
    
    /// <summary>启用敌人攻击事件</summary>
    public bool EnableEnemyAttackStarted { get; set; } = true;
}

/// <summary>
/// UI样式设置
/// </summary>
public class UISettings
{
    /// <summary>面板高度</summary>
    public string PanelHeight { get; set; } = "300px";
    
    /// <summary>字体大小</summary>
    public string FontSize { get; set; } = "14px";
    
    /// <summary>条目内边距</summary>
    public string EntryPadding { get; set; } = "4px 8px";
    
    /// <summary>背景颜色</summary>
    public string BackgroundColor { get; set; } = "#1a1a1a";
    
    /// <summary>文字颜色</summary>
    public string TextColor { get; set; } = "#e0e0e0";
    
    /// <summary>时间戳颜色</summary>
    public string TimestampColor { get; set; } = "#888888";
}

/// <summary>
/// 颜色主题设置
/// </summary>
public class ColorsSettings
{
    /// <summary>攻击开始颜色</summary>
    public string AttackStarted { get; set; } = "#4a9eff";
    
    /// <summary>造成伤害颜色</summary>
    public string DamageDealt { get; set; } = "#ff6b6b";
    
    /// <summary>受到伤害颜色</summary>
    public string DamageReceived { get; set; } = "#ffa94d";
    
    /// <summary>暴击颜色</summary>
    public string CriticalHit { get; set; } = "#ffdd57";
    
    /// <summary>敌人攻击颜色</summary>
    public string EnemyAttack { get; set; } = "#ff4757";
}
