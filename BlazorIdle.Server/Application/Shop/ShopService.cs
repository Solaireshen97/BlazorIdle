using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Shop;
using BlazorIdle.Server.Domain.Shop.ValueObjects;
using BlazorIdle.Server.Infrastructure.Persistence;
using BlazorIdle.Shared.Models.Shop;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorIdle.Server.Application.Shop;

/// <summary>
/// 商店服务实现
/// </summary>
public class ShopService : IShopService
{
    private readonly GameDbContext _db;
    private readonly IPurchaseValidator _validator;
    private readonly ILogger<ShopService> _logger;

    public ShopService(
        GameDbContext db, 
        IPurchaseValidator validator,
        ILogger<ShopService> logger)
    {
        _db = db;
        _validator = validator;
        _logger = logger;
    }

    public async Task<ListShopsResponse> ListShopsAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        var character = await _db.Characters.FindAsync(new object[] { characterId }, cancellationToken);
        if (character == null)
        {
            _logger.LogWarning("Character {CharacterId} not found", characterId);
            return new ListShopsResponse(new List<ShopDto>());
        }

        var shops = await _db.ShopDefinitions
            .Include(s => s.Items)
            .Where(s => s.IsEnabled)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(cancellationToken);

        var shopDtos = shops.Select(s => new ShopDto(
            s.Id,
            s.Name,
            s.Type.ToString(),
            s.Icon,
            s.Description,
            s.IsEnabled,
            IsShopUnlocked(s, character),
            s.Items.Count(i => i.IsEnabled)
        )).ToList();

        return new ListShopsResponse(shopDtos);
    }

    public async Task<ListShopItemsResponse> ListShopItemsAsync(
        string shopId, 
        Guid characterId, 
        CancellationToken cancellationToken = default)
    {
        var character = await _db.Characters.FindAsync(new object[] { characterId }, cancellationToken);
        if (character == null)
        {
            _logger.LogWarning("Character {CharacterId} not found", characterId);
            return new ListShopItemsResponse(shopId, "", new List<ShopItemDto>());
        }

        var shop = await _db.ShopDefinitions
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == shopId, cancellationToken);

        if (shop == null)
        {
            _logger.LogWarning("Shop {ShopId} not found", shopId);
            return new ListShopItemsResponse(shopId, "", new List<ShopItemDto>());
        }

        var items = shop.Items
            .Where(i => i.IsEnabled)
            .OrderBy(i => i.SortOrder)
            .ToList();

        // 获取购买计数
        var itemIds = items.Select(i => i.Id).ToList();
        var now = DateTime.UtcNow;
        var counters = await _db.PurchaseCounters
            .Where(c => c.CharacterId == characterId && itemIds.Contains(c.ShopItemId))
            .ToListAsync(cancellationToken);

        var itemDtos = new List<ShopItemDto>();
        foreach (var item in items)
        {
            var remainingPurchases = await CalculateRemainingPurchasesAsync(
                item, 
                characterId, 
                counters, 
                cancellationToken);

            itemDtos.Add(new ShopItemDto(
                item.Id,
                item.ShopId,
                item.ItemType.ToString(),
                item.ItemDefinitionId,
                item.DisplayName ?? item.ItemDefinitionId,
                item.Icon ?? "📦",
                item.Description ?? "",
                MapPrice(item.Price),
                item.PurchaseLimit != null ? MapPurchaseLimit(item.PurchaseLimit) : null,
                item.RequiredLevel,
                item.IsEnabled,
                IsItemUnlocked(item, character),
                item.HasStock(),
                item.StockLimit == -1 ? null : item.CurrentStock,
                remainingPurchases
            ));
        }

        return new ListShopItemsResponse(shopId, shop.Name, itemDtos);
    }

    public async Task<PurchaseResponse> PurchaseItemAsync(
        PurchaseRequest request, 
        CancellationToken cancellationToken = default)
    {
        // 开始事务
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // 1. 获取角色
            var character = await _db.Characters.FindAsync(new object[] { request.CharacterId }, cancellationToken);
            if (character == null)
            {
                return new PurchaseResponse(false, "角色不存在", null, null, null, null);
            }

            // 2. 获取商品
            var shopItem = await _db.ShopItems
                .Include(i => i.Shop)
                .FirstOrDefaultAsync(i => i.Id == request.ShopItemId, cancellationToken);

            if (shopItem == null)
            {
                return new PurchaseResponse(false, "商品不存在", null, null, null, null);
            }

            // 3. 验证购买
            var (isValid, errorMessage) = await _validator.ValidatePurchaseAsync(
                character, 
                shopItem, 
                request.Quantity, 
                cancellationToken);

            if (!isValid)
            {
                return new PurchaseResponse(false, errorMessage!, null, null, null, null);
            }

            // 4. 扣除货币/物品
            var price = shopItem.Price;
            int goldPaid = 0;
            string? itemPaidId = null;
            int itemPaidQuantity = 0;

            switch (price.CurrencyType)
            {
                case CurrencyType.Gold:
                    goldPaid = price.Amount * request.Quantity;
                    character.Gold -= goldPaid;
                    break;

                case CurrencyType.ItemExchange:
                    itemPaidId = price.ItemId;
                    itemPaidQuantity = price.ItemQuantity * request.Quantity;
                    var inventoryItem = await _db.InventoryItems
                        .FirstOrDefaultAsync(i => 
                            i.CharacterId == character.Id && 
                            i.ItemId == price.ItemId, 
                            cancellationToken);

                    if (inventoryItem != null)
                    {
                        inventoryItem.Quantity -= itemPaidQuantity;
                        inventoryItem.UpdatedAt = DateTime.UtcNow;
                    }
                    break;
            }

            // 5. 减少库存
            if (!shopItem.DecreaseStock(request.Quantity))
            {
                return new PurchaseResponse(false, "扣减库存失败", null, null, null, null);
            }

            // 6. 添加物品到背包
            var purchasedItem = await _db.InventoryItems
                .FirstOrDefaultAsync(i => 
                    i.CharacterId == character.Id && 
                    i.ItemId == shopItem.ItemDefinitionId, 
                    cancellationToken);

            if (purchasedItem != null)
            {
                purchasedItem.Quantity += request.Quantity;
                purchasedItem.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _db.InventoryItems.Add(new InventoryItem
                {
                    Id = Guid.NewGuid(),
                    CharacterId = character.Id,
                    ItemId = shopItem.ItemDefinitionId,
                    Quantity = request.Quantity,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            // 7. 更新购买计数
            if (shopItem.PurchaseLimit != null)
            {
                await UpdatePurchaseCounterAsync(
                    character.Id, 
                    shopItem, 
                    request.Quantity, 
                    cancellationToken);
            }

            // 8. 创建购买记录
            var purchaseRecord = new PurchaseRecord
            {
                Id = Guid.NewGuid(),
                CharacterId = character.Id,
                ShopId = shopItem.ShopId,
                ShopItemId = shopItem.Id,
                ItemDefinitionId = shopItem.ItemDefinitionId,
                Quantity = request.Quantity,
                GoldPaid = goldPaid,
                ItemPaidId = itemPaidId,
                ItemPaidQuantity = itemPaidQuantity,
                PurchasedAt = DateTime.UtcNow
            };
            _db.PurchaseRecords.Add(purchaseRecord);

            // 9. 保存更改
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // 10. 计算剩余数据
            var remainingPurchases = await CalculateRemainingPurchasesAsync(
                shopItem, 
                character.Id, 
                null, 
                cancellationToken);

            return new PurchaseResponse(
                true,
                "购买成功",
                purchaseRecord.Id,
                (int)character.Gold,
                shopItem.StockLimit == -1 ? null : shopItem.CurrentStock,
                remainingPurchases
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Purchase failed for character {CharacterId}, item {ItemId}", 
                request.CharacterId, request.ShopItemId);
            await transaction.RollbackAsync(cancellationToken);
            return new PurchaseResponse(false, "购买失败，请稍后重试", null, null, null, null);
        }
    }

    public async Task<PurchaseHistoryResponse> GetPurchaseHistoryAsync(
        Guid characterId, 
        int pageNumber = 1, 
        int pageSize = 20, 
        CancellationToken cancellationToken = default)
    {
        var query = _db.PurchaseRecords
            .Where(r => r.CharacterId == characterId)
            .OrderByDescending(r => r.PurchasedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var records = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var shopIds = records.Select(r => r.ShopId).Distinct().ToList();
        var shops = await _db.ShopDefinitions
            .Where(s => shopIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        var historyDtos = records.Select(r => new PurchaseHistoryDto(
            r.Id,
            r.ShopId,
            shops.TryGetValue(r.ShopId, out var shopName) ? shopName : r.ShopId,
            r.ItemDefinitionId,
            r.ItemDefinitionId, // TODO: 可以从物品定义中获取真实名称
            r.Quantity,
            r.GoldPaid,
            r.ItemPaidId,
            r.ItemPaidQuantity,
            r.PurchasedAt
        )).ToList();

        return new PurchaseHistoryResponse(historyDtos, totalCount, pageNumber, pageSize);
    }

    // Helper methods
    private bool IsShopUnlocked(ShopDefinition shop, Character character)
    {
        // TODO: 实现解锁条件检查
        return true;
    }

    private bool IsItemUnlocked(ShopItem item, Character character)
    {
        // TODO: 实现解锁条件检查
        return character.Level >= item.RequiredLevel;
    }

    private PriceDto MapPrice(Price price)
    {
        return new PriceDto(
            price.CurrencyType.ToString(),
            price.Amount,
            price.ItemId,
            price.ItemQuantity
        );
    }

    private PurchaseLimitDto MapPurchaseLimit(PurchaseLimit limit)
    {
        return new PurchaseLimitDto(
            limit.LimitType.ToString(),
            limit.MaxPurchases,
            limit.ResetPeriodSeconds
        );
    }

    private async Task<int?> CalculateRemainingPurchasesAsync(
        ShopItem item,
        Guid characterId,
        List<PurchaseCounter>? existingCounters,
        CancellationToken cancellationToken)
    {
        if (item.PurchaseLimit == null)
        {
            return null;
        }

        var limit = item.PurchaseLimit;
        var now = DateTime.UtcNow;

        // 计算周期开始时间
        DateTime periodStart = limit.LimitType switch
        {
            LimitType.Daily => now.Date,
            LimitType.Weekly => now.Date.AddDays(-(int)now.DayOfWeek),
            LimitType.Monthly => new DateTime(now.Year, now.Month, 1),
            LimitType.Custom => limit.ResetPeriodSeconds.HasValue 
                ? now.AddSeconds(-limit.ResetPeriodSeconds.Value)
                : DateTime.MinValue,
            LimitType.Total => DateTime.MinValue,
            _ => DateTime.MinValue
        };

        // 获取购买计数
        PurchaseCounter? counter;
        if (existingCounters != null)
        {
            counter = existingCounters.FirstOrDefault(c => 
                c.ShopItemId == item.Id && 
                c.PeriodStart >= periodStart);
        }
        else
        {
            counter = await _db.PurchaseCounters
                .FirstOrDefaultAsync(c => 
                    c.CharacterId == characterId && 
                    c.ShopItemId == item.Id && 
                    c.PeriodStart >= periodStart,
                    cancellationToken);
        }

        var currentPurchases = counter?.PurchaseCount ?? 0;
        return limit.MaxPurchases - currentPurchases;
    }

    private async Task UpdatePurchaseCounterAsync(
        Guid characterId,
        ShopItem shopItem,
        int quantity,
        CancellationToken cancellationToken)
    {
        var limit = shopItem.PurchaseLimit!;
        var now = DateTime.UtcNow;

        // 计算周期开始时间
        DateTime periodStart = limit.LimitType switch
        {
            LimitType.Daily => now.Date,
            LimitType.Weekly => now.Date.AddDays(-(int)now.DayOfWeek),
            LimitType.Monthly => new DateTime(now.Year, now.Month, 1),
            LimitType.Custom => limit.ResetPeriodSeconds.HasValue 
                ? now.AddSeconds(-limit.ResetPeriodSeconds.Value)
                : now,
            LimitType.Total => DateTime.MinValue,
            _ => now
        };

        // 计算周期结束时间
        DateTime? periodEnd = limit.LimitType switch
        {
            LimitType.Daily => now.Date.AddDays(1),
            LimitType.Weekly => now.Date.AddDays(7 - (int)now.DayOfWeek),
            LimitType.Monthly => new DateTime(now.Year, now.Month, 1).AddMonths(1),
            LimitType.Custom => limit.ResetPeriodSeconds.HasValue 
                ? now.AddSeconds(limit.ResetPeriodSeconds.Value)
                : null,
            LimitType.Total => null,
            _ => null
        };

        var counter = await _db.PurchaseCounters
            .FirstOrDefaultAsync(c => 
                c.CharacterId == characterId && 
                c.ShopItemId == shopItem.Id && 
                c.PeriodStart >= periodStart,
                cancellationToken);

        if (counter != null)
        {
            counter.PurchaseCount += quantity;
            counter.UpdatedAt = now;
        }
        else
        {
            _db.PurchaseCounters.Add(new PurchaseCounter
            {
                Id = Guid.NewGuid(),
                CharacterId = characterId,
                ShopItemId = shopItem.Id,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                PurchaseCount = quantity,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
    }
}
