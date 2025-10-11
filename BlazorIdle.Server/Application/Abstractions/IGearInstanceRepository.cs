using BlazorIdle.Server.Domain.Equipment.Models;

namespace BlazorIdle.Server.Application.Abstractions;

/// <summary>
/// 装备实例仓储接口
/// </summary>
public interface IGearInstanceRepository
{
    /// <summary>
    /// 根据ID获取装备实例
    /// </summary>
    Task<GearInstance?> GetByIdAsync(Guid id, CancellationToken ct = default);
    
    /// <summary>
    /// 获取角色已装备的装备列表
    /// </summary>
    Task<List<GearInstance>> GetEquippedGearAsync(Guid characterId, CancellationToken ct = default);
    
    /// <summary>
    /// 获取角色的所有装备（包括背包中的）
    /// </summary>
    Task<List<GearInstance>> GetGearByCharacterAsync(Guid characterId, CancellationToken ct = default);
    
    /// <summary>
    /// 获取角色指定槽位的装备
    /// </summary>
    Task<GearInstance?> GetEquippedGearBySlotAsync(Guid characterId, EquipmentSlot slot, CancellationToken ct = default);
    
    /// <summary>
    /// 创建装备实例
    /// </summary>
    Task CreateAsync(GearInstance instance, CancellationToken ct = default);
    
    /// <summary>
    /// 更新装备实例
    /// </summary>
    Task UpdateAsync(GearInstance instance, CancellationToken ct = default);
    
    /// <summary>
    /// 删除装备实例
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    
    /// <summary>
    /// 批量创建装备实例
    /// </summary>
    Task CreateBatchAsync(IEnumerable<GearInstance> instances, CancellationToken ct = default);
}
