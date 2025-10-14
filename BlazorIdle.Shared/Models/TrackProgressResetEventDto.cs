namespace BlazorIdle.Shared.Models;

/// <summary>
/// 轨道进度重置事件
/// 用于通知前端攻击/特殊轨道的进度被重置
/// </summary>
public class TrackProgressResetEventDto
{
    /// <summary>
    /// 战斗 ID
    /// </summary>
    public Guid BattleId { get; set; }
    
    /// <summary>
    /// 事件时间（战斗时间）
    /// </summary>
    public double EventTime { get; set; }
    
    /// <summary>
    /// 事件类型（固定为 "TrackProgressReset"）
    /// </summary>
    public string EventType { get; set; } = "TrackProgressReset";
    
    /// <summary>
    /// 重置的轨道类型列表
    /// 例如: ["Attack", "Special"]
    /// </summary>
    public List<string> TrackTypes { get; set; } = new();
    
    /// <summary>
    /// 重置原因
    /// 例如: "SpawnWait", "TargetSwitch", "PlayerDeath", "PlayerRevive", "SpawnComplete"
    /// </summary>
    public string ResetReason { get; set; } = string.Empty;
    
    /// <summary>
    /// 新的下次触发时间（可选）
    /// 用于前端立即更新显示
    /// 键为轨道类型（"Attack" 或 "Special"），值为新的触发时间
    /// </summary>
    public Dictionary<string, double>? NewTriggerTimes { get; set; }
}
