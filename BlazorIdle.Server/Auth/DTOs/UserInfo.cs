namespace BlazorIdle.Server.Auth.DTOs;

/// <summary>
/// 用户信息DTO（返回给客户端）
/// 不包含敏感信息如密码哈希和刷新令牌
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
    /// 账户创建时间（UTC）
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// 最后登录时间（UTC）
    /// </summary>
    public DateTime? LastLoginAt { get; set; }
}
