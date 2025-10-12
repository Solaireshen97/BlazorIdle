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
    Task<ValidationResult> ValidatePurchaseAsync(
        Character character,
        ShopItem item,
        int quantity,
        CancellationToken ct = default);
}

/// <summary>
/// 验证结果
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Errors { get; set; } = new();

    public static ValidationResult Success() => new() { IsValid = true };
    public static ValidationResult Failure(string error) =>
        new() { IsValid = false, ErrorMessage = error, Errors = new() { error } };
}
