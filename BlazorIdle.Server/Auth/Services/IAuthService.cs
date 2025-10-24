using System.Security.Claims;
using BlazorIdle.Server.Auth.DTOs;
using BlazorIdle.Server.Auth.Models;

namespace BlazorIdle.Server.Auth.Services;

/// <summary>
/// 认证服务接口
/// 提供用户认证、令牌生成和验证等核心功能
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// 用户登录
    /// 验证用户凭据并生成JWT令牌
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    /// <returns>认证结果，包含令牌和用户信息</returns>
    Task<AuthResult> LoginAsync(string username, string password);

    /// <summary>
    /// 用户注册
    /// 创建新用户账户并自动登录
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    /// <returns>认证结果，包含令牌和用户信息</returns>
    Task<AuthResult> RegisterAsync(string username, string password);

    /// <summary>
    /// 刷新令牌
    /// 使用刷新令牌获取新的访问令牌
    /// </summary>
    /// <param name="refreshToken">刷新令牌</param>
    /// <returns>认证结果，包含新的令牌</returns>
    Task<AuthResult> RefreshTokenAsync(string refreshToken);

    /// <summary>
    /// 生成JWT令牌
    /// 根据用户信息创建包含Claims的JWT令牌
    /// </summary>
    /// <param name="user">用户对象</param>
    /// <returns>JWT令牌字符串</returns>
    string GenerateJwtToken(User user);

    /// <summary>
    /// 生成刷新令牌
    /// 使用加密安全的随机数生成器创建刷新令牌
    /// </summary>
    /// <returns>刷新令牌字符串</returns>
    string GenerateRefreshToken();

    /// <summary>
    /// 验证JWT令牌
    /// 解析并验证JWT令牌的有效性
    /// </summary>
    /// <param name="token">JWT令牌字符串</param>
    /// <returns>如果令牌有效则返回ClaimsPrincipal，否则返回null</returns>
    ClaimsPrincipal? ValidateToken(string token);
}
