using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Equipment.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BlazorIdle.Server.Infrastructure.Persistence.Repositories;

/// <summary>
/// 装备套装仓储实现（支持缓存读取）
/// Gear set repository implementation (with caching support)
/// 
/// 装备套装是静态配置数据，使用永久内存缓存策略
/// Gear sets are static configuration data, using permanent in-memory cache strategy
/// 
/// 注意：由于 GearSet 使用字符串ID而非Guid ID，不使用 MemoryStateManager
/// Note: Uses string IDs instead of Guid IDs, so doesn't use MemoryStateManager
/// </summary>
public class GearSetRepository : IGearSetRepository
{
    private readonly GameDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<GearSetRepository>? _logger;
    
    // 静态缓存key常量
    // Static cache key constants
    private const string CACHE_KEY_ALL = "GearSets_All";
    private const string CACHE_KEY_PREFIX = "GearSet_";

    /// <summary>
    /// 通过依赖注入获得上下文、配置和缓存
    /// Get context, configuration and cache through dependency injection
    /// </summary>
    public GearSetRepository(
        GameDbContext db,
        IConfiguration configuration,
        IMemoryCache memoryCache,
        ILogger<GearSetRepository>? logger = null)
    {
        _db = db;
        _configuration = configuration;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    /// <summary>
    /// 按ID获取装备套装（支持缓存）
    /// Get gear set by ID (with caching support)
    /// 
    /// 逻辑说明 - Logic:
    /// 1. 检查是否启用读取缓存
    /// 2. 如果启用，优先从IMemoryCache获取
    /// 3. 如果未命中，从数据库加载并缓存
    /// 4. 如果未启用缓存，直接查询数据库（回退模式）
    /// </summary>
    public async Task<GearSet?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var enableCaching = _configuration.GetValue<bool>(
            "CacheConfiguration:GlobalSettings:EnableReadCaching", true);
        
        if (enableCaching)
        {
            // 先检查单项缓存
            var cacheKey = CACHE_KEY_PREFIX + id;
            if (_memoryCache.TryGetValue<GearSet>(cacheKey, out var cached))
            {
                _logger?.LogDebug("从缓存获取 GearSet: {Id}", id);
                return cached;
            }
            
            // 未命中：从数据库加载
            var fromDb = await _db.GearSets.FirstOrDefaultAsync(s => s.Id == id, ct);
            
            if (fromDb != null)
            {
                // 加入缓存（永久缓存，配置数据不过期）
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    Priority = CacheItemPriority.NeverRemove
                };
                _memoryCache.Set(cacheKey, fromDb, cacheOptions);
                
                _logger?.LogDebug("GearSet 已加载到缓存: {Id}", id);
            }
            
            return fromDb;
        }
        else
        {
            // 回退：直接查数据库
            _logger?.LogDebug("读取缓存已禁用，直接查询数据库 GearSet#{Id}", id);
            return await _db.GearSets.FirstOrDefaultAsync(s => s.Id == id, ct);
        }
    }

    /// <summary>
    /// 获取所有装备套装（支持缓存）
    /// Get all gear sets (with caching support)
    /// </summary>
    public async Task<List<GearSet>> GetAllAsync(CancellationToken ct = default)
    {
        var enableCaching = _configuration.GetValue<bool>(
            "CacheConfiguration:GlobalSettings:EnableReadCaching", true);
        
        if (enableCaching)
        {
            // 检查全量缓存
            if (_memoryCache.TryGetValue<List<GearSet>>(CACHE_KEY_ALL, out var cached))
            {
                _logger?.LogDebug("从缓存获取所有 GearSets: {Count} 条", cached?.Count ?? 0);
                return cached ?? new List<GearSet>();
            }
            
            // 未命中：从数据库加载所有
            var fromDb = await _db.GearSets.ToListAsync(ct);
            
            // 加入缓存（永久缓存）
            var cacheOptions = new MemoryCacheEntryOptions
            {
                Priority = CacheItemPriority.NeverRemove
            };
            _memoryCache.Set(CACHE_KEY_ALL, fromDb, cacheOptions);
            
            // 同时缓存每个单项（方便单项查询）
            foreach (var set in fromDb)
            {
                var itemKey = CACHE_KEY_PREFIX + set.Id;
                _memoryCache.Set(itemKey, set, cacheOptions);
            }
            
            _logger?.LogInformation("所有 GearSets 已加载到缓存: {Count} 条", fromDb.Count);
            return fromDb;
        }
        else
        {
            // 回退：直接查数据库
            return await _db.GearSets.ToListAsync(ct);
        }
    }

    /// <summary>
    /// 创建装备套装
    /// Create gear set
    /// 
    /// 注意：写入后清除缓存，确保数据一致性
    /// Note: Clear cache after write to ensure data consistency
    /// </summary>
    public async Task CreateAsync(GearSet gearSet, CancellationToken ct = default)
    {
        gearSet.UpdatedAt = DateTime.UtcNow;
        _db.GearSets.Add(gearSet);
        await _db.SaveChangesAsync(ct);
        
        // 清除缓存（新增数据后刷新）
        _memoryCache.Remove(CACHE_KEY_ALL);
        _logger?.LogInformation("装备套装已创建，缓存已清除: {Id}", gearSet.Id);
    }

    /// <summary>
    /// 更新装备套装
    /// Update gear set
    /// </summary>
    public async Task UpdateAsync(GearSet gearSet, CancellationToken ct = default)
    {
        gearSet.UpdatedAt = DateTime.UtcNow;
        _db.GearSets.Update(gearSet);
        await _db.SaveChangesAsync(ct);
        
        // 清除相关缓存
        _memoryCache.Remove(CACHE_KEY_PREFIX + gearSet.Id);
        _memoryCache.Remove(CACHE_KEY_ALL);
        _logger?.LogInformation("装备套装已更新，缓存已清除: {Id}", gearSet.Id);
    }

    /// <summary>
    /// 删除装备套装
    /// Delete gear set
    /// </summary>
    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var gearSet = await GetByIdAsync(id, ct);
        if (gearSet != null)
        {
            _db.GearSets.Remove(gearSet);
            await _db.SaveChangesAsync(ct);
            
            // 清除相关缓存
            _memoryCache.Remove(CACHE_KEY_PREFIX + id);
            _memoryCache.Remove(CACHE_KEY_ALL);
            _logger?.LogInformation("装备套装已删除，缓存已清除: {Id}", id);
        }
    }
}
