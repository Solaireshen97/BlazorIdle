namespace BlazorIdle.Server.Application.Abstractions;

/// <summary>
/// 库存服务接口
/// </summary>
public interface IInventoryService
{
    /// <summary>
    /// 添加物品到角色背包
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="itemId">物品ID</param>
    /// <param name="quantity">数量</param>
    /// <returns>操作是否成功</returns>
    Task<bool> AddItemAsync(Guid characterId, string itemId, int quantity);
    
    /// <summary>
    /// 检查角色是否有足够的物品
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="itemId">物品ID</param>
    /// <param name="quantity">所需数量</param>
    /// <returns>是否有足够的物品</returns>
    Task<bool> HasItemAsync(Guid characterId, string itemId, int quantity);
    
    /// <summary>
    /// 从角色背包移除物品
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="itemId">物品ID</param>
    /// <param name="quantity">数量</param>
    /// <returns>操作是否成功</returns>
    Task<bool> RemoveItemAsync(Guid characterId, string itemId, int quantity);
}
