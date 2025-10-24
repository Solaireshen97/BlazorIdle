namespace BlazorIdle.Models.Auth;

/// <summary>
/// 用户信息模型（返回给客户端）
/// 不包含敏感信息（如密码哈希）
/// </summary>
public class UserInfo
{
    /// <summary>
    /// 用户唯一标识
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 用户名
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 账户创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 最后登录时间
    /// </summary>
    public DateTime? LastLoginAt { get; set; }
}
