using BlazorIdle;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

// ����������ַ��ע������ Server ʵ�ʶ˿�һ�£�
var apiBase = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7056";

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBase) });
builder.Services.AddScoped<BlazorIdle.Client.Services.AuthService>();
builder.Services.AddScoped<BlazorIdle.Client.Services.ApiClient>();
await builder.Build().RunAsync();