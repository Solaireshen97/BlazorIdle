using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Shared.Models.Shop;
using Microsoft.AspNetCore.Mvc;

namespace BlazorIdle.Server.Api;

[ApiController]
[Route("api/[controller]")]
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
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _shopService.ListShopsAsync(characterId, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list shops for character {CharacterId}", characterId);
            return StatusCode(500, new { message = "获取商店列表失败" });
        }
    }

    /// <summary>
    /// 获取商店商品列表
    /// </summary>
    [HttpGet("{shopId}/items")]
    public async Task<ActionResult<ListShopItemsResponse>> ListShopItems(
        [FromRoute] string shopId,
        [FromQuery] Guid characterId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _shopService.ListShopItemsAsync(shopId, characterId, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list shop items for shop {ShopId}, character {CharacterId}", 
                shopId, characterId);
            return StatusCode(500, new { message = "获取商品列表失败" });
        }
    }

    /// <summary>
    /// 购买商品
    /// </summary>
    [HttpPost("purchase")]
    public async Task<ActionResult<PurchaseResponse>> PurchaseItem(
        [FromBody] PurchaseRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _shopService.PurchaseItemAsync(request, cancellationToken);
            
            if (!response.Success)
            {
                return BadRequest(response);
            }
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to purchase item {ItemId} for character {CharacterId}", 
                request.ShopItemId, request.CharacterId);
            return StatusCode(500, new PurchaseResponse(
                false, 
                "购买失败，请稍后重试", 
                null, null, null, null));
        }
    }

    /// <summary>
    /// 获取购买历史
    /// </summary>
    [HttpGet("purchase-history")]
    public async Task<ActionResult<PurchaseHistoryResponse>> GetPurchaseHistory(
        [FromQuery] Guid characterId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _shopService.GetPurchaseHistoryAsync(
                characterId, 
                pageNumber, 
                pageSize, 
                cancellationToken);
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get purchase history for character {CharacterId}", characterId);
            return StatusCode(500, new { message = "获取购买历史失败" });
        }
    }
}
