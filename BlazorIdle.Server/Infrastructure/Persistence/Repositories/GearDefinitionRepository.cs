using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Equipment.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace BlazorIdle.Server.Infrastructure.Persistence.Repositories;

/// <summary>
/// 装备定义仓储实现（支持缓存读取）
/// Gear definition repository implementation (with caching support)
/// 
/// 装备定义是静态配置数据，使用永久内存缓存策略
/// Gear definitions are static configuration data, using permanent in-memory cache strategy
/// 
/// 注意：由于 GearDefinition 使用字符串ID而非Guid ID，不使用 MemoryStateManager
/// Note: Uses string IDs instead of Guid IDs, so doesn't use MemoryStateManager
/// </summary>
public class GearDefinitionRepository : IGearDefinitionRepository
{
    private readonly GameDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<GearDefinitionRepository>? _logger;
    
    // 静态缓存key常量
    // Static cache key constants
    private const string CACHE_KEY_ALL = "GearDefinitions_All";
    private const string CACHE_KEY_PREFIX = "GearDefinition_";

    /// <summary>
    /// 通过依赖注入获得上下文、配置和缓存
    /// Get context, configuration and cache through dependency injection
    /// </summary>
    public GearDefinitionRepository(
        GameDbContext db,
        IConfiguration configuration,
        IMemoryCache memoryCache,
        ILogger<GearDefinitionRepository>? logger = null)
    {
        _db = db;
        _configuration = configuration;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    /// <summary>
    /// 按ID获取装备定义（支持缓存）
    /// Get gear definition by ID (with caching support)
    /// 
    /// 逻辑说明 - Logic:
    /// 1. 检查是否启用读取缓存
    /// 2. 如果启用，优先从IMemoryCache获取
    /// 3. 如果未命中，从数据库加载并缓存
    /// 4. 如果未启用缓存，直接查询数据库（回退模式）
    /// </summary>
    public async Task<GearDefinition?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var enableCaching = _configuration.GetValue<bool>(
            "CacheConfiguration:GlobalSettings:EnableReadCaching", true);
        
        if (enableCaching)
        {
            // 先检查单项缓存
            var cacheKey = CACHE_KEY_PREFIX + id;
            if (_memoryCache.TryGetValue<GearDefinition>(cacheKey, out var cached))
            {
                _logger?.LogDebug("从缓存获取 GearDefinition: {Id}", id);
                return cached;
            }
            
            // 未命中：从数据库加载
            var fromDb = await _db.GearDefinitions
                .FirstOrDefaultAsync(g => g.Id == id, ct);
            
            if (fromDb != null)
            {
                // 加入缓存（永久缓存，配置数据不过期）
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    Priority = CacheItemPriority.NeverRemove
                };
                _memoryCache.Set(cacheKey, fromDb, cacheOptions);
                
                _logger?.LogDebug("GearDefinition 已加载到缓存: {Id}", id);
            }
            
            return fromDb;
        }
        else
        {
            // 回退：直接查数据库
            _logger?.LogDebug("读取缓存已禁用，直接查询数据库 GearDefinition#{Id}", id);
            return await _db.GearDefinitions.FirstOrDefaultAsync(g => g.Id == id, ct);
        }
    }

    /// <summary>
    /// 按装备槽位获取装备定义（支持缓存）
    /// Get gear definitions by slot (with caching support)
    /// </summary>
    public async Task<List<GearDefinition>> GetBySlotAsync(EquipmentSlot slot, CancellationToken ct = default)
    {
        var enableCaching = _configuration.GetValue<bool>(
            "CacheConfiguration:GlobalSettings:EnableReadCaching", true);
        
        if (enableCaching)
        {
            // 从全量缓存筛选
            var allDefs = await GetAllAsync(ct);
            return allDefs.Where(g => g.Slot == slot).ToList();
        }
        else
        {
            // 回退：直接查数据库
            return await _db.GearDefinitions
                .Where(g => g.Slot == slot)
                .ToListAsync(ct);
        }
    }

    /// <summary>
    /// 获取所有装备定义（支持缓存）
    /// Get all gear definitions (with caching support)
    /// </summary>
    public async Task<List<GearDefinition>> GetAllAsync(CancellationToken ct = default)
    {
        var enableCaching = _configuration.GetValue<bool>(
            "CacheConfiguration:GlobalSettings:EnableReadCaching", true);
        
        if (enableCaching)
        {
            // 检查全量缓存
            if (_memoryCache.TryGetValue<List<GearDefinition>>(CACHE_KEY_ALL, out var cached))
            {
                _logger?.LogDebug("从缓存获取所有 GearDefinitions: {Count} 条", cached?.Count ?? 0);
                return cached ?? new List<GearDefinition>();
            }
            
            // 未命中：从数据库加载所有
            var fromDb = await _db.GearDefinitions.ToListAsync(ct);
            
            // 加入缓存（永久缓存）
            var cacheOptions = new MemoryCacheEntryOptions
            {
                Priority = CacheItemPriority.NeverRemove
            };
            _memoryCache.Set(CACHE_KEY_ALL, fromDb, cacheOptions);
            
            // 同时缓存每个单项（方便单项查询）
            foreach (var def in fromDb)
            {
                var itemKey = CACHE_KEY_PREFIX + def.Id;
                _memoryCache.Set(itemKey, def, cacheOptions);
            }
            
            _logger?.LogInformation("所有 GearDefinitions 已加载到缓存: {Count} 条", fromDb.Count);
            return fromDb;
        }
        else
        {
            // 回退：直接查数据库
            return await _db.GearDefinitions.ToListAsync(ct);
        }
    }

    /// <summary>
    /// 创建装备定义
    /// Create gear definition
    /// 
    /// 注意：写入后清除缓存，确保数据一致性
    /// Note: Clear cache after write to ensure data consistency
    /// </summary>
    public async Task CreateAsync(GearDefinition definition, CancellationToken ct = default)
    {
        definition.UpdatedAt = DateTime.UtcNow;
        _db.GearDefinitions.Add(definition);
        await _db.SaveChangesAsync(ct);
        
        // 清除缓存（新增数据后刷新）
        _memoryCache.Remove(CACHE_KEY_ALL);
        _logger?.LogInformation("装备定义已创建，缓存已清除: {Id}", definition.Id);
    }

    /// <summary>
    /// 更新装备定义
    /// Update gear definition
    /// </summary>
    public async Task UpdateAsync(GearDefinition definition, CancellationToken ct = default)
    {
        definition.UpdatedAt = DateTime.UtcNow;
        _db.GearDefinitions.Update(definition);
        await _db.SaveChangesAsync(ct);
        
        // 清除相关缓存
        _memoryCache.Remove(CACHE_KEY_PREFIX + definition.Id);
        _memoryCache.Remove(CACHE_KEY_ALL);
        _logger?.LogInformation("装备定义已更新，缓存已清除: {Id}", definition.Id);
    }

    /// <summary>
    /// 删除装备定义
    /// Delete gear definition
    /// </summary>
    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var definition = await GetByIdAsync(id, ct);
        if (definition != null)
        {
            _db.GearDefinitions.Remove(definition);
            await _db.SaveChangesAsync(ct);
            
            // 清除相关缓存
            _memoryCache.Remove(CACHE_KEY_PREFIX + id);
            _memoryCache.Remove(CACHE_KEY_ALL);
            _logger?.LogInformation("装备定义已删除，缓存已清除: {Id}", id);
        }
    }
}
