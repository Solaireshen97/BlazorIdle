using Xunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BlazorIdle.Server.Auth.Services;
using BlazorIdle.Server.Auth.DTOs;
using BlazorIdle.Server.Auth;
using System.Net.Http.Json;
using System.Text.Json;

namespace BlazorIdle.Tests.EndToEnd;

/// <summary>
/// 端到端认证测试
/// 测试完整的登录、注册、Token刷新和登出流程
/// 这些测试验证整个认证系统的集成工作正常
/// </summary>
public class AuthenticationEndToEndTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAuthService _authService;
    private readonly IUserStore _userStore;
    private readonly JwtOptions _jwtOptions;

    public AuthenticationEndToEndTests()
    {
        // 创建测试服务提供程序
        var services = new ServiceCollection();
        
        // 配置日志
        services.AddLogging(builder => builder.AddConsole());
        
        // 配置测试配置
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Auth:BCryptWorkFactor", "12" },
                { "Auth:TestAccounts:0:Username", "test123" },
                { "Auth:TestAccounts:0:Password", "test123123" },
                { "Auth:TestAccounts:1:Username", "admin" },
                { "Auth:TestAccounts:1:Password", "admin123" },
                { "Jwt:SecretKey", "Test_JWT_Secret_Key_For_End_To_End_Tests_32_Characters_Min" },
                { "Jwt:Issuer", "BlazorIdleTestServer" },
                { "Jwt:Audience", "BlazorIdleTestClient" },
                { "Jwt:ExpirationMinutes", "60" },
                { "Jwt:RefreshTokenExpirationDays", "7" }
            })
            .Build();
        
        services.AddSingleton<IConfiguration>(configuration);
        
        // 配置JWT选项
        var jwtOptions = new JwtOptions();
        configuration.GetSection(JwtOptions.SectionName).Bind(jwtOptions);
        jwtOptions.Validate();
        services.AddSingleton(jwtOptions);
        
        // 注册认证服务
        services.AddSingleton<IUserStore, InMemoryUserStore>();
        services.AddScoped<IAuthService, AuthService>();
        
        _serviceProvider = services.BuildServiceProvider();
        _authService = _serviceProvider.GetRequiredService<IAuthService>();
        _userStore = _serviceProvider.GetRequiredService<IUserStore>();
        _jwtOptions = _serviceProvider.GetRequiredService<JwtOptions>();
    }

    #region 登录流程端到端测试

    /// <summary>
    /// 测试：使用测试账户成功登录的完整流程
    /// 验收标准：用户可以使用测试账户登录，获得有效JWT令牌
    /// </summary>
    [Fact]
    public async Task EndToEnd_Login_With_Valid_TestAccount_Should_Succeed()
    {
        // Arrange - 准备测试账户凭据（从配置读取）
        var username = "test123";
        var password = "test123123";

        // Act - 执行登录
        var result = await _authService.LoginAsync(username, password);

        // Assert - 验证登录成功
        Assert.True(result.Success, "登录应该成功");
        Assert.Null(result.Message);
        Assert.NotNull(result.Token);
        Assert.NotNull(result.RefreshToken);
        Assert.NotNull(result.User);
        Assert.Equal(username, result.User.Username);
        Assert.NotNull(result.ExpiresAt);
        Assert.True(result.ExpiresAt > DateTime.UtcNow, "Token过期时间应该在未来");
        
        // 验证Token格式正确（JWT格式：header.payload.signature）
        var tokenParts = result.Token.Split('.');
        Assert.Equal(3, tokenParts.Length);
    }

    /// <summary>
    /// 测试：使用错误密码登录失败
    /// 验收标准：错误密码应该返回失败，不泄露用户是否存在
    /// </summary>
    [Fact]
    public async Task EndToEnd_Login_With_Wrong_Password_Should_Fail()
    {
        // Arrange
        var username = "test123";
        var wrongPassword = "wrongpassword";

        // Act
        var result = await _authService.LoginAsync(username, wrongPassword);

        // Assert
        Assert.False(result.Success, "使用错误密码登录应该失败");
        Assert.NotNull(result.Message);
        Assert.Contains("用户名或密码错误", result.Message);
        Assert.Null(result.Token);
        Assert.Null(result.RefreshToken);
        Assert.Null(result.User);
    }

    /// <summary>
    /// 测试：使用不存在的用户名登录失败
    /// 验收标准：不存在的用户应该返回失败，防止用户名枚举攻击
    /// </summary>
    [Fact]
    public async Task EndToEnd_Login_With_Nonexistent_User_Should_Fail()
    {
        // Arrange
        var username = "nonexistentuser";
        var password = "anypassword";

        // Act
        var result = await _authService.LoginAsync(username, password);

        // Assert
        Assert.False(result.Success, "使用不存在的用户登录应该失败");
        Assert.NotNull(result.Message);
        Assert.Contains("用户名或密码错误", result.Message);
        Assert.Null(result.Token);
    }

    /// <summary>
    /// 测试：登录后更新最后登录时间
    /// 验收标准：成功登录后应该更新用户的LastLoginAt字段
    /// </summary>
    [Fact]
    public async Task EndToEnd_Login_Should_Update_LastLoginTime()
    {
        // Arrange
        var username = "test123";
        var password = "test123123";
        var beforeLogin = DateTime.UtcNow;

        // Act
        var result = await _authService.LoginAsync(username, password);
        
        // 等待一小段时间确保时间更新
        await Task.Delay(10);
        
        // 获取用户信息验证
        var user = await _userStore.GetUserByUsernameAsync(username);

        // Assert
        Assert.NotNull(user);
        Assert.NotNull(user.LastLoginAt);
        Assert.True(user.LastLoginAt >= beforeLogin, "最后登录时间应该被更新");
    }

    #endregion

    #region 注册流程端到端测试

    /// <summary>
    /// 测试：新用户注册成功的完整流程
    /// 验收标准：用户可以注册新账户并自动获得JWT令牌
    /// </summary>
    [Fact]
    public async Task EndToEnd_Register_New_User_Should_Succeed()
    {
        // Arrange
        var username = $"newuser_{Guid.NewGuid():N}"; // 使用唯一用户名避免冲突
        var password = "newpassword123";

        // Act
        var result = await _authService.RegisterAsync(username, password);

        // Assert - 验证注册成功
        Assert.True(result.Success, "注册应该成功");
        Assert.Null(result.Message);
        Assert.NotNull(result.Token);
        Assert.NotNull(result.RefreshToken);
        Assert.NotNull(result.User);
        Assert.Equal(username, result.User.Username);
        
        // 验证用户已被创建
        var user = await _userStore.GetUserByUsernameAsync(username);
        Assert.NotNull(user);
        Assert.Equal(username, user.Username);
    }

    /// <summary>
    /// 测试：注册已存在的用户名失败
    /// 验收标准：使用已存在的用户名注册应该返回失败
    /// </summary>
    [Fact]
    public async Task EndToEnd_Register_With_Existing_Username_Should_Fail()
    {
        // Arrange - 使用已存在的测试账户用户名
        var username = "test123";
        var password = "anypassword";

        // Act
        var result = await _authService.RegisterAsync(username, password);

        // Assert
        Assert.False(result.Success, "使用已存在的用户名注册应该失败");
        Assert.NotNull(result.Message);
        Assert.Contains("已存在", result.Message);
        Assert.Null(result.Token);
    }

    /// <summary>
    /// 测试：注册后密码被正确哈希
    /// 验收标准：注册的密码应该使用BCrypt哈希存储，不应明文存储
    /// </summary>
    [Fact]
    public async Task EndToEnd_Register_Should_Hash_Password()
    {
        // Arrange
        var username = $"testuser_{Guid.NewGuid():N}";
        var password = "testpassword123";

        // Act
        var result = await _authService.RegisterAsync(username, password);
        var user = await _userStore.GetUserByUsernameAsync(username);

        // Assert
        Assert.NotNull(user);
        Assert.NotEqual(password, user.PasswordHash); // 密码不应明文存储
        Assert.StartsWith("$2", user.PasswordHash); // BCrypt哈希以$2开头
        
        // 验证可以使用原密码验证
        var isValid = await _userStore.ValidatePasswordAsync(username, password);
        Assert.True(isValid, "应该能用原密码验证");
    }

    #endregion

    #region Token刷新端到端测试

    /// <summary>
    /// 测试：使用刷新令牌获取新的访问令牌
    /// 验收标准：令牌刷新机制正常工作，可以获取新Token
    /// </summary>
    [Fact]
    public async Task EndToEnd_RefreshToken_Should_Return_New_Token()
    {
        // Arrange - 先登录获取刷新令牌
        var loginResult = await _authService.LoginAsync("test123", "test123123");
        Assert.NotNull(loginResult.RefreshToken);
        var oldToken = loginResult.Token;
        var oldRefreshToken = loginResult.RefreshToken;

        // 等待一小段时间确保新Token不同
        await Task.Delay(100);

        // Act - 使用刷新令牌获取新Token
        var refreshResult = await _authService.RefreshTokenAsync(oldRefreshToken);

        // Assert
        Assert.True(refreshResult.Success, "刷新Token应该成功");
        Assert.NotNull(refreshResult.Token);
        Assert.NotNull(refreshResult.RefreshToken);
        Assert.NotEqual(oldToken, refreshResult.Token); // 新Token应该不同
        Assert.NotEqual(oldRefreshToken, refreshResult.RefreshToken); // 新刷新Token应该不同
    }

    /// <summary>
    /// 测试：使用无效的刷新令牌失败
    /// 验收标准：无效或过期的刷新令牌应该被拒绝
    /// </summary>
    [Fact]
    public async Task EndToEnd_RefreshToken_With_Invalid_Token_Should_Fail()
    {
        // Arrange
        var invalidRefreshToken = "invalid_refresh_token";

        // Act
        var result = await _authService.RefreshTokenAsync(invalidRefreshToken);

        // Assert
        Assert.False(result.Success, "使用无效刷新令牌应该失败");
        Assert.NotNull(result.Message);
        Assert.Null(result.Token);
    }

    /// <summary>
    /// 测试：刷新令牌使旧的刷新令牌失效
    /// 验收标准：刷新Token后，旧的刷新令牌应该不能再次使用
    /// </summary>
    [Fact]
    public async Task EndToEnd_RefreshToken_Should_Invalidate_Old_Token()
    {
        // Arrange
        var loginResult = await _authService.LoginAsync("test123", "test123123");
        var oldRefreshToken = loginResult.RefreshToken!;
        
        // Act - 第一次刷新成功
        var firstRefresh = await _authService.RefreshTokenAsync(oldRefreshToken);
        Assert.True(firstRefresh.Success);
        
        // 尝试再次使用旧的刷新令牌
        var secondRefresh = await _authService.RefreshTokenAsync(oldRefreshToken);

        // Assert - 旧的刷新令牌应该已失效
        Assert.False(secondRefresh.Success, "旧的刷新令牌应该不能再次使用");
    }

    #endregion

    #region JWT Token验证测试

    /// <summary>
    /// 测试：生成的JWT令牌可以被验证
    /// 验收标准：生成的Token应该能通过签名验证并提取Claims
    /// </summary>
    [Fact]
    public async Task EndToEnd_JWT_Token_Should_Be_Verifiable()
    {
        // Arrange & Act
        var result = await _authService.LoginAsync("test123", "test123123");
        Assert.NotNull(result.Token);

        // Verify - 验证Token
        var principal = _authService.ValidateToken(result.Token);

        // Assert
        Assert.NotNull(principal);
        var userIdClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        var usernameClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.Name);
        
        Assert.NotNull(userIdClaim);
        Assert.NotNull(usernameClaim);
        Assert.Equal("test123", usernameClaim.Value);
    }

    /// <summary>
    /// 测试：Token包含正确的过期时间
    /// 验收标准：Token的过期时间应该符合配置的ExpirationMinutes
    /// </summary>
    [Fact]
    public async Task EndToEnd_JWT_Token_Should_Have_Correct_Expiration()
    {
        // Arrange & Act
        var beforeLogin = DateTime.UtcNow;
        var result = await _authService.LoginAsync("test123", "test123123");
        var afterLogin = DateTime.UtcNow;

        // Assert
        Assert.NotNull(result.ExpiresAt);
        
        // 过期时间应该是当前时间 + ExpirationMinutes（允许一些误差）
        var expectedExpiration = beforeLogin.AddMinutes(_jwtOptions.ExpirationMinutes);
        var timeDifference = Math.Abs((result.ExpiresAt.Value - expectedExpiration).TotalMinutes);
        
        Assert.True(timeDifference < 1, $"Token过期时间应该接近{_jwtOptions.ExpirationMinutes}分钟后");
    }

    /// <summary>
    /// 测试：篡改的Token无法通过验证
    /// 验收标准：修改Token内容后签名验证应该失败
    /// </summary>
    [Fact]
    public async Task EndToEnd_Tampered_JWT_Token_Should_Fail_Validation()
    {
        // Arrange
        var result = await _authService.LoginAsync("test123", "test123123");
        Assert.NotNull(result.Token);
        
        // Act - 篡改Token（修改最后一个字符）
        var tamperedToken = result.Token.Substring(0, result.Token.Length - 1) + "X";
        var principal = _authService.ValidateToken(tamperedToken);

        // Assert
        Assert.Null(principal); // 篡改的Token应该验证失败
    }

    #endregion

    #region 性能测试

    /// <summary>
    /// 测试：登录响应时间应该在可接受范围内
    /// 验收标准：登录操作应该在500ms内完成（BCrypt验证较慢但安全）
    /// </summary>
    [Fact]
    public async Task EndToEnd_Login_Should_Complete_Within_Acceptable_Time()
    {
        // Arrange
        var username = "test123";
        var password = "test123123";
        var startTime = DateTime.UtcNow;

        // Act
        var result = await _authService.LoginAsync(username, password);
        var endTime = DateTime.UtcNow;
        var duration = (endTime - startTime).TotalMilliseconds;

        // Assert
        Assert.True(result.Success);
        // BCrypt验证需要时间，500ms是合理的上限
        Assert.True(duration < 500, $"登录应该在500ms内完成，实际用时：{duration:F2}ms");
    }

    /// <summary>
    /// 测试：Token验证应该快速完成
    /// 验收标准：Token验证应该在10ms内完成
    /// </summary>
    [Fact]
    public async Task EndToEnd_Token_Validation_Should_Be_Fast()
    {
        // Arrange
        var loginResult = await _authService.LoginAsync("test123", "test123123");
        Assert.NotNull(loginResult.Token);
        
        var startTime = DateTime.UtcNow;

        // Act - 执行多次验证取平均值
        for (int i = 0; i < 100; i++)
        {
            _authService.ValidateToken(loginResult.Token);
        }
        
        var endTime = DateTime.UtcNow;
        var avgDuration = (endTime - startTime).TotalMilliseconds / 100;

        // Assert
        Assert.True(avgDuration < 10, $"Token验证应该在10ms内完成，实际平均用时：{avgDuration}ms");
    }

    #endregion

    #region 安全性测试

    /// <summary>
    /// 测试：密码验证防止时序攻击
    /// 验收标准：不存在的用户和错误密码的验证时间应该相似
    /// </summary>
    [Fact]
    public async Task EndToEnd_Password_Validation_Should_Prevent_Timing_Attacks()
    {
        // Arrange - 测试多次以获得稳定的平均值
        var iterations = 10;
        var validUserTimes = new List<double>();
        var invalidUserTimes = new List<double>();

        // Act - 测试存在的用户（错误密码）
        for (int i = 0; i < iterations; i++)
        {
            var start = DateTime.UtcNow;
            await _userStore.ValidatePasswordAsync("test123", "wrongpassword");
            var end = DateTime.UtcNow;
            validUserTimes.Add((end - start).TotalMilliseconds);
        }

        // 测试不存在的用户
        for (int i = 0; i < iterations; i++)
        {
            var start = DateTime.UtcNow;
            await _userStore.ValidatePasswordAsync("nonexistentuser", "anypassword");
            var end = DateTime.UtcNow;
            invalidUserTimes.Add((end - start).TotalMilliseconds);
        }

        // Assert - 两种情况的平均验证时间应该相近（差异小于50%）
        var validUserAvg = validUserTimes.Average();
        var invalidUserAvg = invalidUserTimes.Average();
        var difference = Math.Abs(validUserAvg - invalidUserAvg);
        var percentDifference = difference / Math.Max(validUserAvg, invalidUserAvg) * 100;

        Assert.True(percentDifference < 50, 
            $"验证时间差异应该小于50%以防止时序攻击，实际差异：{percentDifference:F2}%");
    }

    /// <summary>
    /// 测试：密码使用BCrypt正确加密
    /// 验收标准：密码应该使用配置的work factor进行BCrypt哈希
    /// </summary>
    [Fact]
    public async Task EndToEnd_Password_Should_Use_BCrypt_With_Correct_WorkFactor()
    {
        // Arrange
        var username = $"testuser_{Guid.NewGuid():N}";
        var password = "testpassword123";

        // Act
        await _authService.RegisterAsync(username, password);
        var user = await _userStore.GetUserByUsernameAsync(username);

        // Assert
        Assert.NotNull(user);
        // BCrypt哈希格式：$2a$[cost]$[22 character salt][31 character hash]
        Assert.Matches(@"^\$2[aby]\$\d{2}\$[./A-Za-z0-9]{53}$", user.PasswordHash);
        
        // 验证work factor（从配置读取，默认12）
        var workFactor = user.PasswordHash.Split('$')[2];
        Assert.Equal("12", workFactor);
    }

    #endregion

    #region 并发测试

    /// <summary>
    /// 测试：并发登录应该线程安全
    /// 验收标准：多个用户同时登录不应该产生竞态条件
    /// </summary>
    [Fact]
    public async Task EndToEnd_Concurrent_Logins_Should_Be_Thread_Safe()
    {
        // Arrange
        var tasks = new List<Task<AuthResult>>();
        var userCount = 10;

        // Act - 并发执行多个登录请求
        for (int i = 0; i < userCount; i++)
        {
            tasks.Add(_authService.LoginAsync("test123", "test123123"));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - 所有登录应该都成功
        Assert.All(results, result =>
        {
            Assert.True(result.Success);
            Assert.NotNull(result.Token);
            Assert.NotNull(result.RefreshToken);
        });

        // 每个结果应该有唯一的Token和RefreshToken
        var tokens = results.Select(r => r.Token).ToList();
        var refreshTokens = results.Select(r => r.RefreshToken).ToList();
        
        Assert.Equal(userCount, tokens.Distinct().Count());
        Assert.Equal(userCount, refreshTokens.Distinct().Count());
    }

    #endregion
}
