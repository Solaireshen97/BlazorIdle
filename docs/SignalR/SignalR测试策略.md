# SignalR 测试策略文档

## 问题分析

当前项目的 SignalR 实现需要用户身份验证（通过 Claims 中的 `NameIdentifier`），但项目目前还没有实现用户认证系统。这个功能计划在添加缓存和数据库支持之后实现。

## 当前状态

### SignalR 身份验证要求

1. **GameHub.GetUserId()** 方法从 `Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value` 获取用户ID
2. **OnConnectedAsync()** 会检查用户ID，如果为空则：
   - 发送错误消息："Unauthorized"
   - 中止连接
3. 所有 Hub 方法（订阅、心跳等）都需要有效的用户ID

### 测试策略

#### 1. 单元测试（已实现）

**优点**：
- ✅ 无需实际的用户认证系统
- ✅ 可以完全控制测试环境
- ✅ 快速执行
- ✅ 可以测试所有边缘情况

**实现方式**：
- 使用 **Moq** 库 mock `HubCallerContext`
- Mock `ClaimsPrincipal` 提供假的用户身份
- Mock `IConnectionManager`、`IHubCallerClients` 等依赖

**已创建的测试**：
1. `ConnectionManagerTests.cs` - 测试连接管理器的所有功能
2. `GameHubTests.cs` - 测试 GameHub 的所有场景

#### 2. 集成测试（可选，需要认证配置）

**集成测试的挑战**：
- ❌ 需要实际的认证配置
- ❌ 需要启动完整的 ASP.NET Core 应用
- ❌ 更复杂的设置

**可能的解决方案**：

##### 方案 A：开发环境认证绕过（推荐用于测试）

在 `Program.cs` 中添加开发环境专用的测试认证：

```csharp
#if DEBUG
// 开发环境测试认证 - 仅用于 SignalR 功能测试
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddAuthentication("TestAuth")
        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestAuth", options => { });
}
#endif
```

创建 `TestAuthHandler.cs`：

```csharp
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // 从查询字符串或请求头获取测试用户ID
        var userId = Request.Query["userId"].FirstOrDefault() ?? "test-user";
        
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestAuth");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
```

**使用方式**：
```csharp
// 客户端连接时传入 userId
var connection = new HubConnectionBuilder()
    .WithUrl("https://localhost:7000/hubs/game?userId=test-user-123")
    .Build();
```

##### 方案 B：使用假的 JWT Token

配置 JWT 认证但使用简单的测试 token：

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        if (builder.Environment.IsDevelopment())
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false,
                ValidateIssuerSigningKey = false,
                SignatureValidator = (token, parameters) => new JwtSecurityToken(token)
            };
        }
    });
```

##### 方案 C：修改 GameHub 支持匿名测试（不推荐生产环境）

添加编译时条件：

```csharp
private string? GetUserId()
{
#if DEBUG
    // 开发环境：如果没有认证，使用连接ID作为临时用户ID
    var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId))
    {
        return $"test-{Context.ConnectionId}";
    }
    return userId;
#else
    // 生产环境：必须有有效的用户认证
    return Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
#endif
}
```

## 当前可以测试的内容

### ✅ 已经可以通过单元测试验证的功能

1. **ConnectionManager 功能**
   - ✅ 连接注册和注销
   - ✅ 多连接管理
   - ✅ 订阅管理
   - ✅ 心跳更新
   - ✅ 空闲会话检测
   - ✅ 线程安全性

2. **GameHub 功能**
   - ✅ 已认证用户的连接建立
   - ✅ 未认证用户的连接拒绝
   - ✅ 订阅/取消订阅 (Battle/Party)
   - ✅ 心跳处理
   - ✅ 状态同步请求
   - ✅ 错误处理

### ⏳ 需要集成测试的功能（需要认证配置）

1. **实际 SignalR 连接**
   - 客户端连接到真实服务器
   - WebSocket 传输
   - 消息传递

2. **Group 功能**
   - 多客户端加入同一个 Group
   - Group 消息广播

3. **断线重连**
   - 自动重连
   - 状态恢复

## 建议的实施步骤

### 阶段 1：单元测试（已完成）✅

- [x] 添加 Moq 和 SignalR Client 包
- [x] 创建 ConnectionManagerTests
- [x] 创建 GameHubTests
- [x] 验证所有测试通过

### 阶段 2：开发环境测试支持（可选）

如果需要集成测试，可以：

1. 实现方案 A (TestAuthHandler)
2. 创建简单的 SignalR 客户端测试
3. 验证实际连接和消息传递

### 阶段 3：完整用户认证系统（后续）

在添加缓存和数据库后：

1. 实现用户注册/登录
2. 实现 JWT Token 认证
3. 更新 SignalR 配置使用真实认证
4. 移除测试认证代码

## 测试运行

### 运行所有测试

```bash
cd /home/runner/work/BlazorIdle/BlazorIdle
dotnet test
```

### 运行 SignalR 测试

```bash
cd /home/runner/work/BlazorIdle/BlazorIdle
dotnet test --filter "FullyQualifiedName~SignalR"
```

### 查看测试覆盖率

```bash
dotnet test --collect:"XPlat Code Coverage"
```

## 结论

**回答原始问题：现在如果没有用户系统的话，可以测试 SignalR 的功能吗？**

**答案：是的，可以！**

1. **单元测试层面**（当前已实现）：
   - ✅ 完全可以测试所有 SignalR 功能
   - ✅ 使用 Mock 模拟用户身份
   - ✅ 测试覆盖率高，执行速度快

2. **集成测试层面**（需要额外配置）：
   - ⚠️ 需要添加开发环境测试认证
   - ⚠️ 可以选择上述三个方案之一
   - ⚠️ 建议等用户系统完成后再做完整集成测试

3. **推荐策略**：
   - 当前阶段：专注于单元测试（已完成）
   - 用户系统前：可选择添加简单的测试认证
   - 用户系统后：实施完整的集成测试

## 相关文件

- 测试文件：
  - `/tests/BlazorIdle.Tests/SignalR/ConnectionManagerTests.cs`
  - `/tests/BlazorIdle.Tests/SignalR/GameHubTests.cs`

- 实现文件：
  - `/BlazorIdle.Server/Infrastructure/SignalR/Services/ConnectionManager.cs`
  - `/BlazorIdle.Server/Infrastructure/SignalR/Hubs/GameHub.cs`
  - `/BlazorIdle.Server/Infrastructure/SignalR/IConnectionManager.cs`
  - `/BlazorIdle.Server/Infrastructure/SignalR/Models/UserSession.cs`

- 文档：
  - `/docs/SignalR/Phase1-Step2-验证报告.md`
