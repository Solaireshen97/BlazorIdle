using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BlazorIdle.Server.Infrastructure.Persistence.Repositories;

/// <summary>
/// 装备实例仓储实现（支持缓存读取）
/// Gear instance repository implementation (with caching support)
/// 
/// 注意：装备定义（GearDefinition）的关联需要单独加载
/// Note: GearDefinition associations need to be loaded separately
/// </summary>
public class GearInstanceRepository : IGearInstanceRepository
{
    private readonly GameDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IMemoryStateManager<GearInstance>? _memoryManager;
    private readonly ILogger<GearInstanceRepository>? _logger;
    private readonly IGearDefinitionRepository? _gearDefinitionRepo;

    public GearInstanceRepository(
        GameDbContext db,
        IConfiguration configuration,
        IMemoryStateManager<GearInstance>? memoryManager = null,
        ILogger<GearInstanceRepository>? logger = null,
        IGearDefinitionRepository? gearDefinitionRepo = null)
    {
        _db = db;
        _configuration = configuration;
        _memoryManager = memoryManager;
        _logger = logger;
        _gearDefinitionRepo = gearDefinitionRepo;
    }

    /// <summary>
    /// 按ID获取装备实例（支持缓存）
    /// Get gear instance by ID (with caching support)
    /// </summary>
    public async Task<GearInstance?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var enableCaching = _configuration.GetValue<bool>(
            "CacheConfiguration:GlobalSettings:EnableReadCaching", true);
        
        if (enableCaching && _memoryManager != null)
        {
            // 使用缓存优先策略
            var instance = await _memoryManager.TryGetAsync(
                id,
                async (id, ct) => await _db.GearInstances
                    .FirstOrDefaultAsync(g => g.Id == id, ct),
                ct
            );
            
            // 加载关联的装备定义
            if (instance != null && instance.Definition == null && _gearDefinitionRepo != null)
            {
                instance.Definition = await _gearDefinitionRepo.GetByIdAsync(
                    instance.DefinitionId, ct);
            }
            
            return instance;
        }
        else
        {
            // 回退：直接查数据库（包含关联）
            return await _db.GearInstances
                .Include(g => g.Definition)
                .FirstOrDefaultAsync(g => g.Id == id, ct);
        }
    }

    /// <summary>
    /// 获取角色已装备的装备（支持缓存）
    /// Get equipped gear for a character (with caching support)
    /// </summary>
    public async Task<List<GearInstance>> GetEquippedGearAsync(Guid characterId, CancellationToken ct = default)
    {
        var enableCaching = _configuration.GetValue<bool>(
            "CacheConfiguration:GlobalSettings:EnableReadCaching", true);
        
        if (enableCaching && _memoryManager != null)
        {
            // 从缓存筛选
            var allCached = _memoryManager.GetAll();
            var instances = allCached
                .Where(g => g.CharacterId == characterId && g.IsEquipped)
                .ToList();
            
            // 加载关联的装备定义
            await LoadDefinitionsAsync(instances, ct);
            
            return instances;
        }
        else
        {
            // 回退：直接查数据库
            return await _db.GearInstances
                .Include(g => g.Definition)
                .Where(g => g.CharacterId == characterId && g.IsEquipped)
                .ToListAsync(ct);
        }
    }

    /// <summary>
    /// 获取角色的所有装备（支持缓存）
    /// Get all gear for a character (with caching support)
    /// </summary>
    public async Task<List<GearInstance>> GetGearByCharacterAsync(Guid characterId, CancellationToken ct = default)
    {
        var enableCaching = _configuration.GetValue<bool>(
            "CacheConfiguration:GlobalSettings:EnableReadCaching", true);
        
        if (enableCaching && _memoryManager != null)
        {
            // 从缓存筛选
            var allCached = _memoryManager.GetAll();
            var instances = allCached
                .Where(g => g.CharacterId == characterId)
                .ToList();
            
            // 加载关联的装备定义
            await LoadDefinitionsAsync(instances, ct);
            
            return instances;
        }
        else
        {
            // 回退：直接查数据库
            return await _db.GearInstances
                .Include(g => g.Definition)
                .Where(g => g.CharacterId == characterId)
                .ToListAsync(ct);
        }
    }

    /// <summary>
    /// 获取角色指定槽位的已装备装备（支持缓存）
    /// Get equipped gear by slot for a character (with caching support)
    /// </summary>
    public async Task<GearInstance?> GetEquippedGearBySlotAsync(
        Guid characterId, 
        EquipmentSlot slot, 
        CancellationToken ct = default)
    {
        var enableCaching = _configuration.GetValue<bool>(
            "CacheConfiguration:GlobalSettings:EnableReadCaching", true);
        
        if (enableCaching && _memoryManager != null)
        {
            // 从缓存筛选
            var allCached = _memoryManager.GetAll();
            var instance = allCached
                .FirstOrDefault(g => g.CharacterId == characterId && g.IsEquipped && g.SlotType == slot);
            
            // 加载关联的装备定义
            if (instance != null && instance.Definition == null && _gearDefinitionRepo != null)
            {
                instance.Definition = await _gearDefinitionRepo.GetByIdAsync(
                    instance.DefinitionId, ct);
            }
            
            return instance;
        }
        else
        {
            // 回退：直接查数据库
            return await _db.GearInstances
                .Include(g => g.Definition)
                .FirstOrDefaultAsync(g => g.CharacterId == characterId && g.IsEquipped && g.SlotType == slot, ct);
        }
    }

    /// <summary>
    /// 辅助方法：为装备实例列表加载关联的装备定义
    /// Helper method: Load definitions for a list of gear instances
    /// </summary>
    private async Task LoadDefinitionsAsync(List<GearInstance> instances, CancellationToken ct)
    {
        if (_gearDefinitionRepo == null)
            return;
            
        foreach (var instance in instances)
        {
            if (instance.Definition == null)
            {
                instance.Definition = await _gearDefinitionRepo.GetByIdAsync(
                    instance.DefinitionId, ct);
            }
        }
    }

    /// <summary>
    /// 创建装备实例
    /// Create gear instance
    /// </summary>
    public async Task CreateAsync(GearInstance instance, CancellationToken ct = default)
    {
        instance.UpdatedAt = DateTime.UtcNow;
        _db.GearInstances.Add(instance);
        
        // 检查是否启用内存缓冲
        var enableMemoryBuffering = _configuration.GetValue<bool>("Persistence:EnableMemoryBuffering", false);
        if (enableMemoryBuffering && _memoryManager != null)
        {
            // 使用内存缓冲：将实体添加到内存，标记为dirty
            _memoryManager.Add(instance);
            // 不立即调用 SaveChangesAsync，由 PersistenceCoordinator 批量保存
        }
        else
        {
            // 未启用内存缓冲：保持原有的立即保存行为
            await _db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// 更新装备实例
    /// Update gear instance
    /// </summary>
    public async Task UpdateAsync(GearInstance instance, CancellationToken ct = default)
    {
        instance.UpdatedAt = DateTime.UtcNow;
        _db.GearInstances.Update(instance);
        
        // 检查是否启用内存缓冲
        var enableMemoryBuffering = _configuration.GetValue<bool>("Persistence:EnableMemoryBuffering", false);
        if (enableMemoryBuffering && _memoryManager != null)
        {
            // 使用内存缓冲：更新内存中的实体，标记为dirty
            _memoryManager.Update(instance);
            // 不立即调用 SaveChangesAsync，由 PersistenceCoordinator 批量保存
        }
        else
        {
            // 未启用内存缓冲：保持原有的立即保存行为
            await _db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// 删除装备实例
    /// Delete gear instance
    /// </summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        // 先尝试从缓存获取
        var instance = await GetByIdAsync(id, ct);
        if (instance != null)
        {
            _db.GearInstances.Remove(instance);
            
            // 检查是否启用内存缓冲
            var enableMemoryBuffering = _configuration.GetValue<bool>("Persistence:EnableMemoryBuffering", false);
            if (enableMemoryBuffering && _memoryManager != null)
            {
                // 使用内存缓冲：从内存中移除
                _memoryManager.Remove(id);
                // 标记 EF Core 实体为删除状态，但不立即保存
                // 由 PersistenceCoordinator 批量保存删除操作
            }
            else
            {
                // 未启用内存缓冲：保持原有的立即保存行为
                await _db.SaveChangesAsync(ct);
            }
        }
    }

    public async Task CreateBatchAsync(IEnumerable<GearInstance> instances, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        foreach (var instance in instances)
        {
            instance.UpdatedAt = now;
        }
        _db.GearInstances.AddRange(instances);
        await _db.SaveChangesAsync(ct);
    }
}
