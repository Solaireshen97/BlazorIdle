namespace BlazorIdle.Server.Application.Abstractions;

/// <summary>
/// 持久化协调器接口 - 负责定期批量保存和关闭时强制保存
/// </summary>
public interface IPersistenceCoordinator
{
    /// <summary>
    /// 手动触发立即保存（管理员工具）
    /// </summary>
    /// <param name="ct">取消令牌</param>
    Task TriggerImmediateSaveAsync(CancellationToken ct = default);
    
    /// <summary>
    /// 触发指定类型实体的保存
    /// </summary>
    /// <param name="entityType">实体类型名称</param>
    /// <param name="ct">取消令牌</param>
    Task TriggerSaveAsync(string entityType, CancellationToken ct = default);
    
    /// <summary>
    /// 最终保存 - 关闭时强制保存所有数据
    /// </summary>
    /// <param name="ct">取消令牌</param>
    Task FinalSaveAsync(CancellationToken ct = default);
}
