namespace BlazorIdle.Server.Domain.Activities;

/// <summary>
/// 地下城活动配置数据
/// </summary>
public class DungeonActivityPayload
{
    /// <summary>地下城ID</summary>
    public string DungeonId { get; set; } = "intro_cave";
    
    /// <summary>是否循环（DungeonLoop vs DungeonSingle）</summary>
    public bool Loop { get; set; }
    
    /// <summary>波次延迟（秒）</summary>
    public double? WaveDelay { get; set; }
    
    /// <summary>轮次延迟（秒）</summary>
    public double? RunDelay { get; set; }
    
    /// <summary>随机种子</summary>
    public ulong? Seed { get; set; }
}
