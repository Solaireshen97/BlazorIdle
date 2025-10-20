using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using BlazorIdle.Server.Config.DatabaseOptimization;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization.Caching.Abstractions;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization.Caching.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Infrastructure.DatabaseOptimization.Caching;

/// <summary>
/// 多层缓存管理器实现
/// Multi-tier cache manager implementation
/// 实现 L1/L2/L3 三层缓存架构
/// </summary>
public class MultiTierCacheManager : IMultiTierCacheManager
{
    // L1: Session Cache - 使用 ASP.NET Core MemoryCache
    private readonly IMemoryCache _sessionCache;
    
    // L2: Entity Cache - 使用 ConcurrentDictionary + LRU
    private readonly ConcurrentDictionary<string, CacheEntry> _entityCache;
    
    // L3: Static Cache - 使用 ConcurrentDictionary (永久)
    private readonly ConcurrentDictionary<string, object> _staticCache;
    
    // 防缓存击穿：每个键一个信号量
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores;
    
    // 配置选项
    private readonly ReadCacheOptions _options;
    
    // 日志
    private readonly ILogger<MultiTierCacheManager> _logger;
    
    // 统计信息
    private long _hits;
    private long _misses;
    private readonly ConcurrentQueue<(DateTime timestamp, long durationMs, bool isHit, CacheTier tier)> _recentOperations;
    
    public MultiTierCacheManager(
        IMemoryCache memoryCache,
        IOptions<ReadCacheOptions> options,
        ILogger<MultiTierCacheManager> logger)
    {
        _sessionCache = memoryCache;
        _entityCache = new ConcurrentDictionary<string, CacheEntry>();
        _staticCache = new ConcurrentDictionary<string, object>();
        _semaphores = new ConcurrentDictionary<string, SemaphoreSlim>();
        _options = options.Value;
        _logger = logger;
        _recentOperations = new ConcurrentQueue<(DateTime, long, bool, CacheTier)>();
    }

    /// <summary>
    /// 获取缓存项（穿透三层）
    /// </summary>
    public Task<T?> GetAsync<T>(string cacheKey, CancellationToken ct = default) where T : class
    {
        if (!_options.EnableReadCache)
        {
            return Task.FromResult<T?>(null);
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        // 1. 尝试从 Session Cache 获取
        if (_options.SessionCache.Enabled && _sessionCache.TryGetValue<T>(cacheKey, out var sessionValue))
        {
            RecordOperation(sw.ElapsedMilliseconds, true, CacheTier.Session);
            Interlocked.Increment(ref _hits);
            return Task.FromResult<T?>(sessionValue);
        }
        
        // 2. 尝试从 Entity Cache 获取
        if (_options.EntityCache.Enabled && TryGetFromEntityCache<T>(cacheKey, out var entityValue))
        {
            RecordOperation(sw.ElapsedMilliseconds, true, CacheTier.Entity);
            Interlocked.Increment(ref _hits);
            return Task.FromResult<T?>(entityValue);
        }
        
        // 3. 尝试从 Static Cache 获取
        if (_options.StaticCache.Enabled && _staticCache.TryGetValue(cacheKey, out var staticObj) && staticObj is T staticValue)
        {
            RecordOperation(sw.ElapsedMilliseconds, true, CacheTier.Static);
            Interlocked.Increment(ref _hits);
            return Task.FromResult<T?>(staticValue);
        }
        
        Interlocked.Increment(ref _misses);
        return Task.FromResult<T?>(null);
    }

    /// <summary>
    /// 获取或加载缓存项（含防击穿保护）
    /// </summary>
    public async Task<T?> GetOrLoadAsync<T>(
        string cacheKey,
        Func<Task<T?>> loader,
        CacheTier tier,
        TimeSpan? ttl = null,
        CancellationToken ct = default) where T : class
    {
        if (!_options.EnableReadCache)
        {
            return await loader();
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        // 1. 先尝试从缓存获取
        var cached = await GetAsync<T>(cacheKey, ct);
        if (cached != null)
        {
            return cached;
        }
        
        // 2. 缓存未命中，使用信号量防止击穿
        if (!_options.Performance.EnableAntiCrashing)
        {
            // 未启用防击穿，直接加载
            Interlocked.Increment(ref _misses);
            RecordOperation(sw.ElapsedMilliseconds, false, tier);
            var value = await loader();
            if (value != null)
            {
                await SetAsync(cacheKey, value, tier, ttl);
            }
            return value;
        }
        
        // 3. 获取或创建信号量
        var semaphore = _semaphores.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        
        // 4. 等待信号量
        var timeout = _options.Performance.AntiCrashingSemaphoreTimeout;
        if (!await semaphore.WaitAsync(timeout, ct))
        {
            // 超时：直接查询（降级）
            _logger.LogWarning("缓存加载超时: {CacheKey}, 超时时间: {Timeout}ms", cacheKey, timeout);
            Interlocked.Increment(ref _misses);
            RecordOperation(sw.ElapsedMilliseconds, false, tier);
            return await loader();
        }
        
        try
        {
            // 5. 双重检查（可能已被其他线程加载）
            cached = await GetAsync<T>(cacheKey, ct);
            if (cached != null)
            {
                return cached;
            }
            
            // 6. 从数据源加载
            Interlocked.Increment(ref _misses);
            RecordOperation(sw.ElapsedMilliseconds, false, tier);
            
            var value = await loader();
            
            // 7. 存入缓存
            if (value != null)
            {
                await SetAsync(cacheKey, value, tier, ttl);
            }
            
            return value;
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// 设置缓存项
    /// </summary>
    public async Task SetAsync<T>(string cacheKey, T value, CacheTier tier, TimeSpan? ttl = null) where T : class
    {
        if (!_options.EnableReadCache)
        {
            return;
        }

        var effectiveTtl = ttl ?? GetDefaultTtl(tier);
        
        switch (tier)
        {
            case CacheTier.Session when _options.SessionCache.Enabled:
                SetToSessionCache(cacheKey, value, effectiveTtl);
                break;
                
            case CacheTier.Entity when _options.EntityCache.Enabled:
                SetToEntityCache(cacheKey, value, effectiveTtl);
                break;
                
            case CacheTier.Static when _options.StaticCache.Enabled:
                _staticCache.TryAdd(cacheKey, value);
                break;
        }
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// 失效指定缓存项
    /// </summary>
    public async Task InvalidateAsync(string cacheKey)
    {
        // 从所有层级移除
        _sessionCache.Remove(cacheKey);
        _entityCache.TryRemove(cacheKey, out _);
        _staticCache.TryRemove(cacheKey, out _);
        
        if (_options.Invalidation.LogInvalidations)
        {
            _logger.LogDebug("失效缓存: {CacheKey}", cacheKey);
        }
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// 批量失效缓存（支持模式匹配）
    /// </summary>
    public async Task InvalidateByPatternAsync(string pattern)
    {
        if (!_options.Invalidation.EnablePatternMatch)
        {
            _logger.LogWarning("模式匹配失效未启用，跳过模式: {Pattern}", pattern);
            return;
        }

        // 将通配符模式转换为正则表达式
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        var regex = new Regex(regexPattern, RegexOptions.Compiled);
        
        var removedCount = 0;
        
        // Session Cache - MemoryCache 不支持按模式移除，需要跟踪键
        // 这里我们跳过，因为 MemoryCache 的键无法枚举
        
        // Entity Cache
        var entityKeysToRemove = _entityCache.Keys.Where(k => regex.IsMatch(k)).ToList();
        foreach (var key in entityKeysToRemove)
        {
            if (_entityCache.TryRemove(key, out _))
            {
                removedCount++;
            }
        }
        
        // Static Cache
        var staticKeysToRemove = _staticCache.Keys.Where(k => regex.IsMatch(k)).ToList();
        foreach (var key in staticKeysToRemove)
        {
            if (_staticCache.TryRemove(key, out _))
            {
                removedCount++;
            }
        }
        
        if (_options.Invalidation.LogInvalidations)
        {
            _logger.LogDebug("模式失效缓存: {Pattern}, 移除项数: {Count}", pattern, removedCount);
        }
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// 清理过期缓存
    /// </summary>
    public int CleanupExpired()
    {
        var removedCount = 0;
        
        // Entity Cache - 清理过期项
        var expiredKeys = _entityCache
            .Where(kvp => kvp.Value.IsExpired())
            .Select(kvp => kvp.Key)
            .ToList();
        
        foreach (var key in expiredKeys)
        {
            if (_entityCache.TryRemove(key, out _))
            {
                removedCount++;
            }
        }
        
        // 检查是否需要压缩
        if (_entityCache.Count > _options.EntityCache.MaxSize)
        {
            removedCount += CompactEntityCache();
        }
        
        if (removedCount > 0)
        {
            _logger.LogInformation("清理过期缓存: 移除 {Count} 项", removedCount);
        }
        
        return removedCount;
    }

    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        var totalOps = Interlocked.Read(ref _hits) + Interlocked.Read(ref _misses);
        
        // 计算性能指标
        var recentOps = _recentOperations.ToArray();
        var durations = recentOps.Select(op => op.durationMs).ToArray();
        
        var stats = new CacheStatistics
        {
            TotalOperations = totalOps,
            Hits = Interlocked.Read(ref _hits),
            Misses = Interlocked.Read(ref _misses),
            AvgDurationMs = durations.Any() ? durations.Average() : 0,
            P95DurationMs = CalculatePercentile(durations, 0.95),
            P99DurationMs = CalculatePercentile(durations, 0.99),
            TotalEntries = _entityCache.Count + _staticCache.Count,
            EstimatedMemoryMB = EstimateMemoryUsage()
        };
        
        // 计算各层级统计
        foreach (var tier in Enum.GetValues<CacheTier>())
        {
            var tierOps = recentOps.Where(op => op.tier == tier).ToArray();
            stats.TierStatistics[tier] = new TierStatistics
            {
                TotalOps = tierOps.Length,
                Hits = tierOps.Count(op => op.isHit),
                EntryCount = tier switch
                {
                    CacheTier.Entity => _entityCache.Count,
                    CacheTier.Static => _staticCache.Count,
                    _ => 0 // Session cache 无法获取计数
                }
            };
        }
        
        return stats;
    }

    /// <summary>
    /// 获取缓存内容摘要
    /// </summary>
    public CacheContentSummary GetContentSummary()
    {
        var summary = new CacheContentSummary
        {
            TotalEntries = _entityCache.Count + _staticCache.Count,
            ByTier = new Dictionary<string, int>
            {
                ["Entity"] = _entityCache.Count,
                ["Static"] = _staticCache.Count
            },
            MemoryUsageEstimateMB = EstimateMemoryUsage()
        };
        
        // 按实体类型统计（从缓存键中提取）
        var entityTypes = _entityCache.Keys
            .Concat(_staticCache.Keys)
            .Select(key => key.Split(':').FirstOrDefault() ?? "Unknown")
            .GroupBy(type => type)
            .ToDictionary(g => g.Key, g => g.Count());
        
        summary.ByEntityType = entityTypes;
        
        return summary;
    }

    #region Private Helper Methods

    /// <summary>
    /// 从 Entity Cache 获取值
    /// </summary>
    private bool TryGetFromEntityCache<T>(string cacheKey, out T? value) where T : class
    {
        value = null;
        
        if (_entityCache.TryGetValue(cacheKey, out var entry))
        {
            if (!entry.IsExpired())
            {
                // 更新访问信息
                entry.LastAccessedAt = DateTime.UtcNow;
                entry.AccessCount++;
                
                value = entry.Value as T;
                return value != null;
            }
            else
            {
                // 过期：移除
                _entityCache.TryRemove(cacheKey, out _);
            }
        }
        
        return false;
    }

    /// <summary>
    /// 设置到 Session Cache
    /// </summary>
    private void SetToSessionCache<T>(string cacheKey, T value, TimeSpan ttl) where T : class
    {
        var cacheEntryOptions = new MemoryCacheEntryOptions();
        
        if (_options.SessionCache.SlidingExpiration)
        {
            cacheEntryOptions.SlidingExpiration = ttl;
        }
        else
        {
            cacheEntryOptions.AbsoluteExpirationRelativeToNow = ttl;
        }
        
        // 设置大小（用于容量限制）
        cacheEntryOptions.Size = 1;
        
        _sessionCache.Set(cacheKey, value, cacheEntryOptions);
    }

    /// <summary>
    /// 设置到 Entity Cache
    /// </summary>
    private void SetToEntityCache<T>(string cacheKey, T value, TimeSpan ttl) where T : class
    {
        var entry = new CacheEntry
        {
            Value = value,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(ttl),
            LastAccessedAt = DateTime.UtcNow,
            AccessCount = 0
        };
        
        _entityCache.AddOrUpdate(cacheKey, entry, (_, __) => entry);
        
        // 检查容量
        if (_entityCache.Count > _options.EntityCache.MaxSize)
        {
            CompactEntityCache();
        }
    }

    /// <summary>
    /// 压缩 Entity Cache（LRU 淘汰）
    /// </summary>
    private int CompactEntityCache()
    {
        var targetSize = (int)(_options.EntityCache.MaxSize * (1 - _options.EntityCache.CompactionPercentage));
        var toRemove = _entityCache.Count - targetSize;
        
        if (toRemove <= 0)
        {
            return 0;
        }
        
        // LRU: 按最后访问时间排序，移除最旧的
        var keysToRemove = _entityCache
            .OrderBy(kvp => kvp.Value.LastAccessedAt)
            .Take(toRemove)
            .Select(kvp => kvp.Key)
            .ToList();
        
        var removedCount = 0;
        foreach (var key in keysToRemove)
        {
            if (_entityCache.TryRemove(key, out _))
            {
                removedCount++;
            }
        }
        
        _logger.LogInformation("Entity Cache 压缩: 目标大小={TargetSize}, 移除={Removed}", targetSize, removedCount);
        
        return removedCount;
    }

    /// <summary>
    /// 获取默认 TTL
    /// </summary>
    private TimeSpan GetDefaultTtl(CacheTier tier)
    {
        return tier switch
        {
            CacheTier.Session => TimeSpan.FromMinutes(_options.SessionCache.DefaultTtlMinutes),
            CacheTier.Entity => TimeSpan.FromMinutes(_options.EntityCache.DefaultTtlMinutes),
            CacheTier.Static => TimeSpan.MaxValue,
            _ => TimeSpan.FromMinutes(15)
        };
    }

    /// <summary>
    /// 记录操作统计
    /// </summary>
    private void RecordOperation(long durationMs, bool isHit, CacheTier tier)
    {
        if (!_options.EnableStatistics)
        {
            return;
        }

        _recentOperations.Enqueue((DateTime.UtcNow, durationMs, isHit, tier));
        
        // 限制队列大小
        while (_recentOperations.Count > 1000)
        {
            _recentOperations.TryDequeue(out _);
        }
    }

    /// <summary>
    /// 计算百分位数
    /// </summary>
    private static double CalculatePercentile(long[] values, double percentile)
    {
        if (values.Length == 0)
        {
            return 0;
        }

        var sorted = values.OrderBy(x => x).ToArray();
        var index = (int)Math.Ceiling(sorted.Length * percentile) - 1;
        index = Math.Max(0, Math.Min(sorted.Length - 1, index));
        
        return sorted[index];
    }

    /// <summary>
    /// 估算内存使用（粗略估计）
    /// </summary>
    private double EstimateMemoryUsage()
    {
        // 粗略估计：每个缓存项平均 1KB
        const double avgBytesPerEntry = 1024;
        var totalEntries = _entityCache.Count + _staticCache.Count;
        var totalBytes = totalEntries * avgBytesPerEntry;
        var totalMB = totalBytes / 1024 / 1024;
        
        return Math.Round(totalMB, 2);
    }

    #endregion
}
