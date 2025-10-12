using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Infrastructure.Persistence;
using BlazorIdle.Shared.Models.Shop;

namespace BlazorIdle.Server.Application.Shop;

/// <summary>
/// 商店服务实现
/// </summary>
public class ShopService : IShopService
{
    private readonly GameDbContext _context;
    private readonly IPurchaseValidator _validator;
    private readonly ILogger<ShopService> _logger;

    public ShopService(
        GameDbContext context,
        IPurchaseValidator validator,
        ILogger<ShopService> logger)
    {
        _context = context;
        _validator = validator;
        _logger = logger;
    }

    public async Task<ListShopsResponse> ListShopsAsync(
        Guid characterId,
        bool includeDisabled,
        CancellationToken ct = default)
    {
        // TODO: Phase 2 implementation
        throw new NotImplementedException("商店列表查询功能将在 Phase 2 实现");
    }

    public async Task<ListShopItemsResponse> ListShopItemsAsync(
        string shopId,
        Guid characterId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        // TODO: Phase 2 implementation
        throw new NotImplementedException("商品列表查询功能将在 Phase 2 实现");
    }

    public async Task<PurchaseResponse> PurchaseItemAsync(
        PurchaseRequest request,
        CancellationToken ct = default)
    {
        // TODO: Phase 2 implementation
        throw new NotImplementedException("购买功能将在 Phase 2 实现");
    }

    public async Task<PurchaseHistoryResponse> GetPurchaseHistoryAsync(
        Guid characterId,
        string? shopId,
        DateTime? startDate,
        DateTime? endDate,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        // TODO: Phase 3 implementation
        throw new NotImplementedException("购买历史查询功能将在 Phase 3 实现");
    }
}
