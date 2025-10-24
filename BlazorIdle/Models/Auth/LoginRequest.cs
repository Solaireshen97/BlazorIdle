using System.ComponentModel.DataAnnotations;

namespace BlazorIdle.Models.Auth;

/// <summary>
/// 用户登录请求模型
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// 用户名（必填）
    /// </summary>
    [Required(ErrorMessage = "用户名不能为空")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 密码（必填，至少6位）
    /// </summary>
    [Required(ErrorMessage = "密码不能为空")]
    [MinLength(6, ErrorMessage = "密码至少6位")]
    public string Password { get; set; } = string.Empty;
}
