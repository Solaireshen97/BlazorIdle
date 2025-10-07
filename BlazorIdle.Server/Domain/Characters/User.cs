using System;
using System.Collections.Generic;

namespace BlazorIdle.Server.Domain.Characters;

/// <summary>
/// 用户账号实体，用于管理用户登录信息和角色关联。
/// 一个用户可以拥有多个角色（支持未来的 Roster 系统）。
/// </summary>
public class User
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// 用户名（唯一）
    /// </summary>
    public string Username { get; set; } = "";
    
    /// <summary>
    /// 电子邮箱（唯一，可用于登录和通知）
    /// </summary>
    public string Email { get; set; } = "";
    
    /// <summary>
    /// 密码哈希（使用 BCrypt 等安全哈希算法存储）
    /// </summary>
    public string PasswordHash { get; set; } = "";
    
    /// <summary>
    /// 账号创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 最后登录时间（可选）
    /// </summary>
    public DateTime? LastLoginAt { get; set; }
    
    // Navigation property - 一个用户可以拥有多个角色
    public ICollection<Character> Characters { get; set; } = new List<Character>();
}
