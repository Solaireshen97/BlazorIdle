using BlazorIdle.Shared.Models.Shop;

namespace BlazorIdle.Server.Application.Shop;

/// <summary>
/// 商店错误辅助类 - 提供统一的错误响应创建
/// </summary>
public static class ShopErrorHelper
{
    /// <summary>
    /// 创建购买失败响应
    /// </summary>
    public static PurchaseResponse CreatePurchaseError(
        ShopErrorCode errorCode, 
        string message, 
        string? details = null,
        string? suggestedAction = null)
    {
        return new PurchaseResponse
        {
            Success = false,
            Message = message,
            ErrorCode = errorCode,
            Error = ShopErrorResponse.Error(errorCode, message, details, suggestedAction: suggestedAction)
        };
    }
    
    /// <summary>
    /// 角色ID格式错误
    /// </summary>
    public static PurchaseResponse InvalidCharacterId()
    {
        return CreatePurchaseError(
            ShopErrorCode.InvalidCharacterId,
            "角色 ID 格式错误",
            "提供的角色 ID 不是有效的 GUID 格式",
            "请确认角色 ID 格式正确"
        );
    }
    
    /// <summary>
    /// 角色不存在
    /// </summary>
    public static PurchaseResponse CharacterNotFound()
    {
        return CreatePurchaseError(
            ShopErrorCode.CharacterNotFound,
            "角色不存在",
            "在数据库中找不到对应的角色",
            "请检查角色 ID 是否正确或角色是否已被删除"
        );
    }
    
    /// <summary>
    /// 商品不存在
    /// </summary>
    public static PurchaseResponse ItemNotFound(string itemId)
    {
        return CreatePurchaseError(
            ShopErrorCode.ItemNotFound,
            "商品不存在",
            $"找不到商品: {itemId}",
            "请刷新商店列表或选择其他商品"
        );
    }
    
    /// <summary>
    /// 购买数量无效
    /// </summary>
    public static PurchaseResponse InvalidQuantity(int min, int max)
    {
        return CreatePurchaseError(
            ShopErrorCode.InvalidQuantity,
            $"购买数量必须在 {min} 到 {max} 之间",
            null,
            $"请输入 {min} 到 {max} 之间的数量"
        );
    }
    
    /// <summary>
    /// 等级不足
    /// </summary>
    public static PurchaseResponse InsufficientLevel(int required, int current)
    {
        return CreatePurchaseError(
            ShopErrorCode.InsufficientLevel,
            $"等级不足，需要等级 {required}（当前等级: {current}）",
            null,
            $"升到 {required} 级后再购买"
        );
    }
    
    /// <summary>
    /// 金币不足
    /// </summary>
    public static PurchaseResponse InsufficientGold(int required, int current)
    {
        return CreatePurchaseError(
            ShopErrorCode.InsufficientGold,
            $"金币不足，需要 {required} 金币（当前: {current}）",
            null,
            $"还需要 {required - current} 金币"
        );
    }
    
    /// <summary>
    /// 货币物品不足
    /// </summary>
    public static PurchaseResponse InsufficientCurrency(string currencyName, int required, int current)
    {
        return CreatePurchaseError(
            ShopErrorCode.InsufficientCurrency,
            $"{currencyName}不足，需要 {required} 个（当前: {current}）",
            null,
            $"还需要 {required - current} 个{currencyName}"
        );
    }
    
    /// <summary>
    /// 已达购买限制
    /// </summary>
    public static PurchaseResponse PurchaseLimitReached(string limitType, int maxPurchases)
    {
        var periodText = limitType switch
        {
            "Daily" => "每日",
            "Weekly" => "每周",
            "PerCharacter" => "终身",
            _ => ""
        };
        
        return CreatePurchaseError(
            ShopErrorCode.PurchaseLimitReached,
            $"已达{periodText}购买限制（最多 {maxPurchases} 次）",
            null,
            limitType switch
            {
                "Daily" => "明天可以继续购买",
                "Weekly" => "下周可以继续购买",
                "PerCharacter" => "该商品每个角色只能购买一次",
                _ => null
            }
        );
    }
    
    /// <summary>
    /// 价格配置错误
    /// </summary>
    public static PurchaseResponse InvalidPrice()
    {
        return CreatePurchaseError(
            ShopErrorCode.InvalidPrice,
            "商品价格配置错误",
            "商品的价格数据无效或不完整",
            "请联系管理员检查商品配置"
        );
    }
    
    /// <summary>
    /// 库存不足
    /// </summary>
    public static PurchaseResponse InsufficientStock(int available)
    {
        return CreatePurchaseError(
            ShopErrorCode.InsufficientStock,
            available > 0 ? $"库存不足，仅剩 {available} 件" : "该商品已售罄",
            null,
            available > 0 ? $"最多可购买 {available} 件" : "请等待补货或选择其他商品"
        );
    }
    
    /// <summary>
    /// 物品添加失败
    /// </summary>
    public static PurchaseResponse ItemAddFailed(string reason)
    {
        return CreatePurchaseError(
            ShopErrorCode.ItemAddFailed,
            "发放物品到背包失败，购买已取消",
            reason,
            "请确保背包有足够空间或联系客服"
        );
    }
    
    /// <summary>
    /// 物品扣除失败
    /// </summary>
    public static PurchaseResponse ItemDeductFailed(string reason)
    {
        return CreatePurchaseError(
            ShopErrorCode.ItemDeductFailed,
            "扣除物品货币失败，购买已取消",
            reason,
            "请确保拥有足够的货币物品"
        );
    }
    
    /// <summary>
    /// 创建成功响应
    /// </summary>
    public static PurchaseResponse Success(string itemName, int quantity, PurchaseRecordDto record)
    {
        return new PurchaseResponse
        {
            Success = true,
            Message = $"购买成功！获得 {itemName} x{quantity}",
            ErrorCode = ShopErrorCode.None,
            Error = null,
            Record = record
        };
    }
}
