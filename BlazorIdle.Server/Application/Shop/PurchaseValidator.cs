using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Shop;
using BlazorIdle.Server.Domain.Shop.ValueObjects;
using BlazorIdle.Server.Infrastructure.Configuration;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Application.Shop;

/// <summary>
/// 购买验证器实现
/// </summary>
public class PurchaseValidator : IPurchaseValidator
{
    private readonly GameDbContext _context;
    private readonly ShopOptions _shopOptions;
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<PurchaseValidator> _logger;

    public PurchaseValidator(
        GameDbContext context, 
        IOptions<ShopOptions> shopOptions,
        IInventoryService inventoryService,
        ILogger<PurchaseValidator> logger)
    {
        _context = context;
        _shopOptions = shopOptions.Value;
        _inventoryService = inventoryService;
        _logger = logger;
    }

    public async Task<(bool isValid, string? errorMessage)> ValidatePurchaseAsync(
        Character character,
        ShopItem shopItem,
        int quantity)
    {
        _logger.LogDebug("验证购买: CharacterId={CharacterId}, ShopItemId={ShopItemId}, Quantity={Quantity}", 
            character.Id, shopItem.Id, quantity);

        // 1. 验证商品是否启用
        if (!shopItem.IsEnabled)
        {
            _logger.LogWarning("商品已下架: ShopItemId={ShopItemId}", shopItem.Id);
            return (false, "商品已下架");
        }

        // 2. 验证角色等级
        if (character.Level < shopItem.MinLevel)
        {
            _logger.LogDebug("角色等级不足: CharacterLevel={Level}, RequiredLevel={RequiredLevel}", 
                character.Level, shopItem.MinLevel);
            return (false, $"需要等级 {shopItem.MinLevel}");
        }

        // 3. 验证库存
        if (shopItem.StockQuantity >= 0 && shopItem.StockQuantity < quantity)
        {
            _logger.LogWarning("库存不足: ShopItemId={ShopItemId}, Stock={Stock}, Requested={Requested}", 
                shopItem.Id, shopItem.StockQuantity, quantity);
            return (false, "库存不足");
        }

        // 4. 验证购买数量
        if (quantity < _shopOptions.MinPurchaseQuantity)
        {
            return (false, $"购买数量必须至少为 {_shopOptions.MinPurchaseQuantity}");
        }
        
        if (quantity > _shopOptions.MaxPurchaseQuantity)
        {
            return (false, $"单次购买数量不能超过 {_shopOptions.MaxPurchaseQuantity}");
        }

        // 5. 验证价格
        var price = shopItem.GetPrice();
        if (!price.IsValid())
        {
            return (false, "商品价格配置错误");
        }

        // 6. 验证货币是否足够
        var totalPrice = price.Amount * quantity;
        if (price.CurrencyType == CurrencyType.Gold)
        {
            if (character.Gold < totalPrice)
            {
                return (false, $"金币不足，需要 {totalPrice} 金币");
            }
        }
        else if (price.CurrencyType == CurrencyType.Item)
        {
            // 验证物品货币（需要背包中有足够的物品）
            if (string.IsNullOrWhiteSpace(price.CurrencyId))
            {
                return (false, "商品价格配置错误：缺少物品ID");
            }

            var hasEnoughItems = await _inventoryService.HasItemAsync(
                character.Id, 
                price.CurrencyId, 
                totalPrice);

            if (!hasEnoughItems)
            {
                return (false, $"物品不足，需要 {totalPrice} 个 {price.CurrencyId}");
            }
        }

        // 7. 验证购买限制
        var purchaseLimit = shopItem.GetPurchaseLimit();
        if (!purchaseLimit.IsUnlimited())
        {
            var currentCount = await GetCurrentPurchaseCountAsync(character.Id, shopItem.Id, purchaseLimit);
            if (currentCount + quantity > purchaseLimit.MaxPurchases)
            {
                return (false, $"超过购买限制，最多购买 {purchaseLimit.MaxPurchases} 次");
            }
        }

        return (true, null);
    }

    /// <summary>
    /// 获取当前购买次数
    /// </summary>
    private async Task<int> GetCurrentPurchaseCountAsync(Guid characterId, string shopItemId, PurchaseLimit limit)
    {
        var counterId = PurchaseCounter.GenerateId(characterId, shopItemId);
        var counter = await _context.PurchaseCounters
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == counterId);

        if (counter == null)
        {
            return 0;
        }

        // 检查是否需要重置
        if (limit.Type == LimitType.Daily && counter.ShouldReset(_shopOptions.DailyResetSeconds))
        {
            return 0;
        }
        if (limit.Type == LimitType.Weekly && counter.ShouldReset(_shopOptions.WeeklyResetSeconds))
        {
            return 0;
        }
        if (limit.Type == LimitType.CustomPeriod && limit.ResetPeriodSeconds.HasValue)
        {
            if (counter.ShouldReset(limit.ResetPeriodSeconds.Value))
            {
                return 0;
            }
        }

        return counter.PurchaseCount;
    }
}
