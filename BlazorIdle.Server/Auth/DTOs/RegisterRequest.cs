using System.ComponentModel.DataAnnotations;

namespace BlazorIdle.Server.Auth.DTOs;

/// <summary>
/// 注册请求
/// 用户注册新账户时提交的信息
/// </summary>
public class RegisterRequest
{
    /// <summary>
    /// 用户名
    /// </summary>
    [Required(ErrorMessage = "用户名不能为空")]
    [MinLength(3, ErrorMessage = "用户名至少3位")]
    [MaxLength(20, ErrorMessage = "用户名最多20位")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 密码（明文）
    /// 服务器端将使用BCrypt进行哈希后存储
    /// </summary>
    [Required(ErrorMessage = "密码不能为空")]
    [MinLength(6, ErrorMessage = "密码至少6位")]
    public string Password { get; set; } = string.Empty;
}
