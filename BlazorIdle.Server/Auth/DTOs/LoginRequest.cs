using System.ComponentModel.DataAnnotations;

namespace BlazorIdle.Server.Auth.DTOs;

/// <summary>
/// 登录请求
/// 用户登录时提交的凭据
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// 用户名
    /// </summary>
    [Required(ErrorMessage = "用户名不能为空")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 密码（明文）
    /// 服务器端将验证此密码与存储的哈希值
    /// </summary>
    [Required(ErrorMessage = "密码不能为空")]
    [MinLength(6, ErrorMessage = "密码至少6位")]
    public string Password { get; set; } = string.Empty;
}
