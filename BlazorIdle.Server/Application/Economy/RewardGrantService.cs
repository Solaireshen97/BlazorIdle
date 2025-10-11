using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Server.Domain.Records;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorIdle.Server.Application.Economy;

/// <summary>
/// 奖励发放服务实现：负责将金币/经验/物品发放到角色账户。
/// 使用 EconomyEventRecord 实现幂等性，防止重复发放。
/// </summary>
public class RewardGrantService : IRewardGrantService
{
    private readonly GameDbContext _db;
    private readonly ILogger<RewardGrantService> _logger;
    private readonly GearDropService? _gearDropService;

    public RewardGrantService(
        GameDbContext db, 
        ILogger<RewardGrantService> logger,
        GearDropService? gearDropService = null)
    {
        _db = db;
        _logger = logger;
        _gearDropService = gearDropService;
    }

    public async Task<bool> IsAlreadyGrantedAsync(string idempotencyKey, CancellationToken ct = default)
    {
        return await _db.EconomyEvents
            .AnyAsync(e => e.IdempotencyKey == idempotencyKey, ct);
    }

    public async Task<bool> GrantRewardsAsync(
        Guid characterId,
        long gold,
        long exp,
        Dictionary<string, int> items,
        string idempotencyKey,
        string eventType,
        Guid? battleId = null,
        CancellationToken ct = default)
    {
        // 幂等性检查
        if (await IsAlreadyGrantedAsync(idempotencyKey, ct))
        {
            _logger.LogDebug("Reward already granted for key: {Key}", idempotencyKey);
            return false;
        }

        // 开始事务
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // 1. 更新角色金币和经验
            var character = await _db.Characters.FindAsync(new object[] { characterId }, ct);
            if (character == null)
            {
                _logger.LogWarning("Character {CharacterId} not found, skipping reward grant", characterId);
                return false;
            }

            character.Gold += gold;
            character.Experience += exp;

            // 2. 处理装备掉落（如果启用）
            Dictionary<string, int> regularItems = items;
            if (_gearDropService != null && items.Count > 0)
            {
                try
                {
                    var (gearInstances, remaining) = await _gearDropService.ProcessItemDropsAsync(
                        items,
                        characterId,
                        character.Level);

                    if (gearInstances.Count > 0)
                    {
                        _logger.LogInformation(
                            "Generated {GearCount} gear instances for character {CharacterId}",
                            gearInstances.Count, characterId);
                    }

                    regularItems = remaining;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process gear drops for character {CharacterId}", characterId);
                    // 如果装备生成失败，继续处理普通物品
                }
            }

            // 3. 更新背包物品（仅处理非装备物品）
            foreach (var (itemId, quantity) in regularItems.Where(kv => kv.Value > 0))
            {
                var existing = await _db.InventoryItems
                    .FirstOrDefaultAsync(i => i.CharacterId == characterId && i.ItemId == itemId, ct);

                if (existing != null)
                {
                    existing.Quantity += quantity;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    var newItem = new InventoryItem
                    {
                        Id = Guid.NewGuid(),
                        CharacterId = characterId,
                        ItemId = itemId,
                        Quantity = quantity,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _db.InventoryItems.Add(newItem);
                }
            }

            // 4. 记录经济事件（幂等性记录）
            var economyEvent = new EconomyEventRecord
            {
                Id = Guid.NewGuid(),
                CharacterId = characterId,
                BattleId = battleId,
                EventType = eventType,
                IdempotencyKey = idempotencyKey,
                Gold = gold,
                Exp = exp,
                ItemsJson = items.Count > 0 ? JsonSerializer.Serialize(items) : null,
                CreatedAt = DateTime.UtcNow
            };
            _db.EconomyEvents.Add(economyEvent);

            // 5. 保存所有更改
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Granted rewards to character {CharacterId}: Gold={Gold}, Exp={Exp}, Items={ItemCount}",
                characterId, gold, exp, items.Count);

            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Failed to grant rewards for key: {Key}", idempotencyKey);
            throw;
        }
    }
}
