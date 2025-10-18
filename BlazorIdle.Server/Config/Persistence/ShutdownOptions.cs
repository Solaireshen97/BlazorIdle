using System.ComponentModel.DataAnnotations;

namespace BlazorIdle.Server.Config.Persistence;

/// <summary>
/// 关闭配置选项
/// </summary>
public class ShutdownOptions
{
    /// <summary>
    /// 优雅关闭超时（秒）
    /// </summary>
    [Range(10, 300)]
    public int ShutdownTimeoutSeconds { get; set; } = 30;
    
    /// <summary>
    /// 是否在关闭时设置所有角色为离线
    /// </summary>
    public bool SetCharactersOfflineOnShutdown { get; set; } = true;
    
    /// <summary>
    /// 是否在关闭时强制执行 WAL 检查点
    /// </summary>
    public bool ForceWalCheckpointOnShutdown { get; set; } = true;
    
    /// <summary>
    /// 关闭时保存重试次数
    /// </summary>
    [Range(1, 10)]
    public int ShutdownSaveRetryAttempts { get; set; } = 5;
}
