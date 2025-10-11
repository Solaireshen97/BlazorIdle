using BlazorIdle.Server.Domain.Equipment.Models;

namespace BlazorIdle.Server.Application.Abstractions;

/// <summary>
/// 装备套装仓储接口
/// </summary>
public interface IGearSetRepository
{
    /// <summary>
    /// 根据ID获取套装定义
    /// </summary>
    Task<GearSet?> GetByIdAsync(string id, CancellationToken ct = default);
    
    /// <summary>
    /// 获取所有套装定义
    /// </summary>
    Task<List<GearSet>> GetAllAsync(CancellationToken ct = default);
    
    /// <summary>
    /// 创建套装定义
    /// </summary>
    Task CreateAsync(GearSet gearSet, CancellationToken ct = default);
    
    /// <summary>
    /// 更新套装定义
    /// </summary>
    Task UpdateAsync(GearSet gearSet, CancellationToken ct = default);
    
    /// <summary>
    /// 删除套装定义
    /// </summary>
    Task DeleteAsync(string id, CancellationToken ct = default);
}
