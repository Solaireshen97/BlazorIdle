using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Shop;

namespace BlazorIdle.Server.Application.Abstractions;

/// <summary>
/// 购买验证器接口
/// </summary>
public interface IPurchaseValidator
{
    /// <summary>
    /// 验证购买请求
    /// </summary>
    /// <returns>验证结果 (成功=true, 失败=false)，以及错误消息</returns>
    Task<(bool isValid, string? errorMessage)> ValidatePurchaseAsync(
        Character character,
        ShopItem shopItem,
        int quantity);
}
