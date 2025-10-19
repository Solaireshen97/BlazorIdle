using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Equipment.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BlazorIdle.Server.Infrastructure.Persistence.Repositories;

/// <summary>
/// 词条定义仓储实现（支持缓存读取）
/// Affix definition repository implementation (with caching support)
/// 
/// 词条定义是静态配置数据，使用永久内存缓存策略
/// Affix definitions are static configuration data, using permanent in-memory cache strategy
/// 
/// 注意：由于 Affix 使用字符串ID而非Guid ID，不使用 MemoryStateManager
/// Note: Uses string IDs instead of Guid IDs, so doesn't use MemoryStateManager
/// </summary>
public class AffixRepository : IAffixRepository
{
    private readonly GameDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<AffixRepository>? _logger;
    
    // 静态缓存key常量
    // Static cache key constants
    private const string CACHE_KEY_ALL = "Affixes_All";
    private const string CACHE_KEY_PREFIX = "Affix_";

    /// <summary>
    /// 通过依赖注入获得上下文、配置和缓存
    /// Get context, configuration and cache through dependency injection
    /// </summary>
    public AffixRepository(
        GameDbContext db,
        IConfiguration configuration,
        IMemoryCache memoryCache,
        ILogger<AffixRepository>? logger = null)
    {
        _db = db;
        _configuration = configuration;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    /// <summary>
    /// 按ID获取词条定义（支持缓存）
    /// Get affix definition by ID (with caching support)
    /// 
    /// 逻辑说明 - Logic:
    /// 1. 检查是否启用读取缓存
    /// 2. 如果启用，优先从IMemoryCache获取
    /// 3. 如果未命中，从数据库加载并缓存
    /// 4. 如果未启用缓存，直接查询数据库（回退模式）
    /// </summary>
    public async Task<Affix?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var enableCaching = _configuration.GetValue<bool>(
            "CacheConfiguration:GlobalSettings:EnableReadCaching", true);
        
        if (enableCaching)
        {
            // 先检查单项缓存
            var cacheKey = CACHE_KEY_PREFIX + id;
            if (_memoryCache.TryGetValue<Affix>(cacheKey, out var cached))
            {
                _logger?.LogDebug("从缓存获取 Affix: {Id}", id);
                return cached;
            }
            
            // 未命中：从数据库加载
            var fromDb = await _db.Affixes.FirstOrDefaultAsync(a => a.Id == id, ct);
            
            if (fromDb != null)
            {
                // 加入缓存（永久缓存，配置数据不过期）
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    Priority = CacheItemPriority.NeverRemove
                };
                _memoryCache.Set(cacheKey, fromDb, cacheOptions);
                
                _logger?.LogDebug("Affix 已加载到缓存: {Id}", id);
            }
            
            return fromDb;
        }
        else
        {
            // 回退：直接查数据库
            _logger?.LogDebug("读取缓存已禁用，直接查询数据库 Affix#{Id}", id);
            return await _db.Affixes.FirstOrDefaultAsync(a => a.Id == id, ct);
        }
    }

    /// <summary>
    /// 获取所有词条定义（支持缓存）
    /// Get all affix definitions (with caching support)
    /// </summary>
    public async Task<List<Affix>> GetAllAsync(CancellationToken ct = default)
    {
        var enableCaching = _configuration.GetValue<bool>(
            "CacheConfiguration:GlobalSettings:EnableReadCaching", true);
        
        if (enableCaching)
        {
            // 检查全量缓存
            if (_memoryCache.TryGetValue<List<Affix>>(CACHE_KEY_ALL, out var cached))
            {
                _logger?.LogDebug("从缓存获取所有 Affixes: {Count} 条", cached?.Count ?? 0);
                return cached ?? new List<Affix>();
            }
            
            // 未命中：从数据库加载所有
            var fromDb = await _db.Affixes.ToListAsync(ct);
            
            // 加入缓存（永久缓存）
            var cacheOptions = new MemoryCacheEntryOptions
            {
                Priority = CacheItemPriority.NeverRemove
            };
            _memoryCache.Set(CACHE_KEY_ALL, fromDb, cacheOptions);
            
            // 同时缓存每个单项（方便单项查询）
            foreach (var affix in fromDb)
            {
                var itemKey = CACHE_KEY_PREFIX + affix.Id;
                _memoryCache.Set(itemKey, affix, cacheOptions);
            }
            
            _logger?.LogInformation("所有 Affixes 已加载到缓存: {Count} 条", fromDb.Count);
            return fromDb;
        }
        else
        {
            // 回退：直接查数据库
            return await _db.Affixes.ToListAsync(ct);
        }
    }

    /// <summary>
    /// 按装备槽位获取词条定义（支持缓存）
    /// Get affix definitions by slot (with caching support)
    /// </summary>
    public async Task<List<Affix>> GetBySlotAsync(EquipmentSlot slot, CancellationToken ct = default)
    {
        var enableCaching = _configuration.GetValue<bool>(
            "CacheConfiguration:GlobalSettings:EnableReadCaching", true);
        
        if (enableCaching)
        {
            // 从全量缓存筛选
            var allAffixes = await GetAllAsync(ct);
            return allAffixes
                .Where(a => a.AllowedSlots == null || a.AllowedSlots.Contains(slot))
                .ToList();
        }
        else
        {
            // 回退：直接查数据库
            return await _db.Affixes
                .Where(a => a.AllowedSlots == null || a.AllowedSlots.Contains(slot))
                .ToListAsync(ct);
        }
    }

    /// <summary>
    /// 创建词条定义
    /// Create affix definition
    /// 
    /// 注意：写入后清除缓存，确保数据一致性
    /// Note: Clear cache after write to ensure data consistency
    /// </summary>
    public async Task CreateAsync(Affix affix, CancellationToken ct = default)
    {
        affix.UpdatedAt = DateTime.UtcNow;
        _db.Affixes.Add(affix);
        await _db.SaveChangesAsync(ct);
        
        // 清除缓存（新增数据后刷新）
        _memoryCache.Remove(CACHE_KEY_ALL);
        _logger?.LogInformation("词条定义已创建，缓存已清除: {Id}", affix.Id);
    }

    /// <summary>
    /// 更新词条定义
    /// Update affix definition
    /// </summary>
    public async Task UpdateAsync(Affix affix, CancellationToken ct = default)
    {
        affix.UpdatedAt = DateTime.UtcNow;
        _db.Affixes.Update(affix);
        await _db.SaveChangesAsync(ct);
        
        // 清除相关缓存
        _memoryCache.Remove(CACHE_KEY_PREFIX + affix.Id);
        _memoryCache.Remove(CACHE_KEY_ALL);
        _logger?.LogInformation("词条定义已更新，缓存已清除: {Id}", affix.Id);
    }

    /// <summary>
    /// 删除词条定义
    /// Delete affix definition
    /// </summary>
    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var affix = await GetByIdAsync(id, ct);
        if (affix != null)
        {
            _db.Affixes.Remove(affix);
            await _db.SaveChangesAsync(ct);
            
            // 清除相关缓存
            _memoryCache.Remove(CACHE_KEY_PREFIX + id);
            _memoryCache.Remove(CACHE_KEY_ALL);
            _logger?.LogInformation("词条定义已删除，缓存已清除: {Id}", id);
        }
    }
}
