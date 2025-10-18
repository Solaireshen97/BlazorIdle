using System.ComponentModel.DataAnnotations;

namespace BlazorIdle.Server.Config.DatabaseOptimization;

/// <summary>
/// 全局缓存设置
/// Global Cache Settings
/// </summary>
public class GlobalCacheSettings
{
    /// <summary>
    /// 是否启用读取缓存（总开关）
    /// Enable read caching (master switch)
    /// 
    /// 说明 - Description:
    /// - true: 启用所有缓存功能
    /// - false: 禁用缓存，所有读取直接查询数据库（用于回退）
    /// 
    /// 默认 - Default: true
    /// </summary>
    public bool EnableReadCaching { get; set; } = true;
    
    /// <summary>
    /// 缓存清理间隔（分钟）
    /// Cleanup interval in minutes
    /// 
    /// 范围 - Range: 1 到 60 分钟
    /// 默认 - Default: 5 分钟
    /// 
    /// 说明 - Description: CacheCoordinator 定期清理过期缓存的间隔
    /// </summary>
    [Range(1, 60)]
    public int CleanupIntervalMinutes { get; set; } = 5;
    
    /// <summary>
    /// 是否记录缓存命中率
    /// Track cache hit rate
    /// 
    /// 说明 - Description:
    /// - true: 记录每个实体类型的缓存命中率
    /// - false: 不记录（节省少量性能开销）
    /// 
    /// 默认 - Default: true
    /// 推荐 - Recommendation: 生产环境启用，便于监控
    /// </summary>
    public bool TrackCacheHitRate { get; set; } = true;
    
    /// <summary>
    /// 命中率记录间隔（分钟）
    /// Hit rate logging interval in minutes
    /// 
    /// 范围 - Range: 1 到 60 分钟
    /// 默认 - Default: 10 分钟
    /// 
    /// 说明 - Description: 定期输出缓存命中率到日志的间隔
    /// </summary>
    [Range(1, 60)]
    public int HitRateLogIntervalMinutes { get; set; } = 10;
}
