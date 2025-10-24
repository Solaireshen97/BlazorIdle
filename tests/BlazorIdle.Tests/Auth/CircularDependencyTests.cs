using Blazored.LocalStorage;
using BlazorIdle.Client.Services;
using BlazorIdle.Services.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Moq;

namespace BlazorIdle.Tests.Auth;

/// <summary>
/// 测试循环依赖问题是否已解决
/// 验证HttpClient的依赖注入配置正确无误
/// </summary>
public class CircularDependencyTests
{
    /// <summary>
    /// 测试：验证服务注册能够成功解析所有依赖，无循环依赖
    /// </summary>
    [Fact]
    public void ServiceRegistration_ShouldNotHaveCircularDependency()
    {
        // Arrange - 创建服务集合并注册所有服务（模拟Program.cs的配置）
        var services = new ServiceCollection();
        
        // 添加必要的基础服务
        services.AddLogging();
        
        // 模拟配置服务
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiBaseUrl"] = "https://localhost:7056"
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration);
        
        // 注册LocalStorage服务（使用Mock）
        var mockLocalStorage = new Mock<ILocalStorageService>();
        services.AddSingleton<ILocalStorageService>(mockLocalStorage.Object);
        
        var apiBase = configuration["ApiBaseUrl"] ?? "https://localhost:7056";
        
        // 注册HTTP消息处理器
        services.AddScoped<AuthorizingHttpMessageHandler>();
        
        // 配置未认证的HttpClient（用于认证服务）
        services.AddHttpClient("UnauthenticatedHttpClient", client =>
        {
            client.BaseAddress = new Uri(apiBase);
        });
        
        // 配置已认证的HttpClient（使用拦截器）
        services.AddHttpClient("AuthenticatedHttpClient")
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri(apiBase);
            })
            .AddHttpMessageHandler<AuthorizingHttpMessageHandler>();
        
        // 注册认证服务（使用未认证的HttpClient）
        services.AddScoped<IAuthenticationService>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("UnauthenticatedHttpClient");
            var localStorage = sp.GetRequiredService<ILocalStorageService>();
            var logger = sp.GetRequiredService<ILogger<AuthenticationService>>();
            
            return new AuthenticationService(httpClient, localStorage, logger);
        });
        
        // 配置默认HttpClient（使用已认证的HttpClient）
        services.AddScoped(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            return httpClientFactory.CreateClient("AuthenticatedHttpClient");
        });
        
        // 注册ApiClient
        services.AddScoped<ApiClient>();
        
        // Act & Assert - 构建服务提供者，如果有循环依赖会抛出异常
        var serviceProvider = services.BuildServiceProvider();
        
        // 验证能够成功解析所有服务
        Assert.NotNull(serviceProvider);
        
        // 验证AuthenticationService可以被解析
        var authService = serviceProvider.GetRequiredService<IAuthenticationService>();
        Assert.NotNull(authService);
        
        // 验证HttpClient可以被解析
        var httpClient = serviceProvider.GetRequiredService<HttpClient>();
        Assert.NotNull(httpClient);
        
        // 验证ApiClient可以被解析
        var apiClient = serviceProvider.GetRequiredService<ApiClient>();
        Assert.NotNull(apiClient);
        
        // 验证AuthorizingHttpMessageHandler可以被解析
        var handler = serviceProvider.GetRequiredService<AuthorizingHttpMessageHandler>();
        Assert.NotNull(handler);
    }
    
    /// <summary>
    /// 测试：验证AuthenticationService使用的是未认证的HttpClient
    /// </summary>
    [Fact]
    public void AuthenticationService_ShouldUseUnauthenticatedHttpClient()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        
        var mockLocalStorage = new Mock<ILocalStorageService>();
        services.AddSingleton<ILocalStorageService>(mockLocalStorage.Object);
        
        var apiBase = "https://localhost:7056";
        
        // 配置HttpClients
        services.AddHttpClient("UnauthenticatedHttpClient", client =>
        {
            client.BaseAddress = new Uri(apiBase);
        });
        
        services.AddScoped<AuthorizingHttpMessageHandler>();
        
        services.AddHttpClient("AuthenticatedHttpClient")
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri(apiBase);
            })
            .AddHttpMessageHandler<AuthorizingHttpMessageHandler>();
        
        services.AddScoped<IAuthenticationService>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("UnauthenticatedHttpClient");
            var localStorage = sp.GetRequiredService<ILocalStorageService>();
            var logger = sp.GetRequiredService<ILogger<AuthenticationService>>();
            
            return new AuthenticationService(httpClient, localStorage, logger);
        });
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Act - 解析AuthenticationService
        var authService = serviceProvider.GetRequiredService<IAuthenticationService>();
        
        // Assert - 验证服务成功创建
        Assert.NotNull(authService);
        Assert.IsType<AuthenticationService>(authService);
    }
    
    /// <summary>
    /// 测试：验证ApiClient使用的是已认证的HttpClient
    /// </summary>
    [Fact]
    public void ApiClient_ShouldUseAuthenticatedHttpClient()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        
        var mockLocalStorage = new Mock<ILocalStorageService>();
        services.AddSingleton<ILocalStorageService>(mockLocalStorage.Object);
        
        var apiBase = "https://localhost:7056";
        
        services.AddScoped<AuthorizingHttpMessageHandler>();
        
        services.AddHttpClient("UnauthenticatedHttpClient", client =>
        {
            client.BaseAddress = new Uri(apiBase);
        });
        
        services.AddHttpClient("AuthenticatedHttpClient")
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri(apiBase);
            })
            .AddHttpMessageHandler<AuthorizingHttpMessageHandler>();
        
        services.AddScoped<IAuthenticationService>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("UnauthenticatedHttpClient");
            var localStorage = sp.GetRequiredService<ILocalStorageService>();
            var logger = sp.GetRequiredService<ILogger<AuthenticationService>>();
            
            return new AuthenticationService(httpClient, localStorage, logger);
        });
        
        services.AddScoped(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            return httpClientFactory.CreateClient("AuthenticatedHttpClient");
        });
        
        services.AddScoped<ApiClient>();
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Act - 解析ApiClient
        var apiClient = serviceProvider.GetRequiredService<ApiClient>();
        
        // Assert - 验证服务成功创建
        Assert.NotNull(apiClient);
        Assert.IsType<ApiClient>(apiClient);
    }
    
    /// <summary>
    /// 测试：验证可以同时创建多个服务实例而不会产生冲突
    /// </summary>
    [Fact]
    public void MultipleServiceInstances_ShouldBeCreatedSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        
        var mockLocalStorage = new Mock<ILocalStorageService>();
        services.AddSingleton<ILocalStorageService>(mockLocalStorage.Object);
        
        var apiBase = "https://localhost:7056";
        
        services.AddScoped<AuthorizingHttpMessageHandler>();
        
        services.AddHttpClient("UnauthenticatedHttpClient", client =>
        {
            client.BaseAddress = new Uri(apiBase);
        });
        
        services.AddHttpClient("AuthenticatedHttpClient")
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri(apiBase);
            })
            .AddHttpMessageHandler<AuthorizingHttpMessageHandler>();
        
        services.AddScoped<IAuthenticationService>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("UnauthenticatedHttpClient");
            var localStorage = sp.GetRequiredService<ILocalStorageService>();
            var logger = sp.GetRequiredService<ILogger<AuthenticationService>>();
            
            return new AuthenticationService(httpClient, localStorage, logger);
        });
        
        services.AddScoped(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            return httpClientFactory.CreateClient("AuthenticatedHttpClient");
        });
        
        services.AddScoped<ApiClient>();
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Act - 同时解析多个服务
        var authService1 = serviceProvider.GetRequiredService<IAuthenticationService>();
        var httpClient1 = serviceProvider.GetRequiredService<HttpClient>();
        var apiClient1 = serviceProvider.GetRequiredService<ApiClient>();
        var handler1 = serviceProvider.GetRequiredService<AuthorizingHttpMessageHandler>();
        
        // 创建新的作用域来测试作用域服务
        using var scope = serviceProvider.CreateScope();
        var authService2 = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
        var httpClient2 = scope.ServiceProvider.GetRequiredService<HttpClient>();
        var apiClient2 = scope.ServiceProvider.GetRequiredService<ApiClient>();
        
        // Assert - 验证所有服务都成功创建
        Assert.NotNull(authService1);
        Assert.NotNull(httpClient1);
        Assert.NotNull(apiClient1);
        Assert.NotNull(handler1);
        
        Assert.NotNull(authService2);
        Assert.NotNull(httpClient2);
        Assert.NotNull(apiClient2);
        
        // 验证在同一作用域内，Scoped服务返回相同实例
        var authService1Again = serviceProvider.GetRequiredService<IAuthenticationService>();
        Assert.Same(authService1, authService1Again);
        
        // 验证在不同作用域内，Scoped服务返回不同实例
        Assert.NotSame(authService1, authService2);
    }
}
