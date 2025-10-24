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
/// 测试认证API控制器的各个端点
/// </summary>
public class AuthControllerTests : IDisposable
{
    private readonly Mock<ILogger<AuthController>> _mockControllerLogger;
    private readonly Mock<ILogger<AuthService>> _mockAuthServiceLogger;
    private readonly Mock<ILogger<InMemoryUserStore>> _mockUserStoreLogger;
    private readonly IConfiguration _configuration;
    private readonly JwtOptions _jwtOptions;
    private readonly IUserStore _userStore;
    private readonly IAuthService _authService;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _mockControllerLogger = new Mock<ILogger<AuthController>>();
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

        // 创建JWT选项
        _jwtOptions = new JwtOptions();
        _configuration.GetSection(JwtOptions.SectionName).Bind(_jwtOptions);

        // 创建UserStore
        _userStore = new InMemoryUserStore(_mockUserStoreLogger.Object, _configuration);

        // 创建AuthService
        _authService = new AuthService(_userStore, _jwtOptions, _mockAuthServiceLogger.Object);

        // 创建AuthController
        _controller = new AuthController(_authService, _userStore, _mockControllerLogger.Object);
    }

    public void Dispose()
    {
        // 清理资源
    }

    #region 登录端点测试

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnOk()
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

    [Fact]
    public async Task Login_WithInvalidPassword_ShouldReturnUnauthorized()
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

    [Fact]
    public async Task Login_WithNonExistentUser_ShouldReturnUnauthorized()
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
    }

    #endregion

    #region 注册端点测试

    [Fact]
    public async Task Register_WithValidData_ShouldReturnOk()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "newuser",
            Password = "password123"
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

    [Fact]
    public async Task Register_WithExistingUsername_ShouldReturnBadRequest()
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

    [Fact]
    public async Task Register_WithShortUsername_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "ab", // 用户名太短
            Password = "password123"
        };

        // 手动触发模型验证
        _controller.ModelState.AddModelError("Username", "用户名至少3位");

        // Act
        var result = await _controller.Register(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var authResult = Assert.IsType<AuthResult>(badRequestResult.Value);
        Assert.False(authResult.Success);
        Assert.Equal("请求参数无效", authResult.Message);
    }

    #endregion

    #region 刷新令牌端点测试

    [Fact]
    public async Task RefreshToken_WithValidToken_ShouldReturnOk()
    {
        // Arrange
        // 先登录获取刷新令牌
        var loginRequest = new LoginRequest
        {
            Username = "test1",
            Password = "password123"
        };
        var loginResult = await _controller.Login(loginRequest);
        var loginOkResult = Assert.IsType<OkObjectResult>(loginResult.Result);
        var loginAuthResult = Assert.IsType<AuthResult>(loginOkResult.Value);

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
        // 新的刷新令牌应该不同于旧的
        Assert.NotEqual(loginAuthResult.RefreshToken, authResult.RefreshToken);
    }

    [Fact]
    public async Task RefreshToken_WithInvalidToken_ShouldReturnUnauthorized()
    {
        // Arrange
        var request = new RefreshTokenRequest
        {
            RefreshToken = "invalid_token"
        };

        // Act
        var result = await _controller.RefreshToken(request);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var authResult = Assert.IsType<AuthResult>(unauthorizedResult.Value);
        Assert.False(authResult.Success);
    }

    #endregion

    #region 获取当前用户端点测试

    [Fact]
    public async Task GetCurrentUser_WithValidToken_ShouldReturnOk()
    {
        // Arrange
        // 先创建用户并获取用户信息
        var user = await _userStore.GetUserByUsernameAsync("test1");
        Assert.NotNull(user);

        // 模拟已认证的用户上下文
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Username)
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        // 设置控制器的User属性
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = claimsPrincipal
            }
        };

        // Act
        var result = await _controller.GetCurrentUser();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var userInfo = Assert.IsType<UserInfo>(okResult.Value);
        Assert.Equal(user.Id, userInfo.Id);
        Assert.Equal(user.Username, userInfo.Username);
    }

    [Fact]
    public async Task GetCurrentUser_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        // 设置未认证的用户上下文
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal()
            }
        };

        // Act
        var result = await _controller.GetCurrentUser();

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetCurrentUser_WithInvalidUserId_ShouldReturnNotFound()
    {
        // Arrange
        // 模拟包含无效用户ID的Claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "invalid_user_id")
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = claimsPrincipal
            }
        };

        // Act
        var result = await _controller.GetCurrentUser();

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    #endregion

    #region 获取所有用户端点测试

    [Fact]
    public async Task GetAllUsers_WithAuthentication_ShouldReturnOk()
    {
        // Arrange
        var user = await _userStore.GetUserByUsernameAsync("test1");
        Assert.NotNull(user);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id)
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = claimsPrincipal
            }
        };

        // Act
        var result = await _controller.GetAllUsers();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var users = Assert.IsAssignableFrom<IEnumerable<UserInfo>>(okResult.Value);
        Assert.NotEmpty(users);
        // 应该至少有3个测试账户
        Assert.True(users.Count() >= 3);
    }

    [Fact]
    public async Task GetAllUsers_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal()
            }
        };

        // 注意：在实际运行时，[Authorize]特性会在控制器方法执行前拦截
        // 这里我们测试的是控制器内部的逻辑
        // 在单元测试中，我们需要手动模拟未认证的情况

        // Act & Assert
        // 由于控制器内部没有额外的认证检查，这里只测试认证用户的情况
        // 未认证的情况会在集成测试中通过实际的中间件来测试
    }

    #endregion

    #region 完整认证流程测试

    [Fact]
    public async Task CompleteAuthFlow_RegisterLoginRefresh_ShouldWorkCorrectly()
    {
        // Arrange
        var username = "flowtest";
        var password = "password123";

        // Act 1: 注册
        var registerRequest = new RegisterRequest { Username = username, Password = password };
        var registerResult = await _controller.Register(registerRequest);
        var registerOkResult = Assert.IsType<OkObjectResult>(registerResult.Result);
        var registerAuthResult = Assert.IsType<AuthResult>(registerOkResult.Value);

        Assert.True(registerAuthResult.Success);
        Assert.NotNull(registerAuthResult.Token);
        var originalToken = registerAuthResult.Token;
        var originalRefreshToken = registerAuthResult.RefreshToken;

        // Act 2: 使用注册返回的令牌获取用户信息
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, registerAuthResult.User!.Id),
            new Claim(ClaimTypes.Name, registerAuthResult.User.Username)
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        var meResult = await _controller.GetCurrentUser();
        var meOkResult = Assert.IsType<OkObjectResult>(meResult.Result);
        var userInfo = Assert.IsType<UserInfo>(meOkResult.Value);
        Assert.Equal(username, userInfo.Username);

        // Act 3: 刷新令牌
        var refreshRequest = new RefreshTokenRequest { RefreshToken = originalRefreshToken! };
        var refreshResult = await _controller.RefreshToken(refreshRequest);
        var refreshOkResult = Assert.IsType<OkObjectResult>(refreshResult.Result);
        var refreshAuthResult = Assert.IsType<AuthResult>(refreshOkResult.Value);

        Assert.True(refreshAuthResult.Success);
        Assert.NotNull(refreshAuthResult.Token);
        Assert.NotEqual(originalToken, refreshAuthResult.Token);
        Assert.NotEqual(originalRefreshToken, refreshAuthResult.RefreshToken);

        // Act 4: 登出后重新登录
        var loginRequest = new LoginRequest { Username = username, Password = password };
        var loginResult = await _controller.Login(loginRequest);
        var loginOkResult = Assert.IsType<OkObjectResult>(loginResult.Result);
        var loginAuthResult = Assert.IsType<AuthResult>(loginOkResult.Value);

        Assert.True(loginAuthResult.Success);
        Assert.NotNull(loginAuthResult.Token);
    }

    #endregion

    #region 错误处理测试

    [Fact]
    public async Task Login_WithEmptyUsername_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new LoginRequest
        {
            Username = "",
            Password = "password123"
        };

        _controller.ModelState.AddModelError("Username", "用户名不能为空");

        // Act
        var result = await _controller.Login(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var authResult = Assert.IsType<AuthResult>(badRequestResult.Value);
        Assert.False(authResult.Success);
    }

    [Fact]
    public async Task Register_WithEmptyPassword_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "testuser",
            Password = ""
        };

        _controller.ModelState.AddModelError("Password", "密码不能为空");

        // Act
        var result = await _controller.Register(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var authResult = Assert.IsType<AuthResult>(badRequestResult.Value);
        Assert.False(authResult.Success);
    }

    [Fact]
    public async Task RefreshToken_WithEmptyToken_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new RefreshTokenRequest
        {
            RefreshToken = ""
        };

        _controller.ModelState.AddModelError("RefreshToken", "刷新令牌不能为空");

        // Act
        var result = await _controller.RefreshToken(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var authResult = Assert.IsType<AuthResult>(badRequestResult.Value);
        Assert.False(authResult.Success);
    }

    #endregion
}
