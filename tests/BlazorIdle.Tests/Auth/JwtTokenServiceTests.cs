using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BlazorIdle.Server.Application.Auth;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BlazorIdle.Tests.Auth;

/// <summary>
/// JWT Token服务单元测试
/// 测试Token生成、用户ID提取和配置验证等核心功能
/// </summary>
public class JwtTokenServiceTests
{
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<JwtTokenService>> _mockLogger;

    /// <summary>
    /// 测试用的JWT配置
    /// </summary>
    private const string TestSecretKey = "ThisIsATestSecretKeyWithAtLeast32Characters!";
    private const string TestIssuer = "TestIssuer";
    private const string TestAudience = "TestAudience";
    private const string TestExpirationMinutes = "60";

    public JwtTokenServiceTests()
    {
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<JwtTokenService>>();

        // 设置默认配置
        SetupConfiguration(TestSecretKey, TestIssuer, TestAudience, TestExpirationMinutes);
    }

    #region 辅助方法

    /// <summary>
    /// 设置模拟配置
    /// 注意：传入null的参数将被设置为null，而不传入的参数将使用之前的设置
    /// 如果需要完全重置配置，请显式传入所有参数
    /// </summary>
    private void SetupConfiguration(
        string? secretKey = TestSecretKey,
        string? issuer = TestIssuer,
        string? audience = TestAudience,
        string? expirationMinutes = TestExpirationMinutes)
    {
        _mockConfiguration.Setup(c => c["Jwt:SecretKey"]).Returns(secretKey);
        _mockConfiguration.Setup(c => c["Jwt:Issuer"]).Returns(issuer);
        _mockConfiguration.Setup(c => c["Jwt:Audience"]).Returns(audience);
        _mockConfiguration.Setup(c => c["Jwt:ExpirationMinutes"]).Returns(expirationMinutes);
    }

    /// <summary>
    /// 重置配置为全部null
    /// </summary>
    private void ResetConfiguration()
    {
        _mockConfiguration.Setup(c => c["Jwt:SecretKey"]).Returns((string?)null);
        _mockConfiguration.Setup(c => c["Jwt:Issuer"]).Returns((string?)null);
        _mockConfiguration.Setup(c => c["Jwt:Audience"]).Returns((string?)null);
        _mockConfiguration.Setup(c => c["Jwt:ExpirationMinutes"]).Returns((string?)null);
    }

    /// <summary>
    /// 创建JwtTokenService实例
    /// </summary>
    private JwtTokenService CreateService()
    {
        return new JwtTokenService(_mockConfiguration.Object, _mockLogger.Object);
    }

    #endregion

    #region 构造函数和配置验证测试

    [Fact(DisplayName = "构造函数_配置完整_应成功创建服务")]
    public void Constructor_WithValidConfiguration_ShouldCreateService()
    {
        // Arrange & Act
        var service = CreateService();

        // Assert
        service.Should().NotBeNull();
    }

    [Fact(DisplayName = "构造函数_缺少SecretKey配置_应抛出异常")]
    public void Constructor_MissingSecretKey_ShouldThrowException()
    {
        // Arrange
        ResetConfiguration();
        SetupConfiguration(secretKey: null, issuer: TestIssuer, audience: TestAudience);

        // Act
        Action act = () => CreateService();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*JWT密钥配置缺失*");
    }

    [Fact(DisplayName = "构造函数_SecretKey长度不足_应记录警告")]
    public void Constructor_ShortSecretKey_ShouldLogWarning()
    {
        // Arrange
        SetupConfiguration(secretKey: "ShortKey123"); // 少于32字符

        // Act
        var service = CreateService();

        // Assert
        service.Should().NotBeNull();
        // 验证记录了警告日志
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("JWT密钥长度不足32字符")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact(DisplayName = "构造函数_缺少Issuer配置_应抛出异常")]
    public void Constructor_MissingIssuer_ShouldThrowException()
    {
        // Arrange
        ResetConfiguration();
        SetupConfiguration(secretKey: TestSecretKey, issuer: null, audience: TestAudience);

        // Act
        Action act = () => CreateService();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*JWT签发者配置缺失*");
    }

    [Fact(DisplayName = "构造函数_缺少Audience配置_应抛出异常")]
    public void Constructor_MissingAudience_ShouldThrowException()
    {
        // Arrange
        ResetConfiguration();
        SetupConfiguration(secretKey: TestSecretKey, issuer: TestIssuer, audience: null);

        // Act
        Action act = () => CreateService();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*JWT接收者配置缺失*");
    }

    [Fact(DisplayName = "构造函数_ExpirationMinutes无效_应抛出异常")]
    public void Constructor_InvalidExpirationMinutes_ShouldThrowException()
    {
        // Arrange
        SetupConfiguration(expirationMinutes: "invalid");

        // Act
        Action act = () => CreateService();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*JWT过期时间配置无效*");
    }

    [Fact(DisplayName = "构造函数_ExpirationMinutes为负数_应抛出异常")]
    public void Constructor_NegativeExpirationMinutes_ShouldThrowException()
    {
        // Arrange
        SetupConfiguration(expirationMinutes: "-10");

        // Act
        Action act = () => CreateService();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*JWT过期时间配置无效*");
    }

    #endregion

    #region GenerateToken测试

    [Fact(DisplayName = "GenerateToken_有效参数_应生成有效的JWT Token")]
    public void GenerateToken_WithValidParameters_ShouldGenerateValidToken()
    {
        // Arrange
        var service = CreateService();
        var userId = Guid.NewGuid();
        var username = "testuser";
        var email = "test@example.com";

        // Act
        var token = service.GenerateToken(userId, username, email);

        // Assert
        token.Should().NotBeNullOrEmpty();

        // 验证Token格式（JWT由三部分组成，用.分隔）
        var parts = token.Split('.');
        parts.Should().HaveCount(3);

        // 解析Token验证内容
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        // 验证声明
        jwtToken.Subject.Should().Be(userId.ToString());
        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.UniqueName && c.Value == username);
        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == email);
        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti);

        // 验证签发者和接收者
        jwtToken.Issuer.Should().Be(TestIssuer);
        jwtToken.Audiences.Should().Contain(TestAudience);

        // 验证过期时间
        var expectedExpiration = DateTime.UtcNow.AddMinutes(60);
        jwtToken.ValidTo.Should().BeCloseTo(expectedExpiration, TimeSpan.FromMinutes(1));
    }

    [Fact(DisplayName = "GenerateToken_空用户名_应抛出异常")]
    public void GenerateToken_EmptyUsername_ShouldThrowException()
    {
        // Arrange
        var service = CreateService();
        var userId = Guid.NewGuid();

        // Act
        Action act = () => service.GenerateToken(userId, "", "test@example.com");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*用户名不能为空*");
    }

    [Fact(DisplayName = "GenerateToken_空邮箱_应抛出异常")]
    public void GenerateToken_EmptyEmail_ShouldThrowException()
    {
        // Arrange
        var service = CreateService();
        var userId = Guid.NewGuid();

        // Act
        Action act = () => service.GenerateToken(userId, "testuser", "");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*电子邮箱不能为空*");
    }

    [Fact(DisplayName = "GenerateToken_使用默认过期时间_应生成24小时有效的Token")]
    public void GenerateToken_WithoutExpirationConfig_ShouldUseDefaultExpiration()
    {
        // Arrange
        SetupConfiguration(expirationMinutes: null); // 不配置过期时间，但保留其他必需配置
        var service = CreateService();
        var userId = Guid.NewGuid();

        // Act
        var token = service.GenerateToken(userId, "testuser", "test@example.com");

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        // 验证使用默认的24小时（1440分钟）
        var expectedExpiration = DateTime.UtcNow.AddMinutes(1440);
        jwtToken.ValidTo.Should().BeCloseTo(expectedExpiration, TimeSpan.FromMinutes(5));
    }

    [Fact(DisplayName = "GenerateToken_成功生成Token_应记录日志")]
    public void GenerateToken_Success_ShouldLogInformation()
    {
        // Arrange
        var service = CreateService();
        var userId = Guid.NewGuid();
        var username = "testuser";

        // Act
        service.GenerateToken(userId, username, "test@example.com");

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("成功为用户") && v.ToString()!.Contains(username)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact(DisplayName = "GenerateToken_多次调用_应生成不同的Token")]
    public void GenerateToken_MultipleCalls_ShouldGenerateDifferentTokens()
    {
        // Arrange
        var service = CreateService();
        var userId = Guid.NewGuid();

        // Act
        var token1 = service.GenerateToken(userId, "user1", "user1@example.com");
        var token2 = service.GenerateToken(userId, "user1", "user1@example.com");

        // Assert
        token1.Should().NotBe(token2); // 每次生成的Jti不同，Token也应该不同
    }

    #endregion

    #region GetUserIdFromClaims测试

    [Fact(DisplayName = "GetUserIdFromClaims_包含有效的Sub声明_应返回用户ID")]
    public void GetUserIdFromClaims_WithValidSubClaim_ShouldReturnUserId()
    {
        // Arrange
        var expectedUserId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, expectedUserId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var userId = JwtTokenService.GetUserIdFromClaims(principal);

        // Assert
        userId.Should().NotBeNull();
        userId.Should().Be(expectedUserId);
    }

    [Fact(DisplayName = "GetUserIdFromClaims_包含有效的NameIdentifier声明_应返回用户ID")]
    public void GetUserIdFromClaims_WithValidNameIdentifierClaim_ShouldReturnUserId()
    {
        // Arrange
        var expectedUserId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, expectedUserId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var userId = JwtTokenService.GetUserIdFromClaims(principal);

        // Assert
        userId.Should().NotBeNull();
        userId.Should().Be(expectedUserId);
    }

    [Fact(DisplayName = "GetUserIdFromClaims_同时包含两种声明_应优先使用NameIdentifier")]
    public void GetUserIdFromClaims_WithBothClaims_ShouldPreferNameIdentifier()
    {
        // Arrange
        var nameIdentifierId = Guid.NewGuid();
        var subId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, nameIdentifierId.ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, subId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var userId = JwtTokenService.GetUserIdFromClaims(principal);

        // Assert
        userId.Should().Be(nameIdentifierId); // 应该使用NameIdentifier
    }

    [Fact(DisplayName = "GetUserIdFromClaims_不包含用户ID声明_应返回null")]
    public void GetUserIdFromClaims_WithoutUserIdClaim_ShouldReturnNull()
    {
        // Arrange
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "testuser")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var userId = JwtTokenService.GetUserIdFromClaims(principal);

        // Assert
        userId.Should().BeNull();
    }

    [Fact(DisplayName = "GetUserIdFromClaims_用户ID格式无效_应返回null")]
    public void GetUserIdFromClaims_WithInvalidGuidFormat_ShouldReturnNull()
    {
        // Arrange
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, "not-a-valid-guid")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var userId = JwtTokenService.GetUserIdFromClaims(principal);

        // Assert
        userId.Should().BeNull();
    }

    [Fact(DisplayName = "GetUserIdFromClaims_Principal为null_应返回null")]
    public void GetUserIdFromClaims_WithNullPrincipal_ShouldReturnNull()
    {
        // Act
        var userId = JwtTokenService.GetUserIdFromClaims(null!);

        // Assert
        userId.Should().BeNull();
    }

    #endregion

    #region 集成测试

    [Fact(DisplayName = "集成测试_生成Token并解析用户ID_应能正确往返")]
    public void IntegrationTest_GenerateTokenAndExtractUserId_ShouldRoundTrip()
    {
        // Arrange
        var service = CreateService();
        var originalUserId = Guid.NewGuid();
        var username = "testuser";
        var email = "test@example.com";

        // Act - 生成Token
        var token = service.GenerateToken(originalUserId, username, email);

        // 模拟从Token创建ClaimsPrincipal（通常由JWT中间件完成）
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        var identity = new ClaimsIdentity(jwtToken.Claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        // Act - 从ClaimsPrincipal提取用户ID
        var extractedUserId = JwtTokenService.GetUserIdFromClaims(principal);

        // Assert
        extractedUserId.Should().NotBeNull();
        extractedUserId.Should().Be(originalUserId);
    }

    #endregion
}
