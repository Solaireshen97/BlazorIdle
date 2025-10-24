using Xunit;
using Moq;
using BlazorIdle.Components;
using BlazorIdle.Services.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Bunit;

namespace BlazorIdle.Tests.Components;

/// <summary>
/// AuthenticationGuard组件的单元测试
/// 测试认证拦截和页面保护功能
/// </summary>
public class AuthenticationGuardTests : TestContext
{
    private readonly Mock<IAuthenticationService> _mockAuthService;
    private readonly Mock<NavigationManager> _mockNavigationManager;
    private readonly Mock<ILogger<AuthenticationGuard>> _mockLogger;

    public AuthenticationGuardTests()
    {
        _mockAuthService = new Mock<IAuthenticationService>();
        _mockNavigationManager = new Mock<NavigationManager>();
        _mockLogger = new Mock<ILogger<AuthenticationGuard>>();

        // 设置NavigationManager的Uri属性
        _mockNavigationManager.Setup(x => x.Uri).Returns("https://localhost/test-page");
        _mockNavigationManager.Setup(x => x.ToBaseRelativePath(It.IsAny<string>())).Returns("test-page");

        // 注册模拟服务
        Services.AddSingleton(_mockAuthService.Object);
        Services.AddSingleton(_mockNavigationManager.Object);
        Services.AddSingleton(_mockLogger.Object);
    }

    /// <summary>
    /// 测试未认证用户被重定向到登录页面
    /// </summary>
    [Fact]
    public void AuthenticationGuard_Redirects_Unauthenticated_Users()
    {
        // Arrange - 设置用户未登录
        _mockAuthService
            .Setup(x => x.IsAuthenticatedAsync())
            .ReturnsAsync(false);

        string? navigatedUrl = null;
        _mockNavigationManager
            .Setup(x => x.NavigateTo(It.IsAny<string>(), It.IsAny<bool>()))
            .Callback<string, bool>((url, force) => navigatedUrl = url);

        // Act - 渲染AuthenticationGuard组件
        var cut = RenderComponent<AuthenticationGuard>(parameters => parameters
            .Add(p => p.ChildContent, builder =>
            {
                builder.AddContent(0, "Protected Content");
            }));

        // Assert - 验证重定向到登录页面
        Assert.NotNull(navigatedUrl);
        Assert.StartsWith("/login", navigatedUrl);
        Assert.Contains("returnUrl=", navigatedUrl);
    }

    /// <summary>
    /// 测试已认证用户可以访问受保护内容
    /// </summary>
    [Fact]
    public void AuthenticationGuard_Shows_Content_For_Authenticated_Users()
    {
        // Arrange - 设置用户已登录
        _mockAuthService
            .Setup(x => x.IsAuthenticatedAsync())
            .ReturnsAsync(true);

        // Act - 渲染AuthenticationGuard组件
        var cut = RenderComponent<AuthenticationGuard>(parameters => parameters
            .Add(p => p.ChildContent, builder =>
            {
                builder.AddContent(0, "Protected Content");
            }));

        // 等待异步初始化完成
        cut.WaitForState(() => !cut.Markup.Contains("验证身份"));

        // Assert - 验证显示受保护的内容
        Assert.Contains("Protected Content", cut.Markup);
    }

    /// <summary>
    /// 测试RequireAuthentication=false时不进行认证检查
    /// </summary>
    [Fact]
    public void AuthenticationGuard_Skips_Check_When_Not_Required()
    {
        // Arrange - 不设置认证服务的返回值，因为不应该被调用

        // Act - 渲染AuthenticationGuard组件，设置RequireAuthentication=false
        var cut = RenderComponent<AuthenticationGuard>(parameters => parameters
            .Add(p => p.RequireAuthentication, false)
            .Add(p => p.ChildContent, builder =>
            {
                builder.AddContent(0, "Public Content");
            }));

        // Assert - 验证显示内容，不调用认证服务
        Assert.Contains("Public Content", cut.Markup);
        _mockAuthService.Verify(x => x.IsAuthenticatedAsync(), Times.Never);
    }

    /// <summary>
    /// 测试在检查认证状态时显示加载指示器
    /// </summary>
    [Fact]
    public void AuthenticationGuard_Shows_Loading_Indicator()
    {
        // Arrange - 设置一个较长的异步操作模拟检查过程
        var tcs = new TaskCompletionSource<bool>();
        _mockAuthService
            .Setup(x => x.IsAuthenticatedAsync())
            .Returns(tcs.Task);

        // Act - 渲染AuthenticationGuard组件
        var cut = RenderComponent<AuthenticationGuard>(parameters => parameters
            .Add(p => p.ChildContent, builder =>
            {
                builder.AddContent(0, "Protected Content");
            }));

        // Assert - 验证显示加载指示器
        Assert.Contains("验证身份", cut.Markup);
        Assert.Contains("spinner", cut.Markup);

        // Cleanup - 完成任务以避免测试挂起
        tcs.SetResult(true);
    }
}
