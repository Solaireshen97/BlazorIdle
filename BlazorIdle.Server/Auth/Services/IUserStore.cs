using BlazorIdle.Server.Auth.Models;

namespace BlazorIdle.Server.Auth.Services;

/// <summary>
/// 用户存储接口
/// 定义用户数据的CRUD操作和认证相关的方法
/// </summary>
public interface IUserStore
{
    /// <summary>
    /// 根据用户ID获取用户
    /// </summary>
    /// <param name="userId">用户唯一标识</param>
    /// <returns>用户对象，如果不存在则返回null</returns>
    Task<User?> GetUserByIdAsync(string userId);
    
    /// <summary>
    /// 根据用户名获取用户
    /// </summary>
    /// <param name="username">用户名（不区分大小写）</param>
    /// <returns>用户对象，如果不存在则返回null</returns>
    Task<User?> GetUserByUsernameAsync(string username);
    
    /// <summary>
    /// 创建新用户
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="password">明文密码（将被自动哈希）</param>
    /// <returns>创建的用户对象</returns>
    /// <exception cref="InvalidOperationException">用户名已存在时抛出</exception>
    Task<User> CreateUserAsync(string username, string password);
    
    /// <summary>
    /// 验证用户密码
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="password">待验证的明文密码</param>
    /// <returns>密码是否正确</returns>
    Task<bool> ValidatePasswordAsync(string username, string password);
    
    /// <summary>
    /// 更新用户最后登录时间
    /// </summary>
    /// <param name="userId">用户ID</param>
    Task UpdateLastLoginAsync(string userId);
    
    /// <summary>
    /// 保存刷新令牌
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="refreshToken">刷新令牌</param>
    /// <param name="expiresAt">过期时间（UTC）</param>
    Task SaveRefreshTokenAsync(string userId, string refreshToken, DateTime expiresAt);
    
    /// <summary>
    /// 验证刷新令牌
    /// </summary>
    /// <param name="refreshToken">刷新令牌</param>
    /// <returns>令牌有效则返回对应用户，否则返回null</returns>
    Task<User?> ValidateRefreshTokenAsync(string refreshToken);
    
    /// <summary>
    /// 获取所有用户（仅供测试）
    /// </summary>
    /// <returns>所有用户列表</returns>
    Task<IEnumerable<User>> GetAllUsersAsync();
}
