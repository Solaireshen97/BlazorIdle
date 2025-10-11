using BlazorIdle.Server.Domain.Equipment.Models;

namespace BlazorIdle.Server.Application.Abstractions;

/// <summary>
/// 词条定义仓储接口
/// </summary>
public interface IAffixRepository
{
    /// <summary>
    /// 根据ID获取词条定义
    /// </summary>
    Task<Affix?> GetByIdAsync(string id, CancellationToken ct = default);
    
    /// <summary>
    /// 获取所有词条定义
    /// </summary>
    Task<List<Affix>> GetAllAsync(CancellationToken ct = default);
    
    /// <summary>
    /// 根据槽位获取可用的词条列表
    /// </summary>
    Task<List<Affix>> GetBySlotAsync(EquipmentSlot slot, CancellationToken ct = default);
    
    /// <summary>
    /// 创建词条定义
    /// </summary>
    Task CreateAsync(Affix affix, CancellationToken ct = default);
    
    /// <summary>
    /// 更新词条定义
    /// </summary>
    Task UpdateAsync(Affix affix, CancellationToken ct = default);
    
    /// <summary>
    /// 删除词条定义
    /// </summary>
    Task DeleteAsync(string id, CancellationToken ct = default);
}
