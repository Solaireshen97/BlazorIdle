namespace BlazorIdle.Server.Auth.DTOs;

/// <summary>
/// 认证结果
/// 包含认证操作（登录/注册/刷新令牌）的结果信息
/// </summary>
public class AuthResult
{
    /// <summary>
    /// 操作是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 消息（成功或失败的详细说明）
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// JWT访问令牌
    /// 用于API请求认证的Bearer Token
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// 刷新令牌
    /// 用于在访问令牌过期后获取新的访问令牌
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// 访问令牌过期时间（UTC）
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// 用户信息
    /// 认证成功时返回的用户基本信息
    /// </summary>
    public UserInfo? User { get; set; }
}
