using BlazorIdle.Server.Domain.Equipment.Models;

namespace BlazorIdle.Server.Application.Abstractions;

/// <summary>
/// 词条仓储接口 - 管理装备词条配置
/// </summary>
public interface IAffixRepository
{
    /// <summary>根据ID获取词条</summary>
    Task<Affix?> GetByIdAsync(string id, CancellationToken ct = default);
    
    /// <summary>根据ID列表获取词条列表</summary>
    Task<List<Affix>> GetByIdsAsync(List<string> ids, CancellationToken ct = default);
    
    /// <summary>获取所有词条</summary>
    Task<List<Affix>> GetAllAsync(CancellationToken ct = default);
    
    /// <summary>创建词条</summary>
    Task CreateAsync(Affix affix, CancellationToken ct = default);
    
    /// <summary>更新词条</summary>
    Task UpdateAsync(Affix affix, CancellationToken ct = default);
    
    /// <summary>删除词条</summary>
    Task DeleteAsync(string id, CancellationToken ct = default);
}
