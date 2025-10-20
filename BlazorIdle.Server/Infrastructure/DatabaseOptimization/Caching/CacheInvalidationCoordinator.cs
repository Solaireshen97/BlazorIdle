using BlazorIdle.Server.Config.DatabaseOptimization;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization.Caching.Abstractions;
using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Infrastructure.DatabaseOptimization.Caching;

/// <summary>
/// 缓存失效协调器
/// Cache invalidation coordinator
/// 管理缓存失效逻辑，包括自动失效和级联失效
/// </summary>
public interface ICacheInvalidationCoordinator
{
    /// <summary>
    /// 实体更新时失效相关缓存
    /// Invalidate related caches when entity is updated
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <param name="id">实体ID</param>
    /// <param name="ct">取消令牌</param>
    Task OnEntityUpdatedAsync(string entityType, Guid id, CancellationToken ct = default);
    
    /// <summary>
    /// 角色装备变更时失效相关缓存
    /// Invalidate related caches when character's gear changes
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="gearInstanceId">装备实例ID</param>
    /// <param name="ct">取消令牌</param>
    Task OnGearChangedAsync(Guid characterId, Guid gearInstanceId, CancellationToken ct = default);
    
    /// <summary>
    /// 角色升级时失效相关缓存
    /// Invalidate related caches when character levels up
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="ct">取消令牌</param>
    Task OnCharacterLevelUpAsync(Guid characterId, CancellationToken ct = default);
    
    /// <summary>
    /// 手动失效指定缓存
    /// Manually invalidate specific cache
    /// </summary>
    /// <param name="cacheKey">缓存键</param>
    Task InvalidateAsync(string cacheKey);
    
    /// <summary>
    /// 手动批量失效缓存（支持模式匹配）
    /// Manually batch invalidate caches (supports pattern matching)
    /// </summary>
    /// <param name="pattern">缓存键模式</param>
    Task InvalidateByPatternAsync(string pattern);
}

/// <summary>
/// 缓存失效协调器实现
/// Cache invalidation coordinator implementation
/// </summary>
public class CacheInvalidationCoordinator : ICacheInvalidationCoordinator
{
    private readonly IMultiTierCacheManager _cacheManager;
    private readonly ReadCacheOptions _options;
    private readonly ILogger<CacheInvalidationCoordinator> _logger;
    
    public CacheInvalidationCoordinator(
        IMultiTierCacheManager cacheManager,
        IOptions<ReadCacheOptions> options,
        ILogger<CacheInvalidationCoordinator> logger)
    {
        _cacheManager = cacheManager;
        _options = options.Value;
        _logger = logger;
    }
    
    /// <summary>
    /// 实体更新时失效相关缓存
    /// </summary>
    public async Task OnEntityUpdatedAsync(string entityType, Guid id, CancellationToken ct = default)
    {
        if (!_options.EnableReadCache)
        {
            return;
        }

        // 获取实体策略
        if (!_options.EntityStrategies.TryGetValue(entityType, out var strategy) ||
            !strategy.InvalidateOnUpdate)
        {
            return;
        }
        
        // 失效自身
        await InvalidateSingleAsync(entityType, id);
        
        // 级联失效
        if (_options.Invalidation.EnableCascading)
        {
            foreach (var cascadePattern in strategy.CascadeInvalidation)
            {
                var pattern = cascadePattern.Replace("{id}", id.ToString());
                await _cacheManager.InvalidateByPatternAsync(pattern);
                
                if (_options.Invalidation.LogInvalidations)
                {
                    _logger.LogDebug("级联失效缓存: {Pattern}", pattern);
                }
            }
        }
    }
    
    /// <summary>
    /// 装备变更时失效相关缓存
    /// </summary>
    public async Task OnGearChangedAsync(Guid characterId, Guid gearInstanceId, CancellationToken ct = default)
    {
        if (!_options.EnableReadCache)
        {
            return;
        }

        // 失效装备实例缓存
        await InvalidateSingleAsync("GearInstance", gearInstanceId);
        
        // 失效角色的装备列表缓存（所有相关查询）
        await _cacheManager.InvalidateByPatternAsync($"GearInstance:*:{characterId}");
        
        // 级联失效：角色属性（依赖装备）
        await _cacheManager.InvalidateByPatternAsync($"Character:Stats:{characterId}");
        await InvalidateSingleAsync("Character", characterId);
        
        if (_options.Invalidation.LogInvalidations)
        {
            _logger.LogDebug(
                "装备变更失效缓存: CharacterId={CharacterId}, GearInstanceId={GearInstanceId}",
                characterId, gearInstanceId);
        }
    }
    
    /// <summary>
    /// 角色升级时失效相关缓存
    /// </summary>
    public async Task OnCharacterLevelUpAsync(Guid characterId, CancellationToken ct = default)
    {
        if (!_options.EnableReadCache)
        {
            return;
        }

        // 失效角色缓存
        await InvalidateSingleAsync("Character", characterId);
        
        // 失效角色属性缓存
        await _cacheManager.InvalidateByPatternAsync($"Character:Stats:{characterId}");
        
        // 可能影响装备需求检查，失效装备列表
        await _cacheManager.InvalidateByPatternAsync($"GearInstance:*:{characterId}");
        
        if (_options.Invalidation.LogInvalidations)
        {
            _logger.LogDebug("角色升级失效缓存: CharacterId={CharacterId}", characterId);
        }
    }
    
    /// <summary>
    /// 手动失效指定缓存
    /// </summary>
    public async Task InvalidateAsync(string cacheKey)
    {
        await _cacheManager.InvalidateAsync(cacheKey);
        
        if (_options.Invalidation.LogInvalidations)
        {
            _logger.LogDebug("手动失效缓存: {CacheKey}", cacheKey);
        }
    }
    
    /// <summary>
    /// 手动批量失效缓存
    /// </summary>
    public async Task InvalidateByPatternAsync(string pattern)
    {
        await _cacheManager.InvalidateByPatternAsync(pattern);
        
        if (_options.Invalidation.LogInvalidations)
        {
            _logger.LogDebug("手动批量失效缓存: {Pattern}", pattern);
        }
    }
    
    /// <summary>
    /// 失效单个实体缓存
    /// </summary>
    private async Task InvalidateSingleAsync(string entityType, Guid id)
    {
        var cacheKey = $"{entityType}:{id}";
        await _cacheManager.InvalidateAsync(cacheKey);
        
        if (_options.Invalidation.LogInvalidations)
        {
            _logger.LogDebug("失效缓存: {CacheKey}", cacheKey);
        }
    }
}
