using BlazorIdle;
using BlazorIdle.Client.Services.SignalR;
using BlazorIdle.Services.Auth;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

// 配置API基础地址，注意要与 Server 实际端口一致
var apiBase = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7056";

// 注册LocalStorage服务
// 用于在浏览器本地存储中保存和读取数据（如JWT Token、用户信息等）
builder.Services.AddBlazoredLocalStorage();

// 注册HTTP消息处理器（用于自动附加JWT Token到请求头）
builder.Services.AddScoped<AuthorizingHttpMessageHandler>();

// 配置未认证的HttpClient（用于认证服务 - 登录、注册等不需要Token的请求）
// 这个HttpClient不使用AuthorizingHttpMessageHandler，避免循环依赖
builder.Services.AddHttpClient("UnauthenticatedHttpClient", client =>
{
    client.BaseAddress = new Uri(apiBase);
});

// 配置已认证的HttpClient（使用拦截器自动附加Token）
// 使用Scoped生命周期确保每个用户会话有独立的HttpClient实例
builder.Services.AddHttpClient("AuthenticatedHttpClient")
    .ConfigureHttpClient(client =>
    {
        client.BaseAddress = new Uri(apiBase);
    })
    .AddHttpMessageHandler<AuthorizingHttpMessageHandler>();

// 注册认证相关服务
// 认证服务使用未认证的HttpClient避免循环依赖
builder.Services.AddScoped<IAuthenticationService>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient("UnauthenticatedHttpClient");
    var localStorage = sp.GetRequiredService<ILocalStorageService>();
    var logger = sp.GetRequiredService<ILogger<AuthenticationService>>();

    return new AuthenticationService(httpClient, localStorage, logger);
});

// 配置默认HttpClient（使用已认证的HttpClient）
// 保持向后兼容性，其他服务可以继续直接注入HttpClient
builder.Services.AddScoped(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    return httpClientFactory.CreateClient("AuthenticatedHttpClient");
});

// 保留原有的ApiClient注册（使用已认证的HttpClient配置）
builder.Services.AddScoped<BlazorIdle.Client.Services.ApiClient>();

// 配置SignalR客户端服务
// 从配置文件加载SignalR客户端选项
var signalROptions = new SignalRClientOptions();
builder.Configuration.GetSection(SignalRClientOptions.SectionName).Bind(signalROptions);
signalROptions.Validate(); // 验证配置有效性

// 注册SignalR客户端选项为单例
builder.Services.AddSingleton(signalROptions);

// 在 Blazor WebAssembly 中，Scoped 等同于应用级单例（每个浏览器标签页一个实例）
// 改为 Scoped 可避免 “Singleton 依赖 Scoped” 的生命周期冲突
builder.Services.AddScoped<SignalRConnectionManager>();

await builder.Build().RunAsync();