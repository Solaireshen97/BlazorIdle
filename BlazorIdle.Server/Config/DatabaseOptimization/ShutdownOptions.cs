using System.ComponentModel.DataAnnotations;

namespace BlazorIdle.Server.Config.DatabaseOptimization;

/// <summary>
/// 优雅关闭配置选项
/// Graceful shutdown configuration options
/// </summary>
public class ShutdownOptions
{
    /// <summary>
    /// 优雅关闭超时（秒）
    /// Graceful shutdown timeout (seconds)
    /// Default: 30
    /// </summary>
    [Range(10, 300)]
    public int ShutdownTimeoutSeconds { get; set; } = 30;
    
    /// <summary>
    /// 是否在关闭时设置所有角色为离线
    /// Set all characters offline on shutdown
    /// Default: true
    /// </summary>
    public bool SetCharactersOfflineOnShutdown { get; set; } = true;
    
    /// <summary>
    /// 是否在关闭时强制执行 WAL 检查点
    /// Force WAL checkpoint on shutdown
    /// Default: true
    /// </summary>
    public bool ForceWalCheckpointOnShutdown { get; set; } = true;
    
    /// <summary>
    /// 关闭时保存重试次数
    /// Number of retry attempts during shutdown save
    /// Default: 5
    /// </summary>
    [Range(1, 10)]
    public int ShutdownSaveRetryAttempts { get; set; } = 5;
}
