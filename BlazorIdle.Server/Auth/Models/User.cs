namespace BlazorIdle.Server.Auth.Models;

/// <summary>
/// 用户实体模型
/// 用于存储用户的基本信息和认证相关数据
/// </summary>
public class User
{
    /// <summary>
    /// 用户唯一标识
    /// 使用GUID格式保证全局唯一性
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// 用户名（登录用）
    /// 用于用户登录和显示，应该唯一
    /// </summary>
    public string Username { get; set; } = string.Empty;
    
    /// <summary>
    /// 密码哈希（BCrypt）
    /// 使用BCrypt算法生成的密码哈希值，永远不存储明文密码
    /// BCrypt工作因子设置为12，提供良好的安全性和性能平衡
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;
    
    /// <summary>
    /// 创建时间
    /// 记录用户账户的创建时间（UTC时区）
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 最后登录时间
    /// 记录用户最近一次成功登录的时间（UTC时区）
    /// 可为null表示用户从未登录过
    /// </summary>
    public DateTime? LastLoginAt { get; set; }
    
    /// <summary>
    /// 当前刷新令牌
    /// 用于在访问令牌过期后获取新的访问令牌
    /// 每次生成新的刷新令牌时，旧的会被替换
    /// </summary>
    public string? RefreshToken { get; set; }
    
    /// <summary>
    /// 刷新令牌过期时间
    /// 刷新令牌的有效期截止时间（UTC时区）
    /// 过期后需要重新登录
    /// </summary>
    public DateTime? RefreshTokenExpiresAt { get; set; }
}
