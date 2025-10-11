using BlazorIdle.Server.Domain.Equipment.Models;

namespace BlazorIdle.Server.Application.Abstractions;

/// <summary>
/// 装备实例仓储接口 - 管理角色拥有的装备实例
/// </summary>
public interface IGearInstanceRepository
{
    /// <summary>根据ID获取装备实例</summary>
    Task<GearInstance?> GetByIdAsync(Guid id, CancellationToken ct = default);
    
    /// <summary>获取角色所有装备（包括已装备和背包）</summary>
    Task<List<GearInstance>> GetByCharacterIdAsync(Guid characterId, CancellationToken ct = default);
    
    /// <summary>获取角色已装备的装备</summary>
    Task<List<GearInstance>> GetEquippedGearAsync(Guid characterId, CancellationToken ct = default);
    
    /// <summary>获取角色指定槽位的装备</summary>
    Task<GearInstance?> GetEquippedGearBySlotAsync(Guid characterId, EquipmentSlot slot, CancellationToken ct = default);
    
    /// <summary>创建装备实例</summary>
    Task CreateAsync(GearInstance instance, CancellationToken ct = default);
    
    /// <summary>更新装备实例</summary>
    Task UpdateAsync(GearInstance instance, CancellationToken ct = default);
    
    /// <summary>删除装备实例</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    
    /// <summary>保存更改</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
