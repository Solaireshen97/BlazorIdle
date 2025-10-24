using BlazorIdle.Models.Auth;

namespace BlazorIdle.Services.Auth;

/// <summary>
/// 客户端认证服务接口
/// 提供用户登录、注册、登出等功能
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// 用户登录
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    /// <returns>认证结果（包含Token和用户信息）</returns>
    Task<AuthResult> LoginAsync(string username, string password);

    /// <summary>
    /// 用户注册
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    /// <returns>认证结果（包含Token和用户信息）</returns>
    Task<AuthResult> RegisterAsync(string username, string password);

    /// <summary>
    /// 用户登出
    /// 清除本地存储的Token和用户信息
    /// </summary>
    Task LogoutAsync();

    /// <summary>
    /// 检查是否已登录
    /// </summary>
    /// <returns>true表示已登录，false表示未登录</returns>
    Task<bool> IsAuthenticatedAsync();

    /// <summary>
    /// 获取当前JWT访问令牌
    /// </summary>
    /// <returns>JWT访问令牌，未登录时返回null</returns>
    Task<string?> GetTokenAsync();

    /// <summary>
    /// 获取当前登录用户信息
    /// </summary>
    /// <returns>用户信息，未登录时返回null</returns>
    Task<UserInfo?> GetCurrentUserAsync();

    /// <summary>
    /// 刷新JWT访问令牌
    /// 使用刷新令牌获取新的访问令牌
    /// </summary>
    /// <returns>认证结果（包含新的Token）</returns>
    Task<AuthResult> RefreshTokenAsync();
}
