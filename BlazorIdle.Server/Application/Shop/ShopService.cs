using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Shop;
using BlazorIdle.Server.Domain.Shop.ValueObjects;
using BlazorIdle.Server.Infrastructure.Persistence;
using BlazorIdle.Shared.Models.Shop;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BlazorIdle.Server.Application.Shop;

/// <summary>
/// 商店服务实现
/// </summary>
public class ShopService : IShopService
{
    private readonly GameDbContext _context;
    private readonly IPurchaseValidator _validator;

    public ShopService(GameDbContext context, IPurchaseValidator validator)
    {
        _context = context;
        _validator = validator;
    }

    public async Task<ListShopsResponse> ListShopsAsync(string characterId)
    {
        if (!Guid.TryParse(characterId, out var charGuid))
        {
            return new ListShopsResponse();
        }

        var character = await _context.Characters
            .FirstOrDefaultAsync(c => c.Id == charGuid);

        if (character == null)
        {
            return new ListShopsResponse();
        }

        var shops = await _context.ShopDefinitions
            .Include(s => s.Items)
            .Where(s => s.IsEnabled)
            .OrderBy(s => s.SortOrder)
            .ToListAsync();

        var shopDtos = shops.Select(s => new ShopDto
        {
            Id = s.Id,
            Name = s.Name,
            Type = s.Type.ToString(),
            Icon = s.Icon,
            Description = s.Description,
            IsEnabled = s.IsEnabled,
            ItemCount = s.Items.Count(i => i.IsEnabled),
            IsUnlocked = CheckUnlockCondition(s.UnlockCondition, character.Level)
        }).ToList();

        return new ListShopsResponse { Shops = shopDtos };
    }

    public async Task<ListShopItemsResponse> GetShopItemsAsync(string shopId, string characterId)
    {
        if (!Guid.TryParse(characterId, out var charGuid))
        {
            return new ListShopItemsResponse();
        }

        var character = await _context.Characters
            .FirstOrDefaultAsync(c => c.Id == charGuid);

        if (character == null)
        {
            return new ListShopItemsResponse();
        }

        var items = await _context.ShopItems
            .Where(i => i.ShopId == shopId && i.IsEnabled)
            .OrderBy(i => i.SortOrder)
            .ToListAsync();

        var itemDtos = new List<ShopItemDto>();
        foreach (var item in items)
        {
            var price = item.GetPrice();
            var limit = item.GetPurchaseLimit();
            
            var currentCount = 0;
            if (!limit.IsUnlimited())
            {
                currentCount = await GetCurrentPurchaseCountAsync(charGuid, item.Id, limit);
            }

            var canPurchase = character.Level >= item.MinLevel;
            string? blockReason = null;
            if (!canPurchase)
            {
                blockReason = $"需要等级 {item.MinLevel}";
            }

            itemDtos.Add(new ShopItemDto
            {
                Id = item.Id,
                ShopId = item.ShopId,
                ItemDefinitionId = item.ItemDefinitionId,
                ItemName = item.ItemName,
                ItemIcon = item.ItemIcon,
                Price = new PriceDto
                {
                    CurrencyType = price.CurrencyType.ToString(),
                    CurrencyId = price.CurrencyId,
                    Amount = price.Amount
                },
                PurchaseLimit = new PurchaseLimitDto
                {
                    Type = limit.Type.ToString(),
                    MaxPurchases = limit.MaxPurchases,
                    ResetPeriodSeconds = limit.ResetPeriodSeconds
                },
                StockQuantity = item.StockQuantity,
                MinLevel = item.MinLevel,
                IsEnabled = item.IsEnabled,
                CurrentPurchaseCount = currentCount,
                CanPurchase = canPurchase,
                PurchaseBlockReason = blockReason
            });
        }

        return new ListShopItemsResponse { Items = itemDtos };
    }

    public async Task<PurchaseResponse> PurchaseItemAsync(string characterId, PurchaseRequest request)
    {
        if (!Guid.TryParse(characterId, out var charGuid))
        {
            return new PurchaseResponse
            {
                Success = false,
                Message = "角色 ID 格式错误"
            };
        }

        var character = await _context.Characters
            .FirstOrDefaultAsync(c => c.Id == charGuid);

        if (character == null)
        {
            return new PurchaseResponse
            {
                Success = false,
                Message = "角色不存在"
            };
        }

        var shopItem = await _context.ShopItems
            .Include(i => i.Shop)
            .FirstOrDefaultAsync(i => i.Id == request.ShopItemId);

        if (shopItem == null)
        {
            return new PurchaseResponse
            {
                Success = false,
                Message = "商品不存在"
            };
        }

        // 验证购买
        var validationResult = await _validator.ValidatePurchaseAsync(
            character, shopItem, request.Quantity);
        var isValid = validationResult.isValid;
        var errorMessage = validationResult.errorMessage;

        if (!isValid)
        {
            return new PurchaseResponse
            {
                Success = false,
                Message = errorMessage
            };
        }

        // 执行购买
        var price = shopItem.GetPrice();
        var totalPrice = price.Amount * request.Quantity;

        // 扣除货币
        if (price.CurrencyType == CurrencyType.Gold)
        {
            character.Gold -= totalPrice;
        }

        // 减少库存（如果有限制）
        if (shopItem.StockQuantity >= 0)
        {
            shopItem.StockQuantity -= request.Quantity;
        }

        // 更新购买计数器
        var limit = shopItem.GetPurchaseLimit();
        if (!limit.IsUnlimited())
        {
            await UpdatePurchaseCounterAsync(charGuid, shopItem.Id, request.Quantity, limit);
        }

        // 创建购买记录
        var record = new PurchaseRecord
        {
            Id = Guid.NewGuid().ToString(),
            CharacterId = charGuid,
            ShopId = shopItem.ShopId,
            ShopItemId = shopItem.Id,
            ItemDefinitionId = shopItem.ItemDefinitionId,
            Quantity = request.Quantity,
            PriceJson = JsonSerializer.Serialize(price),
            PurchasedAt = DateTime.UtcNow
        };

        _context.PurchaseRecords.Add(record);

        // TODO: 实际发放物品到库存（需要库存系统支持）
        // await _inventoryService.AddItemAsync(characterId, shopItem.ItemDefinitionId, request.Quantity);

        await _context.SaveChangesAsync();

        return new PurchaseResponse
        {
            Success = true,
            Message = $"购买成功！获得 {shopItem.ItemName} x{request.Quantity}",
            Record = new PurchaseRecordDto
            {
                Id = record.Id,
                CharacterId = record.CharacterId.ToString(),
                ShopId = record.ShopId,
                ShopItemId = record.ShopItemId,
                ItemDefinitionId = record.ItemDefinitionId,
                Quantity = record.Quantity,
                Price = new PriceDto
                {
                    CurrencyType = price.CurrencyType.ToString(),
                    CurrencyId = price.CurrencyId,
                    Amount = price.Amount
                },
                PurchasedAt = record.PurchasedAt
            }
        };
    }

    public async Task<PurchaseHistoryResponse> GetPurchaseHistoryAsync(string characterId, int page = 1, int pageSize = 20)
    {
        if (!Guid.TryParse(characterId, out var charGuid))
        {
            return new PurchaseHistoryResponse();
        }

        var skip = (page - 1) * pageSize;

        var totalCount = await _context.PurchaseRecords
            .Where(r => r.CharacterId == charGuid)
            .CountAsync();

        var records = await _context.PurchaseRecords
            .Where(r => r.CharacterId == charGuid)
            .OrderByDescending(r => r.PurchasedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        var recordDtos = records.Select(r =>
        {
            var price = JsonSerializer.Deserialize<Price>(r.PriceJson) ?? new Price();
            return new PurchaseRecordDto
            {
                Id = r.Id,
                CharacterId = r.CharacterId.ToString(),
                ShopId = r.ShopId,
                ShopItemId = r.ShopItemId,
                ItemDefinitionId = r.ItemDefinitionId,
                Quantity = r.Quantity,
                Price = new PriceDto
                {
                    CurrencyType = price.CurrencyType.ToString(),
                    CurrencyId = price.CurrencyId,
                    Amount = price.Amount
                },
                PurchasedAt = r.PurchasedAt
            };
        }).ToList();

        return new PurchaseHistoryResponse
        {
            Records = recordDtos,
            TotalCount = totalCount
        };
    }

    /// <summary>
    /// 检查解锁条件（简单实现：仅支持等级检查）
    /// </summary>
    private bool CheckUnlockCondition(string? condition, int characterLevel)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            return true;
        }

        // 简单解析 "level>=10" 格式
        if (condition.StartsWith("level>="))
        {
            var requiredLevel = int.Parse(condition.Replace("level>=", ""));
            return characterLevel >= requiredLevel;
        }

        // 默认解锁
        return true;
    }

    /// <summary>
    /// 获取当前购买次数
    /// </summary>
    private async Task<int> GetCurrentPurchaseCountAsync(Guid characterId, string shopItemId, PurchaseLimit limit)
    {
        var counterId = PurchaseCounter.GenerateId(characterId, shopItemId);
        var counter = await _context.PurchaseCounters
            .FirstOrDefaultAsync(c => c.Id == counterId);

        if (counter == null)
        {
            return 0;
        }

        // 检查是否需要重置
        if (limit.Type == LimitType.Daily && counter.ShouldReset(86400))
        {
            return 0;
        }
        if (limit.Type == LimitType.Weekly && counter.ShouldReset(604800))
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

    /// <summary>
    /// 更新购买计数器
    /// </summary>
    private async Task UpdatePurchaseCounterAsync(Guid characterId, string shopItemId, int quantity, PurchaseLimit limit)
    {
        var counterId = PurchaseCounter.GenerateId(characterId, shopItemId);
        var counter = await _context.PurchaseCounters
            .FirstOrDefaultAsync(c => c.Id == counterId);

        if (counter == null)
        {
            counter = new PurchaseCounter
            {
                Id = counterId,
                CharacterId = characterId,
                ShopItemId = shopItemId,
                PurchaseCount = 0,
                PeriodStartAt = DateTime.UtcNow,
                LastPurchasedAt = DateTime.UtcNow
            };
            _context.PurchaseCounters.Add(counter);
        }

        // 检查是否需要重置
        var shouldReset = false;
        if (limit.Type == LimitType.Daily)
        {
            shouldReset = counter.ShouldReset(86400);
        }
        else if (limit.Type == LimitType.Weekly)
        {
            shouldReset = counter.ShouldReset(604800);
        }
        else if (limit.Type == LimitType.CustomPeriod && limit.ResetPeriodSeconds.HasValue)
        {
            shouldReset = counter.ShouldReset(limit.ResetPeriodSeconds.Value);
        }

        if (shouldReset)
        {
            counter.Reset();
        }

        counter.IncrementCount(quantity);
    }
}
