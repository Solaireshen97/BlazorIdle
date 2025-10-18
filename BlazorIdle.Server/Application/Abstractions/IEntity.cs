namespace BlazorIdle.Server.Application.Abstractions;

/// <summary>
/// 实体接口 - 所有需要内存管理的实体必须实现此接口
/// </summary>
public interface IEntity
{
    /// <summary>
    /// 实体唯一标识符
    /// </summary>
    Guid Id { get; set; }
}
