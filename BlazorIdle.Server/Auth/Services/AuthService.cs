using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using BlazorIdle.Server.Auth.DTOs;
using BlazorIdle.Server.Auth.Models;

namespace BlazorIdle.Server.Auth.Services;

/// <summary>
/// 认证服务实现
/// 实现JWT令牌生成、验证和用户认证逻辑
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserStore _userStore;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<AuthService> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="userStore">用户存储服务</param>
    /// <param name="jwtOptions">JWT配置选项</param>
    /// <param name="logger">日志记录器</param>
    public AuthService(
        IUserStore userStore,
        JwtOptions jwtOptions,
        ILogger<AuthService> logger)
    {
        _userStore = userStore;
        _jwtOptions = jwtOptions;
        _logger = logger;
    }

    /// <summary>
    /// 用户登录
    /// 验证用户名和密码，成功后生成JWT令牌和刷新令牌
    /// </summary>
    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        try
        {
            // 验证用户名和密码
            var isValid = await _userStore.ValidatePasswordAsync(username, password);
            if (!isValid)
            {
                _logger.LogWarning("登录失败：用户名或密码错误 - {Username}", username);
                return new AuthResult
                {
                    Success = false,
                    Message = "用户名或密码错误"
                };
            }

            // 获取用户信息
            var user = await _userStore.GetUserByUsernameAsync(username);
            if (user == null)
            {
                _logger.LogError("用户验证成功但无法获取用户信息：{Username}", username);
                return new AuthResult
                {
                    Success = false,
                    Message = "登录失败"
                };
            }

            // 生成JWT令牌和刷新令牌
            var token = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();
            var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes);
            var refreshTokenExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays);

            // 保存刷新令牌到用户存储
            await _userStore.SaveRefreshTokenAsync(user.Id, refreshToken, refreshTokenExpiresAt);

            // 更新最后登录时间
            await _userStore.UpdateLastLoginAsync(user.Id);

            _logger.LogInformation("用户登录成功：{Username} (ID: {UserId})", username, user.Id);

            return new AuthResult
            {
                Success = true,
                Token = token,
                RefreshToken = refreshToken,
                ExpiresAt = expiresAt,
                User = new UserInfo
                {
                    Id = user.Id,
                    Username = user.Username,
                    CreatedAt = user.CreatedAt,
                    LastLoginAt = DateTime.UtcNow
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "登录过程中发生错误：{Username}", username);
            return new AuthResult
            {
                Success = false,
                Message = "登录失败，请稍后重试"
            };
        }
    }

    /// <summary>
    /// 用户注册
    /// 创建新用户并自动登录，返回JWT令牌
    /// </summary>
    public async Task<AuthResult> RegisterAsync(string username, string password)
    {
        try
        {
            // 检查用户名是否已存在
            var existingUser = await _userStore.GetUserByUsernameAsync(username);
            if (existingUser != null)
            {
                _logger.LogWarning("注册失败：用户名已存在 - {Username}", username);
                return new AuthResult
                {
                    Success = false,
                    Message = "用户名已存在"
                };
            }

            // 创建新用户
            var user = await _userStore.CreateUserAsync(username, password);

            // 生成JWT令牌和刷新令牌
            var token = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();
            var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes);
            var refreshTokenExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays);

            // 保存刷新令牌
            await _userStore.SaveRefreshTokenAsync(user.Id, refreshToken, refreshTokenExpiresAt);

            // 更新最后登录时间
            await _userStore.UpdateLastLoginAsync(user.Id);

            _logger.LogInformation("用户注册成功：{Username} (ID: {UserId})", username, user.Id);

            return new AuthResult
            {
                Success = true,
                Token = token,
                RefreshToken = refreshToken,
                ExpiresAt = expiresAt,
                User = new UserInfo
                {
                    Id = user.Id,
                    Username = user.Username,
                    CreatedAt = user.CreatedAt,
                    LastLoginAt = DateTime.UtcNow
                }
            };
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "注册失败：{Username}", username);
            return new AuthResult
            {
                Success = false,
                Message = ex.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "注册过程中发生错误：{Username}", username);
            return new AuthResult
            {
                Success = false,
                Message = "注册失败，请稍后重试"
            };
        }
    }

    /// <summary>
    /// 刷新令牌
    /// 使用有效的刷新令牌获取新的JWT令牌
    /// </summary>
    public async Task<AuthResult> RefreshTokenAsync(string refreshToken)
    {
        try
        {
            // 验证刷新令牌
            var user = await _userStore.ValidateRefreshTokenAsync(refreshToken);
            if (user == null)
            {
                _logger.LogWarning("刷新令牌无效或已过期");
                return new AuthResult
                {
                    Success = false,
                    Message = "刷新令牌无效或已过期"
                };
            }

            // 生成新的JWT令牌和刷新令牌
            var newToken = GenerateJwtToken(user);
            var newRefreshToken = GenerateRefreshToken();
            var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes);
            var refreshTokenExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays);

            // 保存新的刷新令牌（替换旧的）
            await _userStore.SaveRefreshTokenAsync(user.Id, newRefreshToken, refreshTokenExpiresAt);

            _logger.LogInformation("令牌刷新成功：用户 {UserId}", user.Id);

            return new AuthResult
            {
                Success = true,
                Token = newToken,
                RefreshToken = newRefreshToken,
                ExpiresAt = expiresAt,
                User = new UserInfo
                {
                    Id = user.Id,
                    Username = user.Username,
                    CreatedAt = user.CreatedAt,
                    LastLoginAt = user.LastLoginAt
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刷新令牌过程中发生错误");
            return new AuthResult
            {
                Success = false,
                Message = "刷新令牌失败，请重新登录"
            };
        }
    }

    /// <summary>
    /// 生成JWT令牌
    /// 创建包含用户信息的JWT令牌，使用HMAC-SHA256签名
    /// </summary>
    public string GenerateJwtToken(User user)
    {
        // 创建Claims（声明）
        // Claims包含用户的标识信息，将被编码到JWT中
        var claims = new[]
        {
            // 用户ID - 用于识别用户身份
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            // 用户名 - 用于显示
            new Claim(ClaimTypes.Name, user.Username),
            // JWT ID - 令牌的唯一标识
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            // 发行时间 - Unix时间戳
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
        };

        // 创建签名密钥
        // 使用配置文件中的SecretKey生成HMAC-SHA256签名密钥
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // 创建JWT令牌
        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes),
            signingCredentials: credentials
        );

        // 将令牌对象序列化为字符串
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// 生成刷新令牌
    /// 使用加密安全的随机数生成器创建64字节的随机令牌
    /// </summary>
    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    /// <summary>
    /// 验证JWT令牌
    /// 解析并验证令牌的签名、有效期等
    /// </summary>
    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtOptions.SecretKey);

            // 配置验证参数
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,              // 验证发行者
                ValidateAudience = true,            // 验证受众
                ValidateLifetime = true,            // 验证有效期
                ValidateIssuerSigningKey = true,    // 验证签名密钥
                ValidIssuer = _jwtOptions.Issuer,
                ValidAudience = _jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ClockSkew = TimeSpan.Zero           // 移除默认的5分钟时钟偏移
            };

            // 验证令牌并返回ClaimsPrincipal
            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JWT令牌验证失败");
            return null;
        }
    }
}
