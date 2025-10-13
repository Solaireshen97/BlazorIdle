using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorIdle.Server.Application.Inventory;

/// <summary>
/// 库存服务实现
/// </summary>
public class InventoryService : IInventoryService
{
    private readonly GameDbContext _context;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(
        GameDbContext context,
        ILogger<InventoryService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 添加物品到角色背包
    /// 注意：此方法不会自动保存更改，调用者需要调用 SaveChangesAsync
    /// </summary>
    public async Task<bool> AddItemAsync(Guid characterId, string itemId, int quantity)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            _logger.LogWarning("尝试添加无效的物品ID到角色 {CharacterId} 的背包", characterId);
            return false;
        }

        if (quantity <= 0)
        {
            _logger.LogWarning("尝试添加无效数量 {Quantity} 的物品 {ItemId} 到角色 {CharacterId} 的背包", 
                quantity, itemId, characterId);
            return false;
        }

        try
        {
            // 检查角色是否存在
            var characterExists = await _context.Characters
                .AnyAsync(c => c.Id == characterId);

            if (!characterExists)
            {
                _logger.LogWarning("角色 {CharacterId} 不存在", characterId);
                return false;
            }

            // 查找现有的物品记录
            var existingItem = await _context.InventoryItems
                .FirstOrDefaultAsync(i => i.CharacterId == characterId && i.ItemId == itemId);

            if (existingItem != null)
            {
                // 物品已存在，增加数量
                existingItem.Quantity += quantity;
                existingItem.UpdatedAt = DateTime.UtcNow;
                
                _logger.LogInformation("为角色 {CharacterId} 增加物品 {ItemId} 数量 {Quantity}，新总量: {NewQuantity}", 
                    characterId, itemId, quantity, existingItem.Quantity);
            }
            else
            {
                // 物品不存在，创建新记录
                var newItem = new InventoryItem
                {
                    Id = Guid.NewGuid(),
                    CharacterId = characterId,
                    ItemId = itemId,
                    Quantity = quantity,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.InventoryItems.Add(newItem);
                
                _logger.LogInformation("为角色 {CharacterId} 添加新物品 {ItemId} 数量 {Quantity}", 
                    characterId, itemId, quantity);
            }

            // 不在这里保存，由调用者控制事务
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加物品到背包时发生错误。角色: {CharacterId}, 物品: {ItemId}, 数量: {Quantity}", 
                characterId, itemId, quantity);
            return false;
        }
    }

    /// <summary>
    /// 检查角色是否有足够的物品
    /// </summary>
    public async Task<bool> HasItemAsync(Guid characterId, string itemId, int quantity)
    {
        if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
        {
            return false;
        }

        var item = await _context.InventoryItems
            .FirstOrDefaultAsync(i => i.CharacterId == characterId && i.ItemId == itemId);

        return item != null && item.Quantity >= quantity;
    }

    /// <summary>
    /// 从角色背包移除物品
    /// 注意：此方法不会自动保存更改，调用者需要调用 SaveChangesAsync
    /// </summary>
    public async Task<bool> RemoveItemAsync(Guid characterId, string itemId, int quantity)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            _logger.LogWarning("尝试移除无效的物品ID从角色 {CharacterId} 的背包", characterId);
            return false;
        }

        if (quantity <= 0)
        {
            _logger.LogWarning("尝试移除无效数量 {Quantity} 的物品 {ItemId} 从角色 {CharacterId} 的背包", 
                quantity, itemId, characterId);
            return false;
        }

        try
        {
            var item = await _context.InventoryItems
                .FirstOrDefaultAsync(i => i.CharacterId == characterId && i.ItemId == itemId);

            if (item == null)
            {
                _logger.LogWarning("角色 {CharacterId} 的背包中没有物品 {ItemId}", characterId, itemId);
                return false;
            }

            if (item.Quantity < quantity)
            {
                _logger.LogWarning("角色 {CharacterId} 的物品 {ItemId} 数量不足。需要: {Required}, 拥有: {Available}", 
                    characterId, itemId, quantity, item.Quantity);
                return false;
            }

            item.Quantity -= quantity;
            item.UpdatedAt = DateTime.UtcNow;

            // 如果数量为0，删除记录
            if (item.Quantity == 0)
            {
                _context.InventoryItems.Remove(item);
                _logger.LogInformation("从角色 {CharacterId} 的背包中移除物品 {ItemId}（数量归零）", 
                    characterId, itemId);
            }
            else
            {
                _logger.LogInformation("从角色 {CharacterId} 的背包中减少物品 {ItemId} 数量 {Quantity}，剩余: {Remaining}", 
                    characterId, itemId, quantity, item.Quantity);
            }

            // 不在这里保存，由调用者控制事务
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从背包移除物品时发生错误。角色: {CharacterId}, 物品: {ItemId}, 数量: {Quantity}", 
                characterId, itemId, quantity);
            return false;
        }
    }
}
