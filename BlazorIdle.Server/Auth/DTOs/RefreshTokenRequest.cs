using System.ComponentModel.DataAnnotations;

namespace BlazorIdle.Server.Auth.DTOs;

/// <summary>
/// 刷新令牌请求
/// 用于使用刷新令牌获取新的访问令牌
/// </summary>
public class RefreshTokenRequest
{
    /// <summary>
    /// 刷新令牌
    /// 之前登录时服务器返回的RefreshToken
    /// </summary>
    [Required(ErrorMessage = "刷新令牌不能为空")]
    public string RefreshToken { get; set; } = string.Empty;
}
