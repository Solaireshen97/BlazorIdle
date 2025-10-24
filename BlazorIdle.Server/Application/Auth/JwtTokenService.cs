using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace BlazorIdle.Server.Application.Auth;

/// <summary>
/// JWT Token 生成和验证服务
/// 提供用户身份认证所需的Token生成和解析功能
/// </summary>
/// <remarks>
/// 该服务负责：
/// 1. 生成包含用户信息的JWT Token
/// 2. 从认证主体中提取用户标识
/// 3. 使用配置文件中的参数确保安全性和灵活性
/// </remarks>
public class JwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<JwtTokenService> _logger;

    /// <summary>
    /// 配置键名常量 - JWT密钥
    /// </summary>
    private const string ConfigKeySecretKey = "Jwt:SecretKey";

    /// <summary>
    /// 配置键名常量 - JWT签发者
    /// </summary>
    private const string ConfigKeyIssuer = "Jwt:Issuer";

    /// <summary>
    /// 配置键名常量 - JWT接收者
    /// </summary>
    private const string ConfigKeyAudience = "Jwt:Audience";

    /// <summary>
    /// 配置键名常量 - JWT过期时间（分钟）
    /// </summary>
    private const string ConfigKeyExpirationMinutes = "Jwt:ExpirationMinutes";

    /// <summary>
    /// 默认Token过期时间（分钟）- 当配置未指定时使用
    /// </summary>
    private const int DefaultExpirationMinutes = 1440; // 24小时

    /// <summary>
    /// 初始化JWT Token服务
    /// </summary>
    /// <param name="configuration">应用程序配置接口，用于读取JWT相关配置</param>
    /// <param name="logger">日志记录器，用于记录Token生成和验证相关的日志</param>
    public JwtTokenService(IConfiguration configuration, ILogger<JwtTokenService> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // 验证必要的配置项是否存在
        ValidateConfiguration();
    }

    /// <summary>
    /// 为用户生成JWT Token
    /// </summary>
    /// <param name="userId">用户的唯一标识符（GUID）</param>
    /// <param name="username">用户名，将包含在Token的声明中</param>
    /// <param name="email">用户邮箱，将包含在Token的声明中</param>
    /// <returns>生成的JWT Token字符串</returns>
    /// <exception cref="ArgumentException">当用户名或邮箱为空时抛出</exception>
    /// <exception cref="InvalidOperationException">当配置不完整或无效时抛出</exception>
    /// <remarks>
    /// Token包含以下声明（Claims）：
    /// - Sub: 用户ID
    /// - UniqueName: 用户名
    /// - Email: 电子邮箱
    /// - Jti: Token的唯一标识符
    /// 
    /// Token使用HMAC-SHA256算法签名，确保不可篡改。
    /// Token的有效期通过配置文件的Jwt:ExpirationMinutes参数控制。
    /// </remarks>
    public string GenerateToken(Guid userId, string username, string email)
    {
        // 参数验证
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("用户名不能为空", nameof(username));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("电子邮箱不能为空", nameof(email));
        }

        try
        {
            // 从配置文件读取密钥并创建签名凭证
            var secretKey = _configuration[ConfigKeySecretKey]!;
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            // 构建Token声明（Claims）
            // 包含用户的基本身份信息
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()), // 主题声明：用户ID
                new Claim(JwtRegisteredClaimNames.UniqueName, username),   // 唯一名称：用户名
                new Claim(JwtRegisteredClaimNames.Email, email),           // 电子邮件
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) // JWT ID：Token唯一标识
            };

            // 从配置读取过期时间，如果未配置则使用默认值
            var expirationMinutes = GetExpirationMinutes();

            // 创建JWT Token
            var token = new JwtSecurityToken(
                issuer: _configuration[ConfigKeyIssuer],     // Token签发者
                audience: _configuration[ConfigKeyAudience], // Token接收者
                claims: claims,                              // Token声明
                expires: DateTime.UtcNow.AddMinutes(expirationMinutes), // 过期时间
                signingCredentials: credentials              // 签名凭证
            );

            // 序列化Token为字符串
            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            // 记录Token生成日志（不记录敏感信息）
            // 注意：为避免日志伪造攻击，对用户提供的username进行消毒处理
            var sanitizedUsername = SanitizeForLog(username);
            _logger.LogInformation(
                "成功为用户 {Username}（ID: {UserId}）生成JWT Token，有效期 {ExpirationMinutes} 分钟",
                sanitizedUsername, userId, expirationMinutes);

            return tokenString;
        }
        catch (Exception ex)
        {
            // 记录Token生成失败的详细错误
            // 注意：为避免日志伪造攻击，对用户提供的username进行消毒处理
            var sanitizedUsername = SanitizeForLog(username);
            _logger.LogError(ex, 
                "为用户 {Username}（ID: {UserId}）生成JWT Token时发生错误", 
                sanitizedUsername, userId);
            throw new InvalidOperationException("生成JWT Token失败，请检查配置和参数", ex);
        }
    }

    /// <summary>
    /// 从认证主体（ClaimsPrincipal）中提取用户ID
    /// </summary>
    /// <param name="principal">HTTP请求的认证主体，包含用户的声明信息</param>
    /// <returns>
    /// 如果成功提取用户ID则返回GUID值，否则返回null
    /// </returns>
    /// <remarks>
    /// 该方法会尝试从以下声明类型中提取用户ID：
    /// 1. ClaimTypes.NameIdentifier（标准的用户标识符声明）
    /// 2. JwtRegisteredClaimNames.Sub（JWT标准的主题声明）
    /// 
    /// 此方法为静态方法，可以在任何需要获取当前用户ID的地方调用，
    /// 无需依赖JwtTokenService实例。
    /// </remarks>
    public static Guid? GetUserIdFromClaims(ClaimsPrincipal principal)
    {
        if (principal == null)
        {
            return null;
        }

        // 尝试查找用户标识符声明
        // 优先使用ClaimTypes.NameIdentifier，其次使用JWT标准的Sub声明
        var subClaim = principal.FindFirst(ClaimTypes.NameIdentifier) 
                       ?? principal.FindFirst(JwtRegisteredClaimNames.Sub);
        
        // 如果找到声明并且可以解析为有效的GUID，则返回用户ID
        if (subClaim != null && Guid.TryParse(subClaim.Value, out var userId))
        {
            return userId;
        }
        
        // 未找到有效的用户ID声明
        return null;
    }

    /// <summary>
    /// 验证JWT配置的完整性和有效性
    /// </summary>
    /// <exception cref="InvalidOperationException">当必需的配置项缺失或无效时抛出</exception>
    /// <remarks>
    /// 检查以下配置项：
    /// 1. Jwt:SecretKey - 必须存在且至少32字符
    /// 2. Jwt:Issuer - 必须存在
    /// 3. Jwt:Audience - 必须存在
    /// 4. Jwt:ExpirationMinutes - 可选，如果存在必须为有效数字
    /// </remarks>
    private void ValidateConfiguration()
    {
        // 验证密钥
        var secretKey = _configuration[ConfigKeySecretKey];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException(
                $"JWT密钥配置缺失。请在appsettings.json中配置 {ConfigKeySecretKey}");
        }

        if (secretKey.Length < 32)
        {
            _logger.LogWarning(
                "JWT密钥长度不足32字符，可能存在安全风险。当前长度：{Length}",
                secretKey.Length);
        }

        // 验证签发者
        var issuer = _configuration[ConfigKeyIssuer];
        if (string.IsNullOrWhiteSpace(issuer))
        {
            throw new InvalidOperationException(
                $"JWT签发者配置缺失。请在appsettings.json中配置 {ConfigKeyIssuer}");
        }

        // 验证接收者
        var audience = _configuration[ConfigKeyAudience];
        if (string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException(
                $"JWT接收者配置缺失。请在appsettings.json中配置 {ConfigKeyAudience}");
        }

        // 验证过期时间配置（如果存在）
        var expirationConfig = _configuration[ConfigKeyExpirationMinutes];
        if (!string.IsNullOrWhiteSpace(expirationConfig))
        {
            if (!double.TryParse(expirationConfig, out var expirationMinutes) || expirationMinutes <= 0)
            {
                throw new InvalidOperationException(
                    $"JWT过期时间配置无效。{ConfigKeyExpirationMinutes} 必须为正数");
            }
        }

        _logger.LogInformation("JWT配置验证通过");
    }

    /// <summary>
    /// 获取Token过期时间（分钟）
    /// </summary>
    /// <returns>Token的有效期（分钟数）</returns>
    /// <remarks>
    /// 如果配置文件中未指定过期时间或配置无效，将使用默认值24小时（1440分钟）
    /// </remarks>
    private int GetExpirationMinutes()
    {
        var expirationConfig = _configuration[ConfigKeyExpirationMinutes];
        
        if (string.IsNullOrWhiteSpace(expirationConfig))
        {
            _logger.LogDebug(
                "未配置JWT过期时间，使用默认值 {DefaultMinutes} 分钟",
                DefaultExpirationMinutes);
            return DefaultExpirationMinutes;
        }

        if (double.TryParse(expirationConfig, out var expirationMinutes) && expirationMinutes > 0)
        {
            return (int)expirationMinutes;
        }

        _logger.LogWarning(
            "JWT过期时间配置无效：{Config}，使用默认值 {DefaultMinutes} 分钟",
            expirationConfig, DefaultExpirationMinutes);
        
        return DefaultExpirationMinutes;
    }

    /// <summary>
    /// 对日志内容进行消毒处理，防止日志伪造攻击
    /// </summary>
    /// <param name="input">用户提供的输入字符串</param>
    /// <returns>消毒后的安全字符串</returns>
    /// <remarks>
    /// 移除可能导致日志伪造的字符：换行符、回车符等
    /// 这可以防止恶意用户通过特殊字符注入伪造的日志条目
    /// </remarks>
    private static string SanitizeForLog(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        // 移除可能导致日志伪造的控制字符
        return input
            .Replace("\r", "")
            .Replace("\n", "")
            .Replace("\t", " ");
    }
}
