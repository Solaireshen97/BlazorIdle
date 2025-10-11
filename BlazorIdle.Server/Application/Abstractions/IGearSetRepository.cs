using BlazorIdle.Server.Domain.Equipment.Models;

namespace BlazorIdle.Server.Application.Abstractions;

/// <summary>
/// 套装仓储接口 - 管理装备套装配置
/// </summary>
public interface IGearSetRepository
{
    /// <summary>根据ID获取套装</summary>
    Task<GearSet?> GetByIdAsync(string id, CancellationToken ct = default);
    
    /// <summary>获取所有套装</summary>
    Task<List<GearSet>> GetAllAsync(CancellationToken ct = default);
    
    /// <summary>创建套装</summary>
    Task CreateAsync(GearSet gearSet, CancellationToken ct = default);
    
    /// <summary>更新套装</summary>
    Task UpdateAsync(GearSet gearSet, CancellationToken ct = default);
    
    /// <summary>删除套装</summary>
    Task DeleteAsync(string id, CancellationToken ct = default);
}
