using BlazorIdle.Server.Auth.Models;

namespace BlazorIdle.Server.Auth.Services;

/// <summary>
/// 用户存储接口
/// 定义用户数据访问的标准操作
/// 具体实现可以基于内存、数据库或其他持久化方案
/// </summary>
public interface IUserStore
{
    /// <summary>
    /// 根据用户ID获取用户
    /// </summary>
    /// <param name="userId">用户唯一标识</param>
    /// <returns>用户实体，如果不存在则返回null</returns>
    Task<User?> GetUserByIdAsync(string userId);
    
    /// <summary>
    /// 根据用户名获取用户
    /// </summary>
    /// <param name="username">用户名（不区分大小写）</param>
    /// <returns>用户实体，如果不存在则返回null</returns>
    Task<User?> GetUserByUsernameAsync(string username);
    
    /// <summary>
    /// 创建新用户
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="password">明文密码（将被哈希处理）</param>
    /// <returns>创建的用户实体</returns>
    /// <exception cref="InvalidOperationException">当用户名已存在时抛出</exception>
    Task<User> CreateUserAsync(string username, string password);
    
    /// <summary>
    /// 验证用户密码
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="password">明文密码</param>
    /// <returns>密码正确返回true，否则返回false</returns>
    Task<bool> ValidatePasswordAsync(string username, string password);
    
    /// <summary>
    /// 更新用户最后登录时间
    /// </summary>
    /// <param name="userId">用户唯一标识</param>
    Task UpdateLastLoginAsync(string userId);
    
    /// <summary>
    /// 保存刷新令牌
    /// 将刷新令牌及其过期时间保存到用户记录中
    /// </summary>
    /// <param name="userId">用户唯一标识</param>
    /// <param name="refreshToken">刷新令牌</param>
    /// <param name="expiresAt">令牌过期时间</param>
    Task SaveRefreshTokenAsync(string userId, string refreshToken, DateTime expiresAt);
    
    /// <summary>
    /// 验证刷新令牌
    /// 检查刷新令牌是否有效且未过期
    /// </summary>
    /// <param name="refreshToken">刷新令牌</param>
    /// <returns>令牌有效则返回对应的用户实体，否则返回null</returns>
    Task<User?> ValidateRefreshTokenAsync(string refreshToken);
    
    /// <summary>
    /// 获取所有用户（仅供测试和调试使用）
    /// 生产环境应限制此方法的访问权限
    /// </summary>
    /// <returns>所有用户的集合</returns>
    Task<IEnumerable<User>> GetAllUsersAsync();
}
