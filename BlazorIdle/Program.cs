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

// 注册认证相关服务
// 认证服务提供登录、注册、登出等功能
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();

// 注册HTTP消息处理器（用于自动附加JWT Token到请求头）
builder.Services.AddScoped<AuthorizingHttpMessageHandler>();

// 配置HttpClient（使用拦截器自动附加Token）
// 使用Scoped生命周期确保每个用户会话有独立的HttpClient实例
builder.Services.AddScoped(sp =>
{
    // 获取消息处理器
    var handler = sp.GetRequiredService<AuthorizingHttpMessageHandler>();
    handler.InnerHandler = new HttpClientHandler();

    // 创建HttpClient并设置基础地址
    var httpClient = new HttpClient(handler)
    {
        BaseAddress = new Uri(apiBase)
    };

    return httpClient;
});

// 保留原有的ApiClient注册（但使用新的HttpClient配置）
builder.Services.AddScoped<BlazorIdle.Client.Services.ApiClient>();

// 配置SignalR客户端服务
// 从配置文件加载SignalR客户端选项
var signalROptions = new SignalRClientOptions();
builder.Configuration.GetSection(SignalRClientOptions.SectionName).Bind(signalROptions);
signalROptions.Validate(); // 验证配置有效性

// 注册SignalR客户端选项为单例
builder.Services.AddSingleton(signalROptions);

// 注册SignalRConnectionManager为单例服务
// 使用单例确保整个应用程序共享同一个SignalR连接
// 这样用户在不同页面切换时可以保持连接状态
builder.Services.AddSingleton<SignalRConnectionManager>();

await builder.Build().RunAsync();
