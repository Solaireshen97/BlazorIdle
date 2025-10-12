using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Shop.Configuration;
using BlazorIdle.Shared.Models.Shop;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BlazorIdle.Server.Api;

/// <summary>
/// 商店系统 API 控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ShopController : ControllerBase
{
    private readonly IShopService _shopService;

    public ShopController(IShopService shopService)
    {
        _shopService = shopService;
    }

    /// <summary>
    /// 获取所有可用商店列表
    /// GET /api/shop/list
    /// </summary>
    [HttpGet("list")]
    public async Task<ActionResult<ListShopsResponse>> ListShops([FromQuery] string? characterId = null)
    {
        var charId = characterId ?? GetCharacterIdFromClaims();
        if (string.IsNullOrEmpty(charId))
        {
            return BadRequest(new { message = "角色 ID 不能为空" });
        }

        var response = await _shopService.ListShopsAsync(charId);
        return Ok(response);
    }

    /// <summary>
    /// 获取指定商店的商品列表
    /// GET /api/shop/{shopId}/items
    /// </summary>
    [HttpGet("{shopId}/items")]
    public async Task<ActionResult<ListShopItemsResponse>> GetShopItems(
        string shopId,
        [FromQuery] string? characterId = null)
    {
        var charId = characterId ?? GetCharacterIdFromClaims();
        if (string.IsNullOrEmpty(charId))
        {
            return BadRequest(new { message = "角色 ID 不能为空" });
        }

        var response = await _shopService.GetShopItemsAsync(shopId, charId);
        return Ok(response);
    }

    /// <summary>
    /// 购买商品
    /// POST /api/shop/purchase
    /// </summary>
    [HttpPost("purchase")]
    public async Task<ActionResult<PurchaseResponse>> PurchaseItem(
        [FromBody] PurchaseRequest request,
        [FromQuery] string? characterId = null)
    {
        var charId = characterId ?? GetCharacterIdFromClaims();
        if (string.IsNullOrEmpty(charId))
        {
            return BadRequest(new PurchaseResponse
            {
                Success = false,
                Message = "角色 ID 不能为空"
            });
        }

        var response = await _shopService.PurchaseItemAsync(charId, request);
        
        if (!response.Success)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// 获取购买历史
    /// GET /api/shop/history
    /// </summary>
    [HttpGet("history")]
    public async Task<ActionResult<PurchaseHistoryResponse>> GetPurchaseHistory(
        [FromQuery] string? characterId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = ShopSystemConfig.QueryConfig.DefaultPageSize)
    {
        var charId = characterId ?? GetCharacterIdFromClaims();
        if (string.IsNullOrEmpty(charId))
        {
            return BadRequest(new { message = "角色 ID 不能为空" });
        }

        if (pageSize > ShopSystemConfig.QueryConfig.MaxPageSize)
        {
            pageSize = ShopSystemConfig.QueryConfig.MaxPageSize;
        }

        var response = await _shopService.GetPurchaseHistoryAsync(charId, page, pageSize);
        return Ok(response);
    }

    /// <summary>
    /// 从 JWT Claims 中获取角色 ID
    /// </summary>
    private string? GetCharacterIdFromClaims()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
