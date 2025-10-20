using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Config.DatabaseOptimization;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization.Caching;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization.Caching.Abstractions;
using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Infrastructure.Persistence.Repositories;

/// <summary>
/// 缓存感知的角色仓储 - 装饰器模式
/// Cache-aware character repository - Decorator pattern
/// 
/// 优先从缓存读取角色信息，缓存未命中时才查询数据库
/// Prioritizes reading from cache, only queries database on cache miss
/// </summary>
public class CacheAwareCharacterRepository : CacheAwareRepository<Character, Guid>, ICharacterRepository
{
    private readonly ICharacterRepository _innerRepository;
    private readonly ICacheInvalidationCoordinator _invalidationCoordinator;
    
    /// <summary>
    /// 构造函数
    /// Constructor
    /// </summary>
    /// <param name="innerRepository">原始角色仓储</param>
    /// <param name="cacheManager">缓存管理器</param>
    /// <param name="invalidationCoordinator">缓存失效协调器</param>
    /// <param name="cacheOptions">缓存配置选项</param>
    /// <param name="logger">日志记录器</param>
    public CacheAwareCharacterRepository(
        ICharacterRepository innerRepository,
        IMultiTierCacheManager cacheManager,
        ICacheInvalidationCoordinator invalidationCoordinator,
        IOptions<ReadCacheOptions> cacheOptions,
        ILogger<CacheAwareCharacterRepository> logger)
        : base(cacheManager, cacheOptions, logger)
    {
        _innerRepository = innerRepository;
        _invalidationCoordinator = invalidationCoordinator;
    }
    
    /// <summary>
    /// 获取角色信息（优先从缓存）
    /// Get character (cache-first)
    /// </summary>
    /// <param name="id">角色ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>角色对象或null</returns>
    public async Task<Character?> GetAsync(Guid id, CancellationToken ct = default)
    {
        // 通过基类的 GetWithCacheAsync 实现缓存逻辑
        // Use base class GetWithCacheAsync for cache logic
        return await GetWithCacheAsync(
            id,
            async () => await _innerRepository.GetAsync(id, ct),
            entityType: "Character",
            ct: ct
        );
    }
}
