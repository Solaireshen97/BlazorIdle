namespace BlazorIdle.Server.Domain.Activities;

/// <summary>
/// 战斗进度快照
/// 用于保存和恢复战斗中的敌人状态，实现无感的在线->离线->在线继承
/// </summary>
public class BattleProgressSnapshot
{
    /// <summary>当前主要敌人的血量</summary>
    public int? PrimaryEnemyHp { get; set; }
    
    /// <summary>当前波次索引（用于副本）</summary>
    public int? WaveIndex { get; set; }
    
    /// <summary>已完成的Run数量（用于副本）</summary>
    public int? RunCount { get; set; }
    
    /// <summary>快照时间（游戏时间，秒）</summary>
    public double SimulatedSeconds { get; set; }
}
