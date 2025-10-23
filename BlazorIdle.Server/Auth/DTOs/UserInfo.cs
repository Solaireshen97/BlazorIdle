namespace BlazorIdle.Server.Auth.DTOs;

/// <summary>
/// 用户信息DTO（返回给客户端）
/// 包含可以安全返回给客户端的用户信息
/// 不包含敏感信息如密码哈希、刷新令牌等
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
    /// 可为null表示用户从未登录过
    /// </summary>
    public DateTime? LastLoginAt { get; set; }
}
