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
    Task<(bool IsValid, string? ErrorMessage)> ValidatePurchaseAsync(
        Character character,
        ShopItem shopItem,
        int quantity,
        CancellationToken cancellationToken = default);
}
