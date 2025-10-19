# SignalR Connection Fix

## 问题描述 (Problem Description)

前端 SignalR 连接失败，无法建立与服务器的实时通信。

Frontend SignalR connection was failing, unable to establish real-time communication with the server.

## 根本原因 (Root Cause)

发现了两个导致 SignalR 连接失败的关键问题：

Two critical issues were identified that prevented SignalR from working:

### 1. JWT 认证配置问题
SignalR 在使用 WebSocket 连接时，会通过查询字符串（query string）传递 JWT token，而不是通过 HTTP Header。默认的 JWT Bearer 认证配置不处理查询字符串中的 token，导致认证失败。

**JWT Authentication Issue**: SignalR sends JWT tokens via query string for WebSocket connections, but the default JWT Bearer configuration doesn't handle tokens from query strings, causing authentication failures.

### 2. CORS 配置缺失客户端 URL
服务器的 CORS 策略中缺少客户端实际运行的 URL（`https://localhost:7056`），导致跨域请求被阻止。

**CORS Configuration Issue**: The server's CORS policy was missing the client's actual URL (`https://localhost:7056`), causing cross-origin requests to be blocked.

## 修复方案 (Solution)

### 1. 添加 SignalR JWT 认证支持

在 `BlazorIdle.Server/Program.cs` 的 JWT Bearer 配置中添加了 `OnMessageReceived` 事件处理器：

Added `OnMessageReceived` event handler in JWT Bearer configuration:

```csharp
options.Events = new JwtBearerEvents
{
    OnMessageReceived = context =>
    {
        var accessToken = context.Request.Query["access_token"];
        var path = context.HttpContext.Request.Path;
        
        // If the request is for SignalR Hub
        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
        {
            context.Token = accessToken;
        }
        
        return Task.CompletedTask;
    }
};
```

**工作原理 (How it works)**:
- 检查请求查询字符串中是否包含 `access_token` 参数
- 验证请求路径是否以 `/hubs` 开头（SignalR Hub 端点）
- 如果满足条件，将 token 从查询字符串提取并设置到认证上下文中

### 2. 更新 CORS 配置

在 CORS 策略中添加了客户端 URL 并启用凭据支持：

Updated CORS policy to include client URLs and enable credentials:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        policy
            .WithOrigins(
                "https://localhost:5001",
                "http://localhost:5001",
                "http://localhost:5000",
                "https://localhost:7056", // 新增：客户端 HTTPS 端口
                "http://localhost:5056")  // 新增：客户端 HTTP 端口
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // 新增：SignalR 需要凭据支持
    });
});
```

**关键变更 (Key changes)**:
- 添加 `https://localhost:7056` - 客户端实际 HTTPS 端口
- 添加 `http://localhost:5056` - 客户端 HTTP 端口
- 添加 `.AllowCredentials()` - SignalR WebSocket 连接需要凭据支持

## 验证步骤 (Verification Steps)

1. **启动服务器**:
   ```bash
   cd BlazorIdle.Server
   dotnet run
   ```

2. **启动客户端**:
   ```bash
   cd BlazorIdle
   dotnet run
   ```

3. **检查浏览器控制台**:
   - 应该能看到 SignalR 连接成功的日志
   - 不应该有 CORS 错误
   - 不应该有 401 Unauthorized 错误

4. **测试实时通信**:
   - 启动战斗
   - 应该能够接收到实时的战斗事件通知
   - 战斗状态变化应该立即反映在 UI 上

## 技术细节 (Technical Details)

### SignalR Token 传递机制
SignalR 使用 WebSocket 时无法通过 HTTP Header 传递 token，因此使用查询字符串：
```
wss://localhost:7056/hubs/battle?access_token=eyJhbGc...
```

### CORS 与 SignalR
SignalR 需要：
- 正确的 Origin 配置
- `.AllowCredentials()` 支持
- 客户端和服务器端口必须在 CORS 策略中

## 相关文件 (Related Files)

- `BlazorIdle.Server/Program.cs` - 服务器端 JWT 和 CORS 配置
- `BlazorIdle/Services/BattleSignalRService.cs` - 客户端 SignalR 服务
- `BlazorIdle.Server/Hubs/BattleNotificationHub.cs` - SignalR Hub 实现

## 参考资源 (References)

- [ASP.NET Core SignalR authentication and authorization](https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz)
- [Enable Cross-Origin Requests (CORS) in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/cors)
- [SignalR JavaScript client](https://learn.microsoft.com/en-us/aspnet/core/signalr/javascript-client)
