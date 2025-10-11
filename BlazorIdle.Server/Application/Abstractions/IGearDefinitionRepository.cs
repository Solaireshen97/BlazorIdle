using BlazorIdle.Server.Domain.Equipment.Models;

namespace BlazorIdle.Server.Application.Abstractions;

/// <summary>
/// 装备定义仓储接口
/// </summary>
public interface IGearDefinitionRepository
{
    /// <summary>
    /// 根据ID获取装备定义
    /// </summary>
    Task<GearDefinition?> GetByIdAsync(string id, CancellationToken ct = default);
    
    /// <summary>
    /// 根据槽位获取装备定义列表
    /// </summary>
    Task<List<GearDefinition>> GetBySlotAsync(EquipmentSlot slot, CancellationToken ct = default);
    
    /// <summary>
    /// 获取所有装备定义
    /// </summary>
    Task<List<GearDefinition>> GetAllAsync(CancellationToken ct = default);
    
    /// <summary>
    /// 创建装备定义
    /// </summary>
    Task CreateAsync(GearDefinition definition, CancellationToken ct = default);
    
    /// <summary>
    /// 更新装备定义
    /// </summary>
    Task UpdateAsync(GearDefinition definition, CancellationToken ct = default);
    
    /// <summary>
    /// 删除装备定义
    /// </summary>
    Task DeleteAsync(string id, CancellationToken ct = default);
}
