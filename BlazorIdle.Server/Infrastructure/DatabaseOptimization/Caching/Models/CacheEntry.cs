namespace BlazorIdle.Server.Infrastructure.DatabaseOptimization.Caching.Models;

/// <summary>
/// 缓存项 - 包含值和元数据
/// Cache entry - contains value and metadata
/// </summary>
public class CacheEntry
{
    /// <summary>
    /// 缓存的值
    /// Cached value
    /// </summary>
    public object Value { get; set; } = default!;

    /// <summary>
    /// 创建时间
    /// Creation time
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 过期时间（null表示永不过期）
    /// Expiration time (null means never expires)
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// 最后访问时间
    /// Last accessed time
    /// </summary>
    public DateTime LastAccessedAt { get; set; }

    /// <summary>
    /// 访问次数
    /// Access count
    /// </summary>
    public long AccessCount { get; set; }

    /// <summary>
    /// 检查缓存是否已过期
    /// Check if cache entry is expired
    /// </summary>
    public bool IsExpired()
    {
        return ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
    }
}
