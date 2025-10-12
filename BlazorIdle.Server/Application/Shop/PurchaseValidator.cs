using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Shop;
using BlazorIdle.Server.Domain.Shop.ValueObjects;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorIdle.Server.Application.Shop;

/// <summary>
/// 购买验证器实现
/// </summary>
public class PurchaseValidator : IPurchaseValidator
{
    private readonly GameDbContext _db;
    private readonly ILogger<PurchaseValidator> _logger;

    public PurchaseValidator(GameDbContext db, ILogger<PurchaseValidator> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<(bool IsValid, string? ErrorMessage)> ValidatePurchaseAsync(
        Character character,
        ShopItem shopItem,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        // 1. 检查商品是否启用
        if (!shopItem.IsEnabled)
        {
            return (false, "该商品当前不可购买");
        }

        // 2. 检查等级要求
        if (character.Level < shopItem.RequiredLevel)
        {
            return (false, $"需要等级 {shopItem.RequiredLevel}");
        }

        // 3. 检查库存
        if (!shopItem.HasStock())
        {
            return (false, "库存不足");
        }

        if (shopItem.StockLimit != -1 && shopItem.CurrentStock < quantity)
        {
            return (false, $"库存不足，仅剩 {shopItem.CurrentStock} 个");
        }

        // 4. 检查价格和货币
        var price = shopItem.Price;
        switch (price.CurrencyType)
        {
            case CurrencyType.Gold:
                var totalPrice = price.Amount * quantity;
                if (character.Gold < totalPrice)
                {
                    return (false, $"金币不足，需要 {totalPrice} 金币");
                }
                break;

            case CurrencyType.ItemExchange:
                if (string.IsNullOrEmpty(price.ItemId))
                {
                    return (false, "商品配置错误：缺少交换物品ID");
                }

                var requiredQuantity = price.ItemQuantity * quantity;
                var inventoryItem = await _db.InventoryItems
                    .FirstOrDefaultAsync(i => 
                        i.CharacterId == character.Id && 
                        i.ItemId == price.ItemId, 
                        cancellationToken);

                if (inventoryItem == null || inventoryItem.Quantity < requiredQuantity)
                {
                    return (false, $"所需物品不足，需要 {requiredQuantity} 个 {price.ItemId}");
                }
                break;

            case CurrencyType.SpecialCurrency:
                return (false, "暂不支持特殊货币购买");

            default:
                return (false, "未知的货币类型");
        }

        // 5. 检查购买限制
        if (shopItem.PurchaseLimit != null)
        {
            var limit = shopItem.PurchaseLimit;
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

            // 查询购买计数
            var counter = await _db.PurchaseCounters
                .FirstOrDefaultAsync(c => 
                    c.CharacterId == character.Id && 
                    c.ShopItemId == shopItem.Id && 
                    c.PeriodStart >= periodStart,
                    cancellationToken);

            var currentPurchases = counter?.PurchaseCount ?? 0;
            if (currentPurchases + quantity > limit.MaxPurchases)
            {
                var remaining = limit.MaxPurchases - currentPurchases;
                return (false, $"超过购买限制，本周期还可购买 {remaining} 次");
            }
        }

        return (true, null);
    }
}
