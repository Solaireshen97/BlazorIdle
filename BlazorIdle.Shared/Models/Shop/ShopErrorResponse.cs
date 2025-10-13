namespace BlazorIdle.Shared.Models.Shop;

/// <summary>
/// 商店系统错误响应
/// </summary>
public class ShopErrorResponse
{
    /// <summary>
    /// 错误码
    /// </summary>
    public ShopErrorCode ErrorCode { get; set; }
    
    /// <summary>
    /// 错误消息（用户友好）
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// 详细错误信息（用于调试）
    /// </summary>
    public string? Details { get; set; }
    
    /// <summary>
    /// 错误发生的字段（用于表单验证）
    /// </summary>
    public string? Field { get; set; }
    
    /// <summary>
    /// 建议的操作（可选）
    /// </summary>
    public string? SuggestedAction { get; set; }
    
    /// <summary>
    /// 创建一个成功响应（无错误）
    /// </summary>
    public static ShopErrorResponse Success()
    {
        return new ShopErrorResponse
        {
            ErrorCode = ShopErrorCode.None,
            Message = "成功"
        };
    }
    
    /// <summary>
    /// 创建一个错误响应
    /// </summary>
    public static ShopErrorResponse Error(
        ShopErrorCode errorCode, 
        string message, 
        string? details = null,
        string? field = null,
        string? suggestedAction = null)
    {
        return new ShopErrorResponse
        {
            ErrorCode = errorCode,
            Message = message,
            Details = details,
            Field = field,
            SuggestedAction = suggestedAction
        };
    }
}
