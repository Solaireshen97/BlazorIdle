namespace BlazorIdle.Server.Domain.Activities;

/// <summary>
/// 战斗活动配置数据
/// </summary>
public class CombatActivityPayload
{
    /// <summary>敌人ID</summary>
    public string? EnemyId { get; set; }
    
    /// <summary>敌人数量</summary>
    public int EnemyCount { get; set; } = 1;
    
    /// <summary>重生延迟（秒，用于持续战斗）</summary>
    public double? RespawnDelay { get; set; }
    
    /// <summary>随机种子</summary>
    public ulong? Seed { get; set; }
    
    /// <summary>当前敌人血量（用于无感继承在线->离线->在线）</summary>
    public int? CurrentEnemyHp { get; set; }
}
