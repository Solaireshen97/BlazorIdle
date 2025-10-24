using System.ComponentModel.DataAnnotations;

namespace BlazorIdle.Models.Auth;

/// <summary>
/// 用户注册请求模型
/// </summary>
public class RegisterRequest
{
    /// <summary>
    /// 用户名（必填，3-20字符）
    /// </summary>
    [Required(ErrorMessage = "用户名不能为空")]
    [MinLength(3, ErrorMessage = "用户名至少3位")]
    [MaxLength(20, ErrorMessage = "用户名最多20位")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 密码（必填，至少6位）
    /// </summary>
    [Required(ErrorMessage = "密码不能为空")]
    [MinLength(6, ErrorMessage = "密码至少6位")]
    public string Password { get; set; } = string.Empty;
}
