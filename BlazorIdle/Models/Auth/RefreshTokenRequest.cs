using System.ComponentModel.DataAnnotations;

namespace BlazorIdle.Models.Auth;

/// <summary>
/// 刷新令牌请求模型
/// </summary>
public class RefreshTokenRequest
{
    /// <summary>
    /// 刷新令牌（必填）
    /// </summary>
    [Required(ErrorMessage = "刷新令牌不能为空")]
    public string RefreshToken { get; set; } = string.Empty;
}
