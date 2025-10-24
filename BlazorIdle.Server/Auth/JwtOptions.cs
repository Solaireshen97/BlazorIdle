namespace BlazorIdle.Server.Auth;

/// <summary>
/// JWT配置选项
/// 从appsettings.json的"Jwt"节点读取配置
/// </summary>
public class JwtOptions
{
    /// <summary>
    /// 配置节点名称
    /// </summary>
    public const string SectionName = "Jwt";

    /// <summary>
    /// JWT签名密钥（至少32字符）
    /// 用于生成和验证JWT令牌的HMAC-SHA256签名
    /// 生产环境应使用环境变量或密钥管理服务
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// JWT发行者
    /// 标识令牌的发行方（通常是服务器的名称或URL）
    /// </summary>
    public string Issuer { get; set; } = "BlazorIdleServer";

    /// <summary>
    /// JWT受众
    /// 标识令牌的预期接收方（通常是客户端应用的标识）
    /// </summary>
    public string Audience { get; set; } = "BlazorIdleClient";

    /// <summary>
    /// JWT令牌过期时间（分钟）
    /// 访问令牌的有效期，过期后需要使用刷新令牌获取新令牌
    /// 默认60分钟
    /// </summary>
    public int ExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// 刷新令牌过期时间（天）
    /// 刷新令牌的有效期，用于在访问令牌过期后获取新的访问令牌
    /// 默认7天
    /// </summary>
    public int RefreshTokenExpirationDays { get; set; } = 7;

    /// <summary>
    /// 验证配置有效性
    /// 在应用启动时调用，确保配置参数符合要求
    /// </summary>
    /// <exception cref="InvalidOperationException">配置无效时抛出</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SecretKey))
            throw new InvalidOperationException("JWT SecretKey 不能为空");

        if (SecretKey.Length < 32)
            throw new InvalidOperationException($"JWT SecretKey 长度至少32字符，当前长度：{SecretKey.Length}");

        if (string.IsNullOrWhiteSpace(Issuer))
            throw new InvalidOperationException("JWT Issuer 不能为空");

        if (string.IsNullOrWhiteSpace(Audience))
            throw new InvalidOperationException("JWT Audience 不能为空");

        if (ExpirationMinutes <= 0)
            throw new InvalidOperationException($"JWT ExpirationMinutes 必须大于0，当前值：{ExpirationMinutes}");

        if (RefreshTokenExpirationDays <= 0)
            throw new InvalidOperationException($"JWT RefreshTokenExpirationDays 必须大于0，当前值：{RefreshTokenExpirationDays}");
    }
}
