using BlazorIdle.Server.Api;
using BlazorIdle.Server.Auth;
using BlazorIdle.Server.Auth.DTOs;
using BlazorIdle.Server.Auth.Models;
using BlazorIdle.Server.Auth.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace BlazorIdle.Tests.Auth;

/// <summary>
/// AuthController单元测试
/// 测试认证控制器的各个API端点
/// </summary>
public class AuthControllerTests : IDisposable
{
    private readonly Mock<ILogger<AuthController>> _mockLogger;
    private readonly Mock<ILogger<AuthService>> _mockAuthServiceLogger;
    private readonly Mock<ILogger<InMemoryUserStore>> _mockUserStoreLogger;
    private readonly IConfiguration _configuration;
    private readonly JwtOptions _jwtOptions;
    private readonly IUserStore _userStore;
    private readonly IAuthService _authService;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _mockLogger = new Mock<ILogger<AuthController>>();
        _mockAuthServiceLogger = new Mock<ILogger<AuthService>>();
        _mockUserStoreLogger = new Mock<ILogger<InMemoryUserStore>>();

        // 创建测试配置
        var configData = new Dictionary<string, string>
        {
            { "Auth:BCryptWorkFactor", "12" },
            { "Auth:TestAccounts:0:Username", "test1" },
            { "Auth:TestAccounts:0:Password", "password123" },
            { "Auth:TestAccounts:1:Username", "test2" },
            { "Auth:TestAccounts:1:Password", "password123" },
            { "Auth:TestAccounts:2:Username", "admin" },
            { "Auth:TestAccounts:2:Password", "admin123" },
            { "Jwt:SecretKey", "TestSecretKey_At_Least_32_Characters_Long_For_Security" },
            { "Jwt:Issuer", "TestIssuer" },
            { "Jwt:Audience", "TestAudience" },
            { "Jwt:ExpirationMinutes", "60" },
            { "Jwt:RefreshTokenExpirationDays", "7" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        // 配置JWT选项
        _jwtOptions = new JwtOptions();
        _configuration.GetSection(JwtOptions.SectionName).Bind(_jwtOptions);

        // 创建依赖服务
        _userStore = new InMemoryUserStore(_mockUserStoreLogger.Object, _configuration);
        _authService = new AuthService(_userStore, _jwtOptions, _mockAuthServiceLogger.Object);
        
        // 创建控制器
        _controller = new AuthController(_authService, _userStore, _mockLogger.Object);
    }

    public void Dispose()
    {
        // 清理资源
        GC.SuppressFinalize(this);
    }

    #region 登录测试

    /// <summary>
    /// 测试登录成功场景
    /// </summary>
    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithToken()
    {
        // Arrange
        var request = new LoginRequest
        {
            Username = "test1",
            Password = "password123"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var authResult = Assert.IsType<AuthResult>(okResult.Value);
        
        Assert.True(authResult.Success);
        Assert.NotNull(authResult.Token);
        Assert.NotNull(authResult.RefreshToken);
        Assert.NotNull(authResult.User);
        Assert.Equal("test1", authResult.User.Username);
    }

    /// <summary>
    /// 测试用户名或密码错误的登录场景
    /// </summary>
    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest
        {
            Username = "test1",
            Password = "wrongpassword"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var authResult = Assert.IsType<AuthResult>(unauthorizedResult.Value);
        
        Assert.False(authResult.Success);
        Assert.Null(authResult.Token);
    }

    /// <summary>
    /// 测试不存在的用户登录场景
    /// </summary>
    [Fact]
    public async Task Login_WithNonExistentUser_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest
        {
            Username = "nonexistent",
            Password = "password123"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var authResult = Assert.IsType<AuthResult>(unauthorizedResult.Value);
        
        Assert.False(authResult.Success);
        Assert.Null(authResult.Token);
    }

    #endregion

    #region 注册测试

    /// <summary>
    /// 测试注册成功场景
    /// </summary>
    [Fact]
    public async Task Register_WithValidData_ReturnsOkWithToken()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "newuser",
            Password = "newpassword123"
        };

        // Act
        var result = await _controller.Register(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var authResult = Assert.IsType<AuthResult>(okResult.Value);
        
        Assert.True(authResult.Success);
        Assert.NotNull(authResult.Token);
        Assert.NotNull(authResult.RefreshToken);
        Assert.NotNull(authResult.User);
        Assert.Equal("newuser", authResult.User.Username);
    }

    /// <summary>
    /// 测试注册已存在的用户名
    /// </summary>
    [Fact]
    public async Task Register_WithExistingUsername_ReturnsBadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "test1", // 已存在的用户名
            Password = "password123"
        };

        // Act
        var result = await _controller.Register(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var authResult = Assert.IsType<AuthResult>(badRequestResult.Value);
        
        Assert.False(authResult.Success);
        Assert.Contains("已存在", authResult.Message);
    }

    /// <summary>
    /// 测试注册后可以立即登录
    /// </summary>
    [Fact]
    public async Task Register_ThenLogin_ShouldSucceed()
    {
        // Arrange
        var registerRequest = new RegisterRequest
        {
            Username = "testuser2",
            Password = "testpass123"
        };

        // Act - 注册
        var registerResult = await _controller.Register(registerRequest);
        var registerOk = Assert.IsType<OkObjectResult>(registerResult.Result);
        var registerAuthResult = Assert.IsType<AuthResult>(registerOk.Value);
        
        Assert.True(registerAuthResult.Success);

        // Act - 登录
        var loginRequest = new LoginRequest
        {
            Username = "testuser2",
            Password = "testpass123"
        };
        var loginResult = await _controller.Login(loginRequest);

        // Assert
        var loginOk = Assert.IsType<OkObjectResult>(loginResult.Result);
        var loginAuthResult = Assert.IsType<AuthResult>(loginOk.Value);
        
        Assert.True(loginAuthResult.Success);
        Assert.NotNull(loginAuthResult.Token);
    }

    #endregion

    #region 刷新令牌测试

    /// <summary>
    /// 测试刷新令牌成功场景
    /// </summary>
    [Fact]
    public async Task RefreshToken_WithValidToken_ReturnsNewToken()
    {
        // Arrange - 先登录获取刷新令牌
        var loginRequest = new LoginRequest
        {
            Username = "test1",
            Password = "password123"
        };
        var loginResult = await _controller.Login(loginRequest);
        var loginOk = Assert.IsType<OkObjectResult>(loginResult.Result);
        var loginAuthResult = Assert.IsType<AuthResult>(loginOk.Value);
        
        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = loginAuthResult.RefreshToken!
        };

        // Act
        var result = await _controller.RefreshToken(refreshRequest);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var authResult = Assert.IsType<AuthResult>(okResult.Value);
        
        Assert.True(authResult.Success);
        Assert.NotNull(authResult.Token);
        Assert.NotNull(authResult.RefreshToken);
        // 新令牌应该与旧令牌不同
        Assert.NotEqual(loginAuthResult.Token, authResult.Token);
    }

    /// <summary>
    /// 测试使用无效刷新令牌
    /// </summary>
    [Fact]
    public async Task RefreshToken_WithInvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        var request = new RefreshTokenRequest
        {
            RefreshToken = "invalid_refresh_token"
        };

        // Act
        var result = await _controller.RefreshToken(request);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var authResult = Assert.IsType<AuthResult>(unauthorizedResult.Value);
        
        Assert.False(authResult.Success);
    }

    #endregion

    #region 获取当前用户测试

    /// <summary>
    /// 测试获取当前用户信息成功场景
    /// </summary>
    [Fact]
    public async Task GetCurrentUser_WithValidToken_ReturnsUserInfo()
    {
        // Arrange - 先登录获取用户ID
        var loginRequest = new LoginRequest
        {
            Username = "test1",
            Password = "password123"
        };
        var loginResult = await _controller.Login(loginRequest);
        var loginOk = Assert.IsType<OkObjectResult>(loginResult.Result);
        var loginAuthResult = Assert.IsType<AuthResult>(loginOk.Value);

        // 模拟JWT认证，设置User Claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, loginAuthResult.User!.Id),
            new Claim(ClaimTypes.Name, loginAuthResult.User.Username)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };

        // Act
        var result = await _controller.GetCurrentUser();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var userInfo = Assert.IsType<UserInfo>(okResult.Value);
        
        Assert.Equal("test1", userInfo.Username);
        Assert.Equal(loginAuthResult.User.Id, userInfo.Id);
    }

    /// <summary>
    /// 测试未授权访问获取当前用户
    /// </summary>
    [Fact]
    public async Task GetCurrentUser_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange - 不设置User Claims
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Act
        var result = await _controller.GetCurrentUser();

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    #endregion

    #region 获取所有用户测试

    /// <summary>
    /// 测试获取所有用户列表
    /// </summary>
    [Fact]
    public async Task GetAllUsers_WithAuth_ReturnsUserList()
    {
        // Arrange - 模拟已认证用户
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "test_user_id"),
            new Claim(ClaimTypes.Name, "test1")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };

        // Act
        var result = await _controller.GetAllUsers();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var users = Assert.IsAssignableFrom<IEnumerable<UserInfo>>(okResult.Value);
        
        // 应该至少有3个预设的测试账户
        Assert.True(users.Count() >= 3);
        Assert.Contains(users, u => u.Username == "test1");
        Assert.Contains(users, u => u.Username == "test2");
        Assert.Contains(users, u => u.Username == "admin");
    }

    #endregion

    #region JWT令牌验证测试

    /// <summary>
    /// 测试JWT令牌包含正确的Claims
    /// </summary>
    [Fact]
    public async Task Login_TokenContainsCorrectClaims()
    {
        // Arrange
        var request = new LoginRequest
        {
            Username = "test1",
            Password = "password123"
        };

        // Act
        var result = await _controller.Login(request);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var authResult = Assert.IsType<AuthResult>(okResult.Value);

        // Assert - 验证Token中的Claims
        var principal = _authService.ValidateToken(authResult.Token!);
        Assert.NotNull(principal);
        
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
        var usernameClaim = principal.FindFirst(ClaimTypes.Name);
        
        Assert.NotNull(userIdClaim);
        Assert.NotNull(usernameClaim);
        Assert.Equal("test1", usernameClaim.Value);
    }

    /// <summary>
    /// 测试Token过期时间设置正确
    /// </summary>
    [Fact]
    public async Task Login_TokenExpirationIsCorrect()
    {
        // Arrange
        var request = new LoginRequest
        {
            Username = "test1",
            Password = "password123"
        };

        // Act
        var result = await _controller.Login(request);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var authResult = Assert.IsType<AuthResult>(okResult.Value);

        // Assert
        Assert.NotNull(authResult.ExpiresAt);
        
        // 验证过期时间大约是60分钟后（允许1分钟误差）
        var expectedExpiration = DateTime.UtcNow.AddMinutes(60);
        var timeDifference = Math.Abs((authResult.ExpiresAt.Value - expectedExpiration).TotalMinutes);
        Assert.True(timeDifference < 1, $"Token过期时间误差过大: {timeDifference}分钟");
    }

    #endregion

    #region 安全性测试

    /// <summary>
    /// 测试密码不会在响应中泄露
    /// </summary>
    [Fact]
    public async Task Login_DoesNotExposePassword()
    {
        // Arrange
        var request = new LoginRequest
        {
            Username = "test1",
            Password = "password123"
        };

        // Act
        var result = await _controller.Login(request);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var authResult = Assert.IsType<AuthResult>(okResult.Value);

        // Assert - 用户信息中不应包含密码或密码哈希
        Assert.NotNull(authResult.User);
        var userProperties = authResult.User.GetType().GetProperties();
        Assert.DoesNotContain(userProperties, p => 
            p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Hash", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 测试GetAllUsers不会暴露敏感信息
    /// </summary>
    [Fact]
    public async Task GetAllUsers_DoesNotExposeSensitiveData()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "test_id"),
            new Claim(ClaimTypes.Name, "test1")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        // Act
        var result = await _controller.GetAllUsers();
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var users = Assert.IsAssignableFrom<IEnumerable<UserInfo>>(okResult.Value);

        // Assert - 确保用户信息不包含敏感数据
        foreach (var user in users)
        {
            var userProperties = user.GetType().GetProperties();
            Assert.DoesNotContain(userProperties, p => 
                p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains("Hash", StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains("RefreshToken", StringComparison.OrdinalIgnoreCase));
        }
    }

    #endregion
}
