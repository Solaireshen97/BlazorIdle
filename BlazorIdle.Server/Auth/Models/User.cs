namespace BlazorIdle.Server.Auth.Models;

/// <summary>
/// 用户实体模型
/// 存储用户的基本信息和认证相关数据
/// </summary>
public class User
{
    /// <summary>
    /// 用户唯一标识
    /// 使用GUID确保唯一性
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// 用户名（登录用）
    /// 用于用户登录和识别，不区分大小写
    /// </summary>
    public string Username { get; set; } = string.Empty;
    
    /// <summary>
    /// 密码哈希（BCrypt）
    /// 使用BCrypt算法加密存储，工作因子为12
    /// 永远不存储明文密码
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;
    
    /// <summary>
    /// 创建时间（UTC）
    /// 用户账户创建的时间戳
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 最后登录时间（UTC）
    /// 记录用户最近一次成功登录的时间
    /// </summary>
    public DateTime? LastLoginAt { get; set; }
    
    /// <summary>
    /// 当前刷新令牌
    /// 用于刷新过期的访问令牌
    /// </summary>
    public string? RefreshToken { get; set; }
    
    /// <summary>
    /// 刷新令牌过期时间（UTC）
    /// 刷新令牌的有效期截止时间
    /// </summary>
    public DateTime? RefreshTokenExpiresAt { get; set; }
}
