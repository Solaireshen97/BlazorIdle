using Xunit;
using Moq;
using BlazorIdle.Pages;
using BlazorIdle.Services.Auth;
using BlazorIdle.Models.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Bunit;

namespace BlazorIdle.Tests.Pages;

/// <summary>
/// Login页面组件的单元测试
/// 测试登录和注册功能
/// </summary>
public class LoginPageTests : TestContext
{
    private readonly Mock<IAuthenticationService> _mockAuthService;
    private readonly Mock<NavigationManager> _mockNavigationManager;
    private readonly Mock<ILogger<Login>> _mockLogger;

    public LoginPageTests()
    {
        _mockAuthService = new Mock<IAuthenticationService>();
        _mockNavigationManager = new Mock<NavigationManager>();
        _mockLogger = new Mock<ILogger<Login>>();

        // 注册模拟服务
        Services.AddSingleton(_mockAuthService.Object);
        Services.AddSingleton(_mockNavigationManager.Object);
        Services.AddSingleton(_mockLogger.Object);
    }

    /// <summary>
    /// 测试页面能够正常渲染
    /// </summary>
    [Fact]
    public void Login_Page_Renders_Successfully()
    {
        // Act - 渲染Login组件
        var cut = RenderComponent<Login>();

        // Assert - 验证页面包含关键元素
        Assert.Contains("登录", cut.Markup);
        Assert.Contains("注册", cut.Markup);
        Assert.Contains("用户名", cut.Markup);
        Assert.Contains("密码", cut.Markup);
    }

    /// <summary>
    /// 测试默认显示登录表单
    /// </summary>
    [Fact]
    public void Login_Page_Shows_Login_Form_By_Default()
    {
        // Act
        var cut = RenderComponent<Login>();

        // Assert - 验证显示登录按钮而不是注册按钮
        var loginButton = cut.Find("button[type='submit']");
        Assert.Contains("登录", loginButton.TextContent);
    }

    /// <summary>
    /// 测试切换到注册表单
    /// </summary>
    [Fact]
    public void Login_Page_Can_Switch_To_Register_Form()
    {
        // Arrange
        var cut = RenderComponent<Login>();

        // Act - 点击注册标签
        var registerTab = cut.FindAll("a.nav-link")[1];
        registerTab.Click();

        // Assert - 验证显示注册按钮
        var registerButton = cut.Find("button[type='submit']");
        Assert.Contains("注册", registerButton.TextContent);
    }

    /// <summary>
    /// 测试显示测试账户信息
    /// </summary>
    [Fact]
    public void Login_Page_Shows_Test_Account_Info()
    {
        // Act
        var cut = RenderComponent<Login>();

        // Assert - 验证显示测试账户信息
        Assert.Contains("test123", cut.Markup);
        Assert.Contains("admin", cut.Markup);
    }
}
