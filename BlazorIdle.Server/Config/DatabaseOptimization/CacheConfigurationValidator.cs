using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Config.DatabaseOptimization;

/// <summary>
/// 缓存配置验证器
/// Cache configuration validator
/// </summary>
/// <remarks>
/// 提供缓存配置的高级验证逻辑，确保配置的合理性和一致性
/// Provides advanced validation logic for cache configuration to ensure reasonableness and consistency
/// </remarks>
public class CacheConfigurationValidator : IValidateOptions<CacheConfiguration>
{
    /// <summary>
    /// 验证缓存配置
    /// Validate cache configuration
    /// </summary>
    /// <param name="name">配置名称 / Configuration name</param>
    /// <param name="options">缓存配置 / Cache configuration</param>
    /// <returns>验证结果 / Validation result</returns>
    public ValidateOptionsResult Validate(string? name, CacheConfiguration options)
    {
        var errors = new List<string>();
        
        // 验证全局设置
        // Validate global settings
        if (!ValidateGlobalSettings(options.GlobalSettings, errors))
        {
            return ValidateOptionsResult.Fail(errors);
        }
        
        // 验证实体策略
        // Validate entity strategies
        if (!ValidateEntityStrategies(options.EntityStrategies, errors))
        {
            return ValidateOptionsResult.Fail(errors);
        }
        
        return errors.Count > 0 
            ? ValidateOptionsResult.Fail(errors) 
            : ValidateOptionsResult.Success;
    }
    
    /// <summary>
    /// 验证全局设置
    /// Validate global settings
    /// </summary>
    private bool ValidateGlobalSettings(GlobalCacheSettings settings, List<string> errors)
    {
        var isValid = true;
        
        // 验证清理间隔
        // Validate cleanup interval
        if (settings.CleanupIntervalMinutes < 1 || settings.CleanupIntervalMinutes > 60)
        {
            errors.Add($"CleanupIntervalMinutes ({settings.CleanupIntervalMinutes}) 必须在 1-60 分钟之间 / must be between 1 and 60 minutes");
            isValid = false;
        }
        
        // 验证命中率日志间隔
        // Validate hit rate log interval
        if (settings.HitRateLogIntervalMinutes < 1 || settings.HitRateLogIntervalMinutes > 60)
        {
            errors.Add($"HitRateLogIntervalMinutes ({settings.HitRateLogIntervalMinutes}) 必须在 1-60 分钟之间 / must be between 1 and 60 minutes");
            isValid = false;
        }
        
        // 建议：命中率日志间隔应该 >= 清理间隔
        // Recommendation: Hit rate log interval should be >= cleanup interval
        if (settings.TrackCacheHitRate && 
            settings.HitRateLogIntervalMinutes < settings.CleanupIntervalMinutes)
        {
            errors.Add($"建议 HitRateLogIntervalMinutes ({settings.HitRateLogIntervalMinutes}) >= CleanupIntervalMinutes ({settings.CleanupIntervalMinutes}) / Recommend HitRateLogIntervalMinutes >= CleanupIntervalMinutes");
            // 这只是建议，不阻止启动
            // This is just a recommendation, don't block startup
        }
        
        return isValid;
    }
    
    /// <summary>
    /// 验证实体策略
    /// Validate entity strategies
    /// </summary>
    private bool ValidateEntityStrategies(Dictionary<string, EntityCacheStrategy> strategies, List<string> errors)
    {
        var isValid = true;
        
        foreach (var (entityType, strategy) in strategies)
        {
            // 验证 TTL
            // Validate TTL
            if (strategy.TtlSeconds < 60 || strategy.TtlSeconds > 86400)
            {
                errors.Add($"{entityType}: TtlSeconds ({strategy.TtlSeconds}) 必须在 60-86400 秒之间 / must be between 60 and 86400 seconds");
                isValid = false;
            }
            
            // 验证最大缓存数量
            // Validate max cached count
            if (strategy.MaxCachedCount < 100 || strategy.MaxCachedCount > 1000000)
            {
                errors.Add($"{entityType}: MaxCachedCount ({strategy.MaxCachedCount}) 必须在 100-1000000 之间 / must be between 100 and 1000000");
                isValid = false;
            }
            
            // 验证预加载批量大小
            // Validate preload batch size
            if (strategy.PreloadBatchSize < 100 || strategy.PreloadBatchSize > 10000)
            {
                errors.Add($"{entityType}: PreloadBatchSize ({strategy.PreloadBatchSize}) 必须在 100-10000 之间 / must be between 100 and 10000");
                isValid = false;
            }
            
            // 逻辑验证：Permanent 策略应该启用预加载
            // Logic validation: Permanent strategy should enable preload
            if (strategy.Strategy == CacheStrategyType.Permanent && !strategy.PreloadOnStartup)
            {
                errors.Add($"{entityType}: Permanent 策略建议启用 PreloadOnStartup / Permanent strategy should enable PreloadOnStartup");
                // 这只是建议，不阻止启动
                // This is just a recommendation, don't block startup
            }
            
            // 逻辑验证：Temporary 策略不应该启用预加载
            // Logic validation: Temporary strategy should not enable preload
            if (strategy.Strategy == CacheStrategyType.Temporary && strategy.PreloadOnStartup)
            {
                errors.Add($"{entityType}: Temporary 策略不建议启用 PreloadOnStartup（懒加载更高效） / Temporary strategy should not enable PreloadOnStartup (lazy loading is more efficient)");
                // 这只是建议，不阻止启动
                // This is just a recommendation, don't block startup
            }
            
            // 内存使用预估和警告
            // Memory usage estimation and warning
            if (strategy.PreloadOnStartup)
            {
                var estimatedMemoryMB = strategy.MaxCachedCount * 1024 / 1024 / 1024.0; // 粗略估计每个实体 1KB
                if (estimatedMemoryMB > 100)
                {
                    errors.Add($"{entityType}: 预加载可能消耗大量内存（估计 ~{estimatedMemoryMB:F1} MB），请确认服务器有足够内存 / Preload may consume significant memory (estimated ~{estimatedMemoryMB:F1} MB), ensure server has sufficient memory");
                }
            }
        }
        
        return isValid;
    }
}
