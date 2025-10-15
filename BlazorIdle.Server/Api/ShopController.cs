using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Shared.Models.Shop;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BlazorIdle.Server.Api;

/// <summary>
/// 商店系统 API 控制器
/// </summary>
/// <remarks>
/// 提供的功能：
/// - 查询所有可用商店列表（带解锁条件）
/// - 查询指定商店的商品列表（带购买限制）
/// - 购买商品（消耗货币，发放物品）
/// - 查询购买历史记录（分页）
/// 
/// 商店特性：
/// - 支持多种货币（金币、钻石、材料等）
/// - 商品限购（每日/每周/永久限购）
/// - 商店解锁条件（等级/成就等）
/// - 购买记录追踪
/// 
/// 认证要求：
/// - 所有接口需要 JWT 认证
/// - 支持从 JWT Claims 或 Query 参数获取角色ID
/// 
/// 基础路由：/api/shop
/// </remarks>
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
    /// </summary>
    /// <param name="characterId">可选的角色ID，不提供则从JWT Claims获取</param>
    /// <returns>商店列表，包含解锁状态</returns>
    /// <response code="200">返回商店列表</response>
    /// <response code="400">角色ID无效</response>
    /// <response code="401">未登录</response>
    /// <remarks>
    /// 功能说明：
    /// - 返回游戏中所有配置的商店
    /// - 显示每个商店的解锁状态
    /// - 未解锁商店会显示解锁条件
    /// - 支持多种商店类型（常规、特殊、限时等）
    /// 
    /// 商店解锁条件示例：
    /// - 角色等级达到要求
    /// - 完成特定任务
    /// - 解锁特定成就
    /// 
    /// 请求示例：
    /// <code>
    /// GET /api/shop/list
    /// GET /api/shop/list?characterId=123e4567-e89b-12d3-a456-426614174000
    /// </code>
    /// 
    /// 响应示例：
    /// <code>
    /// {
    ///   "shops": [
    ///     {
    ///       "shopId": "general_store",
    ///       "name": "常规商店",
    ///       "description": "出售基础物品",
    ///       "isUnlocked": true,
    ///       "unlockCondition": null
    ///     },
    ///     {
    ///       "shopId": "rare_goods",
    ///       "name": "稀有商店",
    ///       "description": "出售稀有物品",
    ///       "isUnlocked": false,
    ///       "unlockCondition": "角色等级达到30级"
    ///     }
    ///   ]
    /// }
    /// </code>
    /// </remarks>
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
    /// </summary>
    /// <param name="shopId">商店ID</param>
    /// <param name="characterId">可选的角色ID，不提供则从JWT Claims获取</param>
    /// <returns>商品列表，包含价格、库存、限购信息</returns>
    /// <response code="200">返回商品列表</response>
    /// <response code="400">角色ID无效</response>
    /// <response code="401">未登录</response>
    /// <remarks>
    /// 功能说明：
    /// - 返回指定商店的所有商品
    /// - 显示商品价格和所需货币类型
    /// - 显示购买限制（每日/每周/永久限购）
    /// - 显示角色已购买次数
    /// - 显示商品是否可购买（根据限制和货币）
    /// 
    /// 限购类型：
    /// - Daily: 每日限购，每天重置
    /// - Weekly: 每周限购，每周一重置
    /// - Permanent: 永久限购，购买后不可再买
    /// - None: 无限制
    /// 
    /// 请求示例：
    /// <code>
    /// GET /api/shop/general_store/items
    /// GET /api/shop/rare_goods/items?characterId=123e4567-e89b-12d3-a456-426614174000
    /// </code>
    /// 
    /// 响应示例：
    /// <code>
    /// {
    ///   "shopId": "general_store",
    ///   "shopName": "常规商店",
    ///   "items": [
    ///     {
    ///       "itemId": "health_potion",
    ///       "name": "生命药水",
    ///       "description": "恢复100点生命值",
    ///       "price": 50,
    ///       "currencyType": "gold",
    ///       "purchaseLimit": "Daily",
    ///       "maxPurchases": 10,
    ///       "purchasedCount": 3,
    ///       "canPurchase": true
    ///     }
    ///   ]
    /// }
    /// </code>
    /// </remarks>
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
    /// </summary>
    /// <param name="request">购买请求，包含商店ID、商品ID和数量</param>
    /// <param name="characterId">可选的角色ID，不提供则从JWT Claims获取</param>
    /// <returns>购买结果，包含成功状态和获得的物品</returns>
    /// <response code="200">购买成功</response>
    /// <response code="400">购买失败（如：货币不足、超出限购、商品不存在等）</response>
    /// <response code="401">未登录</response>
    /// <remarks>
    /// 功能说明：
    /// - 验证角色拥有足够的货币
    /// - 验证商品购买限制（不超过限购次数）
    /// - 扣除货币，发放物品到角色背包
    /// - 记录购买历史
    /// - 支持一次购买多个数量（在限购范围内）
    /// 
    /// 购买失败情况：
    /// - 货币不足
    /// - 超出每日/每周/永久限购次数
    /// - 商品不存在或已下架
    /// - 商店未解锁
    /// - 背包空间不足（如有限制）
    /// 
    /// 事务处理：
    /// - 所有操作在数据库事务中完成
    /// - 失败时自动回滚，不会扣除货币
    /// 
    /// 请求示例：
    /// <code>
    /// POST /api/shop/purchase
    /// {
    ///   "shopId": "general_store",
    ///   "itemId": "health_potion",
    ///   "quantity": 5
    /// }
    /// </code>
    /// 
    /// 响应示例（成功）：
    /// <code>
    /// {
    ///   "success": true,
    ///   "message": "购买成功",
    ///   "purchasedItems": [
    ///     {
    ///       "itemId": "health_potion",
    ///       "quantity": 5
    ///     }
    ///   ],
    ///   "totalCost": 250,
    ///   "currencyType": "gold",
    ///   "remainingCurrency": 1750
    /// }
    /// </code>
    /// 
    /// 响应示例（失败）：
    /// <code>
    /// {
    ///   "success": false,
    ///   "message": "金币不足，需要250金币，当前只有100金币"
    /// }
    /// </code>
    /// </remarks>
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
    /// </summary>
    /// <param name="characterId">可选的角色ID，不提供则从JWT Claims获取</param>
    /// <param name="page">页码，从1开始，默认为1</param>
    /// <param name="pageSize">每页数量，默认20，最大100</param>
    /// <returns>购买历史记录列表，包含分页信息</returns>
    /// <response code="200">返回购买历史</response>
    /// <response code="400">角色ID无效</response>
    /// <response code="401">未登录</response>
    /// <remarks>
    /// 功能说明：
    /// - 查询角色的所有购买记录
    /// - 按时间倒序排列（最新的在前）
    /// - 支持分页查询
    /// - 显示商品信息、价格、数量、购买时间
    /// 
    /// 分页限制：
    /// - 每页最大100条记录
    /// - 超过100会自动限制为100
    /// 
    /// 请求示例：
    /// <code>
    /// GET /api/shop/history
    /// GET /api/shop/history?page=2&amp;pageSize=50
    /// GET /api/shop/history?characterId=123e4567-e89b-12d3-a456-426614174000
    /// </code>
    /// 
    /// 响应示例：
    /// <code>
    /// {
    ///   "page": 1,
    ///   "pageSize": 20,
    ///   "totalCount": 156,
    ///   "totalPages": 8,
    ///   "purchases": [
    ///     {
    ///       "purchaseId": "abc-123",
    ///       "shopId": "general_store",
    ///       "shopName": "常规商店",
    ///       "itemId": "health_potion",
    ///       "itemName": "生命药水",
    ///       "quantity": 5,
    ///       "totalCost": 250,
    ///       "currencyType": "gold",
    ///       "purchaseTime": "2025-10-15T10:30:00Z"
    ///     }
    ///   ]
    /// }
    /// </code>
    /// </remarks>
    [HttpGet("history")]
    public async Task<ActionResult<PurchaseHistoryResponse>> GetPurchaseHistory(
        [FromQuery] string? characterId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var charId = characterId ?? GetCharacterIdFromClaims();
        if (string.IsNullOrEmpty(charId))
        {
            return BadRequest(new { message = "角色 ID 不能为空" });
        }

        if (pageSize > 100)
        {
            pageSize = 100;
        }

        var response = await _shopService.GetPurchaseHistoryAsync(charId, page, pageSize);
        return Ok(response);
    }

    /// <summary>
    /// 从 JWT Claims 中获取角色 ID
    /// </summary>
    /// <returns>角色ID，如果未找到则返回null</returns>
    /// <remarks>
    /// 从JWT Token的NameIdentifier声明中提取角色ID
    /// 用作所有接口的默认角色ID来源
    /// </remarks>
    private string? GetCharacterIdFromClaims()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
