using BlazorIdle;
using BlazorIdle.Client.Services.SignalR;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

// 配置API基础地址，注意要与 Server 实际端口一致
var apiBase = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7056";

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBase) });
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
