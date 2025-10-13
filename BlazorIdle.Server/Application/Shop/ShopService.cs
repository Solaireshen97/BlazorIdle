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
    private readonly IShopCacheService _cacheService;
    private readonly IInventoryService _inventoryService;
    private readonly Infrastructure.Configuration.ShopOptions _shopOptions;
    private readonly ILogger<ShopService> _logger;

    public ShopService(
        GameDbContext context, 
        IPurchaseValidator validator,
        IShopCacheService cacheService,
        IInventoryService inventoryService,
        Microsoft.Extensions.Options.IOptions<Infrastructure.Configuration.ShopOptions> shopOptions,
        ILogger<ShopService> logger)
    {
        _context = context;
        _validator = validator;
        _cacheService = cacheService;
        _inventoryService = inventoryService;
        _shopOptions = shopOptions.Value;
        _logger = logger;
    }

    public async Task<ListShopsResponse> ListShopsAsync(string characterId)
    {
        if (!Guid.TryParse(characterId, out var charGuid))
        {
            return new ListShopsResponse();
        }

        var character = await _context.Characters
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == charGuid);

        if (character == null)
        {
            return new ListShopsResponse();
        }

        // 尝试从缓存获取商店列表
        var shops = await _cacheService.GetShopsAsync();
        
        if (shops == null)
        {
            // 缓存未命中，从数据库加载
            shops = await _context.ShopDefinitions
                .AsNoTracking()
                .Include(s => s.Items)
                .Where(s => s.IsEnabled)
                .OrderBy(s => s.SortOrder)
                .ToListAsync();
            
            // 将结果缓存
            _cacheService.SetShops(shops);
        }

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
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == charGuid);

        if (character == null)
        {
            return new ListShopItemsResponse();
        }

        // 尝试从缓存获取商品列表
        var items = await _cacheService.GetShopItemsAsync(shopId);
        
        if (items == null)
        {
            // 缓存未命中，从数据库加载
            items = await _context.ShopItems
                .Where(i => i.ShopId == shopId && i.IsEnabled)
                .OrderBy(i => i.SortOrder)
                .ToListAsync();
            
            // 将结果缓存
            _cacheService.SetShopItems(shopId, items);
        }

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
                ItemCategory = item.ItemCategory,
                Rarity = item.Rarity,
                IsEnabled = item.IsEnabled,
                CurrentPurchaseCount = currentCount,
                CanPurchase = canPurchase,
                PurchaseBlockReason = blockReason
            });
        }

        return new ListShopItemsResponse { Items = itemDtos };
    }

    public async Task<ListShopItemsResponse> GetShopItemsWithFilterAsync(string characterId, ShopItemFilterRequest filter)
    {
        if (!Guid.TryParse(characterId, out var charGuid))
        {
            return new ListShopItemsResponse();
        }

        var character = await _context.Characters
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == charGuid);

        if (character == null)
        {
            return new ListShopItemsResponse();
        }

        // 尝试从缓存获取商品列表
        var items = await _cacheService.GetShopItemsAsync(filter.ShopId);
        
        if (items == null)
        {
            // 缓存未命中，从数据库加载
            items = await _context.ShopItems
                .Where(i => i.ShopId == filter.ShopId && i.IsEnabled)
                .OrderBy(i => i.SortOrder)
                .ToListAsync();
            
            // 将结果缓存
            _cacheService.SetShopItems(filter.ShopId, items);
        }

        // 应用过滤条件
        var filteredItems = items.AsEnumerable();

        // 按物品类别过滤
        if (!string.IsNullOrWhiteSpace(filter.ItemCategory))
        {
            filteredItems = filteredItems.Where(i => 
                i.ItemCategory != null && 
                i.ItemCategory.Equals(filter.ItemCategory, StringComparison.OrdinalIgnoreCase));
        }

        // 按稀有度过滤
        if (!string.IsNullOrWhiteSpace(filter.Rarity))
        {
            filteredItems = filteredItems.Where(i => 
                i.Rarity != null && 
                i.Rarity.Equals(filter.Rarity, StringComparison.OrdinalIgnoreCase));
        }

        // 按价格范围过滤
        if (filter.MinPrice.HasValue || filter.MaxPrice.HasValue)
        {
            filteredItems = filteredItems.Where(i =>
            {
                var price = i.GetPrice();
                var amount = price.Amount;
                if (filter.MinPrice.HasValue && amount < filter.MinPrice.Value)
                    return false;
                if (filter.MaxPrice.HasValue && amount > filter.MaxPrice.Value)
                    return false;
                return true;
            });
        }

        // 按等级要求范围过滤
        if (filter.MinLevel.HasValue)
        {
            filteredItems = filteredItems.Where(i => i.MinLevel >= filter.MinLevel.Value);
        }
        if (filter.MaxLevel.HasValue)
        {
            filteredItems = filteredItems.Where(i => i.MinLevel <= filter.MaxLevel.Value);
        }

        // 应用排序
        filteredItems = ApplySorting(filteredItems, filter.SortBy, filter.SortDirection);

        // 转换为 DTO
        var itemDtos = new List<ShopItemDto>();
        foreach (var item in filteredItems)
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
                ItemCategory = item.ItemCategory,
                Rarity = item.Rarity,
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
        _logger.LogInformation("开始处理购买请求: CharacterId={CharacterId}, ShopItemId={ShopItemId}, Quantity={Quantity}", 
            characterId, request.ShopItemId, request.Quantity);

        if (!Guid.TryParse(characterId, out var charGuid))
        {
            _logger.LogWarning("无效的角色ID格式: {CharacterId}", characterId);
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
            _logger.LogWarning("角色不存在: CharacterId={CharacterId}", charGuid);
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
            _logger.LogWarning("商品不存在: ShopItemId={ShopItemId}", request.ShopItemId);
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
            _logger.LogInformation("购买验证失败: CharacterId={CharacterId}, ShopItemId={ShopItemId}, Reason={Reason}", 
                charGuid, request.ShopItemId, errorMessage);
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
        else if (price.CurrencyType == CurrencyType.Item)
        {
            // 扣除物品货币
            var itemRemoved = await _inventoryService.RemoveItemAsync(charGuid, price.CurrencyId!, totalPrice);
            if (!itemRemoved)
            {
                return new PurchaseResponse
                {
                    Success = false,
                    Message = "扣除物品货币失败，购买已取消"
                };
            }
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

        // 发放物品到库存（在同一事务中）
        var itemAdded = await _inventoryService.AddItemAsync(charGuid, shopItem.ItemDefinitionId, request.Quantity);
        if (!itemAdded)
        {
            // 如果发放物品失败，不保存任何更改（自动回滚）
            return new PurchaseResponse
            {
                Success = false,
                Message = "发放物品到背包失败，购买已取消"
            };
        }

        // 所有操作成功，保存到数据库（原子性操作）
        await _context.SaveChangesAsync();

        _logger.LogInformation("购买成功: CharacterId={CharacterId}, ShopItemId={ShopItemId}, ItemName={ItemName}, Quantity={Quantity}, TotalPrice={TotalPrice}", 
            charGuid, request.ShopItemId, shopItem.ItemName, request.Quantity, totalPrice);

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

    public async Task<PurchaseHistoryResponse> GetPurchaseHistoryAsync(string characterId, int page = 1, int pageSize = 0)
    {
        if (!Guid.TryParse(characterId, out var charGuid))
        {
            return new PurchaseHistoryResponse();
        }

        // 使用配置的默认页面大小，如果未指定
        if (pageSize <= 0)
        {
            pageSize = _shopOptions.DefaultPageSize;
        }
        
        // 限制最大页面大小
        if (pageSize > _shopOptions.MaxPageSize)
        {
            pageSize = _shopOptions.MaxPageSize;
        }

        var skip = (page - 1) * pageSize;

        var totalCount = await _context.PurchaseRecords
            .AsNoTracking()
            .Where(r => r.CharacterId == charGuid)
            .CountAsync();

        var records = await _context.PurchaseRecords
            .AsNoTracking()
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
    /// 应用排序规则到商品集合
    /// </summary>
    private IEnumerable<ShopItem> ApplySorting(IEnumerable<ShopItem> items, string? sortBy, string? sortDirection)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return items;
        }

        var isAscending = string.IsNullOrWhiteSpace(sortDirection) || 
                         sortDirection.Equals("Asc", StringComparison.OrdinalIgnoreCase);

        return sortBy.ToLower() switch
        {
            "price" => isAscending 
                ? items.OrderBy(i => i.GetPrice().Amount) 
                : items.OrderByDescending(i => i.GetPrice().Amount),
            
            "level" => isAscending 
                ? items.OrderBy(i => i.MinLevel) 
                : items.OrderByDescending(i => i.MinLevel),
            
            "name" => isAscending 
                ? items.OrderBy(i => i.ItemName) 
                : items.OrderByDescending(i => i.ItemName),
            
            "rarity" => isAscending 
                ? items.OrderBy(i => GetRarityOrder(i.Rarity)) 
                : items.OrderByDescending(i => GetRarityOrder(i.Rarity)),
            
            _ => items
        };
    }

    /// <summary>
    /// 获取稀有度排序权重
    /// </summary>
    private int GetRarityOrder(string? rarity)
    {
        if (string.IsNullOrWhiteSpace(rarity))
        {
            return 0;
        }

        return rarity.ToLower() switch
        {
            "common" => 1,
            "uncommon" => 2,
            "rare" => 3,
            "epic" => 4,
            "legendary" => 5,
            _ => 0
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
            shouldReset = counter.ShouldReset(_shopOptions.DailyResetSeconds);
        }
        else if (limit.Type == LimitType.Weekly)
        {
            shouldReset = counter.ShouldReset(_shopOptions.WeeklyResetSeconds);
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
