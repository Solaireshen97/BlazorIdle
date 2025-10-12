using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Shared.Models.Shop;

namespace BlazorIdle.Server.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ShopController : ControllerBase
{
    private readonly IShopService _shopService;
    private readonly ILogger<ShopController> _logger;

    public ShopController(IShopService shopService, ILogger<ShopController> logger)
    {
        _shopService = shopService;
        _logger = logger;
    }

    /// <summary>
    /// 获取商店列表
    /// </summary>
    [HttpGet("list")]
    public async Task<ActionResult<ListShopsResponse>> ListShops(
        [FromQuery] Guid characterId,
        [FromQuery] bool includeDisabled = false,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _shopService.ListShopsAsync(characterId, includeDisabled, ct);
            return Ok(result);
        }
        catch (NotImplementedException ex)
        {
            _logger.LogWarning("Shop list feature not yet implemented: {Message}", ex.Message);
            return StatusCode(501, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list shops for character {CharacterId}", characterId);
            return StatusCode(500, new { error = "获取商店列表失败" });
        }
    }

    /// <summary>
    /// 获取商店商品列表
    /// </summary>
    [HttpGet("{shopId}/items")]
    public async Task<ActionResult<ListShopItemsResponse>> ListShopItems(
        string shopId,
        [FromQuery] Guid characterId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _shopService.ListShopItemsAsync(
                shopId, characterId, page, pageSize, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"商店 {shopId} 不存在" });
        }
        catch (NotImplementedException ex)
        {
            _logger.LogWarning("Shop items list feature not yet implemented: {Message}", ex.Message);
            return StatusCode(501, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list items for shop {ShopId}", shopId);
            return StatusCode(500, new { error = "获取商品列表失败" });
        }
    }

    /// <summary>
    /// 购买商品
    /// </summary>
    [HttpPost("purchase")]
    public async Task<ActionResult<PurchaseResponse>> Purchase(
        [FromBody] PurchaseRequest request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _shopService.PurchaseItemAsync(request, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new PurchaseResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
        catch (NotImplementedException ex)
        {
            _logger.LogWarning("Purchase feature not yet implemented: {Message}", ex.Message);
            return StatusCode(501, new PurchaseResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to purchase item {ItemId} for character {CharacterId}",
                request.ItemId, request.CharacterId);
            return StatusCode(500, new PurchaseResponse
            {
                Success = false,
                ErrorMessage = "购买失败，请稍后重试"
            });
        }
    }

    /// <summary>
    /// 获取购买历史
    /// </summary>
    [HttpGet("purchase-history")]
    public async Task<ActionResult<PurchaseHistoryResponse>> GetPurchaseHistory(
        [FromQuery] Guid characterId,
        [FromQuery] string? shopId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _shopService.GetPurchaseHistoryAsync(
                characterId, shopId, startDate, endDate, page, pageSize, ct);
            return Ok(result);
        }
        catch (NotImplementedException ex)
        {
            _logger.LogWarning("Purchase history feature not yet implemented: {Message}", ex.Message);
            return StatusCode(501, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get purchase history for character {CharacterId}",
                characterId);
            return StatusCode(500, new { error = "获取购买历史失败" });
        }
    }
}
