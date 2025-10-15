using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Characters;
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

    public RewardGrantService(GameDbContext db, ILogger<RewardGrantService> logger)
    {
        _db = db;
        _logger = logger;
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
        _logger.LogInformation(
            "奖励发放开始，CharacterId={CharacterId}, EventType={EventType}, Gold={Gold}, Exp={Exp}, ItemCount={ItemCount}, IdempotencyKey={IdempotencyKey}",
            characterId, eventType, gold, exp, items.Count, idempotencyKey);

        // 幂等性检查
        if (await IsAlreadyGrantedAsync(idempotencyKey, ct))
        {
            _logger.LogDebug("奖励已发放（幂等性检查），IdempotencyKey={IdempotencyKey}", idempotencyKey);
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
                _logger.LogWarning("角色不存在，CharacterId={CharacterId}，跳过奖励发放", characterId);
                return false;
            }

            var oldGold = character.Gold;
            var oldExp = character.Experience;

            character.Gold += gold;
            character.Experience += exp;

            _logger.LogDebug(
                "金币经验变更，CharacterId={CharacterId}, OldGold={OldGold}, NewGold={NewGold}, OldExp={OldExp}, NewExp={NewExp}",
                characterId, oldGold, character.Gold, oldExp, character.Experience);

            // 2. 更新背包物品
            foreach (var (itemId, quantity) in items.Where(kv => kv.Value > 0))
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

            // 3. 记录经济事件（幂等性记录）
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

            // 4. 保存所有更改
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "奖励发放完成，CharacterId={CharacterId}, EventType={EventType}, Gold={Gold}, Exp={Exp}, ItemCount={ItemCount}",
                characterId, eventType, gold, exp, items.Count);

            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, 
                "奖励发放失败，CharacterId={CharacterId}, EventType={EventType}, IdempotencyKey={IdempotencyKey}", 
                characterId, eventType, idempotencyKey);
            throw;
        }
    }
}
