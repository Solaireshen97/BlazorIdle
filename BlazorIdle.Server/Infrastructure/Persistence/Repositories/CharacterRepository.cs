using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BlazorIdle.Server.Infrastructure.Persistence.Repositories;

/// <summary>
/// 角色数据仓储实现：封装对 Character 实体的读取操作
/// Character repository implementation: Encapsulates read operations for Character entities
/// 
/// 支持缓存优先读取策略，减少数据库访问
/// Supports cache-first read strategy to reduce database access
/// </summary>
public class CharacterRepository : ICharacterRepository
{
    private readonly GameDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IMemoryStateManager<Character>? _memoryManager;
    private readonly ILogger<CharacterRepository>? _logger;

    /// <summary>
    /// 通过依赖注入获得上下文、配置和缓存管理器
    /// Get context, configuration and cache manager through dependency injection
    /// </summary>
    public CharacterRepository(
        GameDbContext db,
        IConfiguration configuration,
        IMemoryStateManager<Character>? memoryManager = null,
        ILogger<CharacterRepository>? logger = null)
    {
        _db = db;
        _configuration = configuration;
        _memoryManager = memoryManager;
        _logger = logger;
    }

    /// <summary>
    /// 按角色 ID 异步查询一个角色（支持缓存）
    /// Query a character by ID asynchronously (with caching support)
    /// 
    /// 逻辑说明 - Logic:
    /// 1. 检查是否启用读取缓存
    /// 2. 如果启用，使用缓存优先策略（先查内存，未命中再查数据库）
    /// 3. 如果未启用，直接查询数据库（回退模式）
    /// </summary>
    /// <param name="id">角色ID / Character ID</param>
    /// <param name="ct">取消令牌 / Cancellation token</param>
    /// <returns>角色对象，找不到返回 null / Character or null if not found</returns>
    public async Task<Character?> GetAsync(Guid id, CancellationToken ct = default)
    {
        // 检查是否启用读取缓存
        var enableCaching = _configuration.GetValue<bool>(
            "CacheConfiguration:GlobalSettings:EnableReadCaching", true);
        
        if (enableCaching && _memoryManager != null)
        {
            // 使用缓存优先策略
            // 注意：Character 已在写入时使用 MemoryStateManager
            // 这里读取时也使用同一个 MemoryStateManager
            // 确保读写共享同一份内存数据，保证数据一致性
            return await _memoryManager.TryGetAsync(
                id,
                async (id, ct) => await _db.Characters
                    .FirstOrDefaultAsync(c => c.Id == id, ct),
                ct
            );
        }
        else
        {
            // 回退：直接查数据库
            _logger?.LogDebug(
                "读取缓存已禁用或 MemoryManager 未注册，直接查询数据库 Character#{Id}",
                id
            );
            return await _db.Characters
                .FirstOrDefaultAsync(c => c.Id == id, ct);
        }
    }
}