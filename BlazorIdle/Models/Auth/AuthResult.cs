namespace BlazorIdle.Models.Auth;

/// <summary>
/// 认证结果模型
/// 包含认证成功或失败的信息以及相关的Token和用户信息
/// </summary>
public class AuthResult
{
    /// <summary>
    /// 认证是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 认证消息（成功或错误描述）
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// JWT访问令牌（认证成功时返回）
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// 刷新令牌（用于获取新的访问令牌）
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Token过期时间
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// 用户信息（认证成功时返回）
    /// </summary>
    public UserInfo? User { get; set; }
}
