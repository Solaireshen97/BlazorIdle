# JWT 用户系统设计方案

**文档版本**: 1.0  
**生成日期**: 2025年10月23日  
**状态**: 设计方案  
**目标**: 为BlazorIdle项目设计轻量级JWT用户认证系统，支持SignalR连接测试

---

## 📚 目录

1. [背景与需求](#背景与需求)
2. [设计目标](#设计目标)
3. [系统架构](#系统架构)
4. [技术选型](#技术选型)
5. [核心组件设计](#核心组件设计)
6. [API接口设计](#api接口设计)
7. [数据模型](#数据模型)
8. [安全性设计](#安全性设计)
9. [客户端集成](#客户端集成)
10. [SignalR集成](#signalr集成)
11. [配置管理](#配置管理)
12. [测试计划](#测试计划)
13. [实施路线图](#实施路线图)

---

## 背景与需求

### 当前状况

- ✅ SignalR基础架构已完成（阶段一）
  - GameHub统一Hub实现
  - ConnectionManager连接管理
  - SignalRDispatcher消息分发
  - 客户端SignalRConnectionManager
- ⚠️ GameHub使用`ClaimTypes.NameIdentifier`验证用户，但JWT认证系统尚未实现
- 🚫 无法进行SignalR连接测试，因为缺少有效的用户身份

### 核心需求

1. **轻量级实现**: 不需要完整的用户管理系统，只需支持SignalR测试
2. **内存存储**: 无需数据库持久化，服务重启后数据清空
3. **测试账户**: 预设测试账户，方便开发调试
4. **JWT标准**: 使用标准JWT实现，便于后续扩展
5. **无缝集成**: 与现有SignalR架构完全兼容
6. **最小改动**: 最小化对现有代码的修改

### 功能范围

**包含功能**：
- ✅ 用户登录/注册
- ✅ JWT令牌生成和验证
- ✅ 用户信息查询
- ✅ SignalR连接认证
- ✅ 令牌刷新机制

**不包含功能**：
- ❌ 数据持久化（数据库）
- ❌ 密码重置/找回
- ❌ 用户角色和权限管理
- ❌ 第三方登录（OAuth）
- ❌ 多因素认证（MFA）

---

## 设计目标

### 核心价值

1. **简单易用**: 最小化配置，开箱即用
2. **标准合规**: 遵循JWT和ASP.NET Core认证最佳实践
3. **可测试性**: 预设测试账户，快速验证功能
4. **可扩展性**: 后续可轻松替换为数据库持久化
5. **安全性**: 基本的安全措施（密码哈希、HTTPS、令牌过期）

### 非功能需求

- **性能**: 登录响应时间 < 100ms
- **可靠性**: 内存存储线程安全
- **可维护性**: 代码结构清晰，注释完整
- **兼容性**: 与现有SignalR架构无缝集成

---

## 系统架构

### 整体架构图

```
┌─────────────────────────────────────────────────────────────┐
│  Blazor WebAssembly Client                                  │
│                                                              │
│  ┌────────────────┐        ┌──────────────────┐            │
│  │  Login Page    │───────>│  AuthService     │            │
│  │  (UI)          │        │  (Token管理)     │            │
│  └────────────────┘        └──────────────────┘            │
│                                      │                       │
│                                      ▼                       │
│                            LocalStorage (JWT Token)         │
│                                      │                       │
│                                      ▼                       │
│              SignalRConnectionManager (附加Token)           │
│                                                              │
└────────────────────┬────────────────────────────────────────┘
                     │ HTTPS (Authorization: Bearer <token>)
┌────────────────────▼────────────────────────────────────────┐
│  ASP.NET Core Server                                        │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  Authentication Middleware                            │  │
│  │  - JWT Bearer Token验证                               │  │
│  │  - Claims提取和验证                                   │  │
│  └──────────────────────────────────────────────────────┘  │
│                          │                                   │
│         ┌────────────────┼────────────────┐                 │
│         ▼                ▼                ▼                 │
│  ┌───────────┐   ┌─────────────┐   ┌──────────┐           │
│  │ GameHub   │   │ AuthAPI     │   │ 其他API  │           │
│  │ (SignalR) │   │ (登录/注册) │   │          │           │
│  └───────────┘   └─────────────┘   └──────────┘           │
│         │                │                                   │
│         │                ▼                                   │
│         │        ┌─────────────────┐                        │
│         │        │  AuthService    │                        │
│         │        │  (Token生成)    │                        │
│         │        └─────────────────┘                        │
│         │                │                                   │
│         └────────────────┼───────────────┐                  │
│                          ▼               │                  │
│                  ┌──────────────┐        │                  │
│                  │ UserStore    │        │                  │
│                  │ (内存存储)   │        │                  │
│                  └──────────────┘        │                  │
│                                           ▼                  │
│                                   Context.User.Claims       │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

### 数据流图

**登录流程**：
```
Client                  AuthAPI              AuthService         UserStore
  │                       │                      │                   │
  │─── POST /api/auth/login ──>│                │                   │
  │     {username, password}   │                │                   │
  │                       │─── ValidateUser ───>│                   │
  │                       │                      │─── FindUser ────>│
  │                       │                      │<─── User ────────│
  │                       │<─── IsValid ─────────│                   │
  │                       │─── GenerateToken ───>│                   │
  │                       │<─── JWT Token ───────│                   │
  │<─── 200 OK ──────────│                      │                   │
  │     {token, userId}   │                      │                   │
  │                       │                      │                   │
  │─── 保存Token到LocalStorage                  │                   │
  │                       │                      │                   │
```

**SignalR连接流程**：
```
Client                  GameHub              AuthMiddleware      UserStore
  │                       │                      │                   │
  │─── Connect (/hubs/game) ───>│               │                   │
  │     Authorization: Bearer <token>           │                   │
  │                       │                      │                   │
  │                       │<─── Validate Token ─│                   │
  │                       │                      │─── GetUser ─────>│
  │                       │                      │<─── User ────────│
  │                       │─── Set Context.User ─│                   │
  │                       │                      │                   │
  │                       │─── OnConnectedAsync()│                   │
  │                       │     GetUserId()      │                   │
  │                       │     (from Claims)    │                   │
  │<─── Connected ───────│                      │                   │
  │                       │                      │                   │
```

---

## 技术选型

### 服务端技术栈

| 组件 | 技术选型 | 说明 |
|------|---------|------|
| **JWT库** | System.IdentityModel.Tokens.Jwt | Microsoft官方JWT实现 |
| **认证框架** | ASP.NET Core Authentication | 内置认证中间件 |
| **密码哈希** | BCrypt.Net-Next | 业界标准的密码哈希算法 |
| **用户存储** | ConcurrentDictionary<string, User> | 线程安全的内存存储 |
| **配置管理** | IConfiguration (appsettings.json) | 标准配置系统 |

### 客户端技术栈

| 组件 | 技术选型 | 说明 |
|------|---------|------|
| **Token存储** | Blazored.LocalStorage | Blazor本地存储库 |
| **HTTP客户端** | HttpClient | .NET标准HTTP客户端 |
| **SignalR客户端** | Microsoft.AspNetCore.SignalR.Client | 已有依赖 |
| **状态管理** | 服务注入 | Blazor依赖注入 |

### NuGet包依赖

**服务端新增**：
```xml
<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.2.1" />
<PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="9.0.9" />
```

**客户端新增**：
```xml
<PackageReference Include="Blazored.LocalStorage" Version="4.5.0" />
```

---

## 核心组件设计

### 1. UserStore（用户存储）

**职责**：
- 管理用户数据（内存存储）
- 提供用户查询、创建、验证功能
- 初始化测试账户

**接口设计**：
```csharp
public interface IUserStore
{
    Task<User?> GetUserByIdAsync(string userId);
    Task<User?> GetUserByUsernameAsync(string username);
    Task<User> CreateUserAsync(string username, string password);
    Task<bool> ValidatePasswordAsync(string username, string password);
    Task<IEnumerable<User>> GetAllUsersAsync();
}
```

**实现要点**：
```csharp
public class InMemoryUserStore : IUserStore
{
    private readonly ConcurrentDictionary<string, User> _users = new();
    private readonly ConcurrentDictionary<string, string> _usernameToId = new();
    
    public InMemoryUserStore()
    {
        // 初始化测试账户
        InitializeTestAccounts();
    }
    
    private void InitializeTestAccounts()
    {
        // 创建3个测试账户
        CreateTestUser("test1", "password123");
        CreateTestUser("test2", "password123");
        CreateTestUser("admin", "admin123");
    }
    
    // ... 其他实现
}
```

### 2. AuthService（认证服务）

**职责**：
- JWT令牌生成
- 令牌验证
- 用户认证逻辑

**接口设计**：
```csharp
public interface IAuthService
{
    Task<AuthResult> LoginAsync(string username, string password);
    Task<AuthResult> RegisterAsync(string username, string password);
    Task<AuthResult> RefreshTokenAsync(string refreshToken);
    string GenerateJwtToken(User user);
    ClaimsPrincipal? ValidateToken(string token);
}
```

**JWT配置**：
```csharp
public class JwtOptions
{
    public const string SectionName = "Jwt";
    
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "BlazorIdleServer";
    public string Audience { get; set; } = "BlazorIdleClient";
    public int ExpirationMinutes { get; set; } = 60;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
```

**令牌生成示例**：
```csharp
public string GenerateJwtToken(User user)
{
    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id),
        new Claim(ClaimTypes.Name, user.Username),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    };
    
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    
    var token = new JwtSecurityToken(
        issuer: _jwtOptions.Issuer,
        audience: _jwtOptions.Audience,
        claims: claims,
        expires: DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes),
        signingCredentials: creds
    );
    
    return new JwtSecurityTokenHandler().WriteToken(token);
}
```

### 3. AuthController（认证API控制器）

**职责**：
- 提供HTTP API端点
- 处理登录、注册、刷新令牌请求
- 返回标准化响应

**端点设计**：
```csharp
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request);
    
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request);
    
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody] RefreshTokenRequest request);
    
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserInfo>> GetCurrentUser();
}
```

### 4. AuthenticationService（客户端认证服务）

**职责**：
- 管理客户端Token
- 提供登录、注销功能
- 自动附加Token到HTTP请求
- 管理认证状态

**接口设计**：
```csharp
public interface IAuthenticationService
{
    Task<AuthResult> LoginAsync(string username, string password);
    Task LogoutAsync();
    Task<bool> IsAuthenticatedAsync();
    Task<string?> GetTokenAsync();
    Task<UserInfo?> GetCurrentUserAsync();
}
```

---

## API接口设计

### 1. 登录接口

**端点**: `POST /api/auth/login`

**请求**:
```json
{
  "username": "test1",
  "password": "password123"
}
```

**成功响应** (200 OK):
```json
{
  "success": true,
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "refresh_token_here",
  "expiresAt": "2025-10-23T14:30:00Z",
  "user": {
    "id": "user_123",
    "username": "test1",
    "createdAt": "2025-10-23T06:00:00Z"
  }
}
```

**失败响应** (401 Unauthorized):
```json
{
  "success": false,
  "message": "用户名或密码错误"
}
```

### 2. 注册接口

**端点**: `POST /api/auth/register`

**请求**:
```json
{
  "username": "newuser",
  "password": "password123"
}
```

**成功响应** (200 OK):
```json
{
  "success": true,
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "refresh_token_here",
  "expiresAt": "2025-10-23T14:30:00Z",
  "user": {
    "id": "user_124",
    "username": "newuser",
    "createdAt": "2025-10-23T13:30:00Z"
  }
}
```

**失败响应** (400 Bad Request):
```json
{
  "success": false,
  "message": "用户名已存在"
}
```

### 3. 获取当前用户

**端点**: `GET /api/auth/me`

**请求头**:
```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**成功响应** (200 OK):
```json
{
  "id": "user_123",
  "username": "test1",
  "createdAt": "2025-10-23T06:00:00Z"
}
```

**失败响应** (401 Unauthorized):
```json
{
  "message": "未授权访问"
}
```

### 4. 刷新令牌

**端点**: `POST /api/auth/refresh`

**请求**:
```json
{
  "refreshToken": "refresh_token_here"
}
```

**成功响应** (200 OK):
```json
{
  "success": true,
  "token": "new_jwt_token_here",
  "refreshToken": "new_refresh_token_here",
  "expiresAt": "2025-10-23T15:30:00Z"
}
```

---

## 数据模型

### User（用户模型）

```csharp
public class User
{
    /// <summary>
    /// 用户唯一标识
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// 用户名（登录用）
    /// </summary>
    public string Username { get; set; } = string.Empty;
    
    /// <summary>
    /// 密码哈希（BCrypt）
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 最后登录时间
    /// </summary>
    public DateTime? LastLoginAt { get; set; }
    
    /// <summary>
    /// 当前刷新令牌
    /// </summary>
    public string? RefreshToken { get; set; }
    
    /// <summary>
    /// 刷新令牌过期时间
    /// </summary>
    public DateTime? RefreshTokenExpiresAt { get; set; }
}
```

### AuthResult（认证结果）

```csharp
public class AuthResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Token { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public UserInfo? User { get; set; }
}
```

### UserInfo（用户信息DTO）

```csharp
public class UserInfo
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
```

### LoginRequest/RegisterRequest

```csharp
public class LoginRequest
{
    [Required(ErrorMessage = "用户名不能为空")]
    public string Username { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "密码不能为空")]
    [MinLength(6, ErrorMessage = "密码至少6位")]
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    [Required(ErrorMessage = "用户名不能为空")]
    [MinLength(3, ErrorMessage = "用户名至少3位")]
    [MaxLength(20, ErrorMessage = "用户名最多20位")]
    public string Username { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "密码不能为空")]
    [MinLength(6, ErrorMessage = "密码至少6位")]
    public string Password { get; set; } = string.Empty;
}
```

---

## 安全性设计

### 1. 密码安全

**密码哈希**：
- 使用BCrypt算法（工作因子12）
- 每个密码有唯一的salt
- 不存储明文密码

```csharp
// 密码哈希
var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password, 12);

// 密码验证
var isValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
```

### 2. JWT令牌安全

**令牌配置**：
- 使用HMAC-SHA256签名算法
- 密钥长度至少256位
- 令牌有效期：60分钟
- 刷新令牌有效期：7天

**安全建议**：
```csharp
// appsettings.json
{
  "Jwt": {
    "SecretKey": "这里应该使用至少32字符的强密钥_建议使用环境变量",
    "Issuer": "BlazorIdleServer",
    "Audience": "BlazorIdleClient",
    "ExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 7
  }
}
```

**生产环境建议**：
- SecretKey使用环境变量或密钥管理服务
- 启用HTTPS（已有）
- 考虑令牌黑名单（可选）

### 3. CORS安全

```csharp
// 限制允许的来源
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        policy
            .WithOrigins(
                "https://localhost:5001",
                "https://yourproduction.domain")  // 生产环境域名
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
```

### 4. 输入验证

- 使用DataAnnotations验证用户输入
- 用户名长度限制：3-20字符
- 密码长度限制：至少6字符
- 防止SQL注入（虽然使用内存存储，但养成习惯）

### 5. 错误处理

```csharp
// 不泄露敏感信息
if (user == null || !ValidatePassword(password))
{
    // 使用统一错误消息，避免暴露用户是否存在
    return new AuthResult 
    { 
        Success = false, 
        Message = "用户名或密码错误" 
    };
}
```

---

## 客户端集成

### 1. 登录页面

**位置**: `BlazorIdle/Pages/Login.razor`

**功能**：
- 用户名/密码输入表单
- 登录按钮
- 错误消息显示
- 登录成功后跳转到主页

**示例代码**：
```razor
@page "/login"
@inject IAuthenticationService AuthService
@inject NavigationManager Navigation

<h3>用户登录</h3>

@if (!string.IsNullOrEmpty(errorMessage))
{
    <div class="alert alert-danger">@errorMessage</div>
}

<EditForm Model="loginModel" OnValidSubmit="HandleLogin">
    <DataAnnotationsValidator />
    <ValidationSummary />
    
    <div class="form-group">
        <label>用户名</label>
        <InputText @bind-Value="loginModel.Username" class="form-control" />
    </div>
    
    <div class="form-group">
        <label>密码</label>
        <InputText @bind-Value="loginModel.Password" type="password" class="form-control" />
    </div>
    
    <button type="submit" class="btn btn-primary">登录</button>
</EditForm>

<p class="mt-3">
    测试账户：test1 / password123
</p>

@code {
    private LoginRequest loginModel = new();
    private string? errorMessage;
    
    private async Task HandleLogin()
    {
        errorMessage = null;
        var result = await AuthService.LoginAsync(loginModel.Username, loginModel.Password);
        
        if (result.Success)
        {
            Navigation.NavigateTo("/");
        }
        else
        {
            errorMessage = result.Message;
        }
    }
}
```

### 2. AuthenticationService实现

```csharp
public class AuthenticationService : IAuthenticationService
{
    private readonly HttpClient _httpClient;
    private readonly ILocalStorageService _localStorage;
    private const string TOKEN_KEY = "authToken";
    
    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        var request = new LoginRequest { Username = username, Password = password };
        var response = await _httpClient.PostAsJsonAsync("/api/auth/login", request);
        
        if (response.IsSuccessStatusCode)
        {
            var authResult = await response.Content.ReadFromJsonAsync<AuthResult>();
            if (authResult?.Success == true && authResult.Token != null)
            {
                await _localStorage.SetItemAsync(TOKEN_KEY, authResult.Token);
                return authResult;
            }
        }
        
        return new AuthResult { Success = false, Message = "登录失败" };
    }
    
    public async Task<string?> GetTokenAsync()
    {
        return await _localStorage.GetItemAsync<string>(TOKEN_KEY);
    }
    
    // ... 其他方法
}
```

### 3. HTTP拦截器（附加Token）

```csharp
public class AuthorizingHttpMessageHandler : DelegatingHandler
{
    private readonly IAuthenticationService _authService;
    
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        var token = await _authService.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = 
                new AuthenticationHeaderValue("Bearer", token);
        }
        
        return await base.SendAsync(request, cancellationToken);
    }
}
```

**注册拦截器**：
```csharp
// Program.cs
builder.Services.AddScoped<AuthorizingHttpMessageHandler>();
builder.Services.AddScoped(sp => 
{
    var handler = sp.GetRequiredService<AuthorizingHttpMessageHandler>();
    handler.InnerHandler = new HttpClientHandler();
    
    var httpClient = new HttpClient(handler)
    {
        BaseAddress = new Uri(apiBase)
    };
    return httpClient;
});
```

---

## SignalR集成

### 1. 修改SignalR连接配置

**修改**: `BlazorIdle/Services/SignalR/SignalRConnectionManager.cs`

```csharp
public async Task InitializeAsync()
{
    if (_connection != null)
    {
        _logger.LogWarning("SignalR连接已初始化");
        return;
    }

    try
    {
        // 获取JWT Token
        var token = await _authService.GetTokenAsync();
        
        _connection = new HubConnectionBuilder()
            .WithUrl(_options.HubUrl, options =>
            {
                // 附加JWT Token
                if (!string.IsNullOrEmpty(token))
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                }
                
                // ... 其他配置
            })
            .WithAutomaticReconnect(GetReconnectDelays())
            .Build();
            
        // ... 其他初始化代码
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "初始化SignalR连接失败");
        throw;
    }
}
```

### 2. 服务端JWT验证配置

**修改**: `BlazorIdle.Server/Program.cs`

```csharp
// 配置JWT认证
var jwtOptions = new JwtOptions();
builder.Configuration.GetSection(JwtOptions.SectionName).Bind(jwtOptions);
jwtOptions.Validate();
builder.Services.AddSingleton(jwtOptions);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.SecretKey))
        };
        
        // SignalR需要从查询字符串读取Token
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                
                if (!string.IsNullOrEmpty(accessToken) && 
                    path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ... 其他配置

var app = builder.Build();

// 启用认证和授权中间件（必须在MapControllers和MapHub之前）
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<GameHub>("/hubs/game");
```

### 3. GameHub验证流程

GameHub已经包含用户验证逻辑，无需修改：

```csharp
public override async Task OnConnectedAsync()
{
    var userId = GetUserId();  // 从JWT Claims提取
    
    if (string.IsNullOrEmpty(userId))
    {
        _logger.LogWarning("未授权的连接尝试：{ConnectionId}", Context.ConnectionId);
        await Clients.Caller.SendAsync("Error", "Unauthorized");
        Context.Abort();
        return;
    }
    
    // 继续连接逻辑...
}
```

---

## 配置管理

### 服务端配置

**文件**: `BlazorIdle.Server/appsettings.json`

```json
{
  "Jwt": {
    "SecretKey": "这里应该使用至少32字符的强密钥_请在生产环境使用环境变量",
    "Issuer": "BlazorIdleServer",
    "Audience": "BlazorIdleClient",
    "ExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 7
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "BlazorIdle.Server.Auth": "Debug"
    }
  }
}
```

**开发环境**: `BlazorIdle.Server/appsettings.Development.json`

```json
{
  "Jwt": {
    "ExpirationMinutes": 1440
  },
  "Logging": {
    "LogLevel": {
      "BlazorIdle.Server.Auth": "Debug"
    }
  }
}
```

### 客户端配置

**文件**: `BlazorIdle/wwwroot/appsettings.json`

```json
{
  "ApiBaseUrl": "https://localhost:7056",
  "Auth": {
    "LoginPath": "/login",
    "LogoutRedirectPath": "/",
    "TokenStorageKey": "authToken"
  }
}
```

---

## 测试计划

### 1. 单元测试

**测试覆盖**：
- UserStore基本操作
- AuthService令牌生成和验证
- 密码哈希和验证
- 配置验证

**测试文件**: `tests/BlazorIdle.Tests/Auth/`
- `UserStoreTests.cs` (10个测试用例)
- `AuthServiceTests.cs` (15个测试用例)
- `JwtOptionsTests.cs` (5个测试用例)

### 2. 集成测试

**测试场景**：
- 登录API端到端测试
- 注册API端到端测试
- Token刷新流程测试
- SignalR连接认证测试
- 未授权访问拒绝测试

**测试文件**: `tests/BlazorIdle.Tests/Integration/AuthIntegrationTests.cs`

### 3. 手动测试清单

**服务端测试**：
- [ ] 使用Swagger测试登录API
- [ ] 验证JWT令牌格式和Claims
- [ ] 测试令牌过期后的行为
- [ ] 测试错误的用户名/密码
- [ ] 测试重复注册同一用户名

**客户端测试**：
- [ ] 登录界面UI正常显示
- [ ] 测试账户可以成功登录
- [ ] Token保存到LocalStorage
- [ ] 登录后自动跳转
- [ ] 登录失败显示错误消息

**SignalR集成测试**：
- [ ] 登录后可以连接SignalR
- [ ] 未登录无法连接SignalR
- [ ] Token过期后连接被拒绝
- [ ] GameHub正确识别用户ID
- [ ] 心跳和订阅功能正常

---

## 实施路线图

### 阶段一：服务端基础实施（2-3天）

#### 第1步：安装依赖和配置（0.5天）

**任务清单**：
- [ ] 安装NuGet包（JWT、BCrypt）
- [ ] 创建目录结构
- [ ] 配置appsettings.json

**详细步骤**：

1. **安装NuGet包**：
```bash
cd BlazorIdle.Server
dotnet add package System.IdentityModel.Tokens.Jwt
dotnet add package BCrypt.Net-Next
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
```

2. **创建目录结构**：
```bash
mkdir -p BlazorIdle.Server/Auth/Models
mkdir -p BlazorIdle.Server/Auth/Services
mkdir -p BlazorIdle.Server/Auth/Controllers
```

3. **配置文件**：
修改`appsettings.json`，添加JWT配置节

**验收标准**：
- ✅ 所有包安装成功
- ✅ 项目编译无错误
- ✅ 配置文件格式正确

#### 第2步：实现UserStore（0.5天）

**任务清单**：
- [ ] 创建User模型
- [ ] 创建IUserStore接口
- [ ] 实现InMemoryUserStore
- [ ] 初始化测试账户
- [ ] 注册服务

**验收标准**：
- ✅ 测试账户可以查询
- ✅ 密码哈希正确
- ✅ 线程安全测试通过

#### 第3步：实现AuthService（1天）

**任务清单**：
- [ ] 创建JwtOptions配置类
- [ ] 创建IAuthService接口
- [ ] 实现JWT令牌生成
- [ ] 实现令牌验证
- [ ] 实现刷新令牌逻辑
- [ ] 编写单元测试

**验收标准**：
- ✅ JWT令牌格式正确
- ✅ Claims包含UserId和Username
- ✅ 令牌过期时间正确
- ✅ 单元测试全部通过

#### 第4步：实现AuthController（0.5天）

**任务清单**：
- [ ] 创建DTO类（Request/Response）
- [ ] 实现登录端点
- [ ] 实现注册端点
- [ ] 实现刷新令牌端点
- [ ] 实现获取当前用户端点
- [ ] 添加输入验证

**验收标准**：
- ✅ Swagger文档生成
- ✅ 端点可以访问
- ✅ 返回格式正确

#### 第5步：配置JWT认证中间件（0.5天）

**任务清单**：
- [ ] 配置Authentication服务
- [ ] 配置Authorization服务
- [ ] 配置JWT Bearer选项
- [ ] 添加SignalR Token读取逻辑
- [ ] 启用认证中间件

**验收标准**：
- ✅ JWT认证正常工作
- ✅ 未授权请求返回401
- ✅ 有效Token可以通过认证

---

### 阶段二：客户端实施（1-2天）

#### 第6步：安装客户端依赖（0.5天）

**任务清单**：
- [ ] 安装Blazored.LocalStorage
- [ ] 配置客户端服务注册

**验收标准**：
- ✅ 包安装成功
- ✅ 项目编译无错误

#### 第7步：实现AuthenticationService（1天）

**任务清单**：
- [ ] 创建IAuthenticationService接口
- [ ] 实现AuthenticationService
- [ ] 实现Token存储逻辑
- [ ] 创建HTTP拦截器
- [ ] 注册服务

**验收标准**：
- ✅ Token可以保存和读取
- ✅ HTTP请求自动附加Token
- ✅ 登录状态正确

#### 第8步：创建登录页面（0.5天）

**任务清单**：
- [ ] 创建Login.razor页面
- [ ] 实现登录表单
- [ ] 添加错误处理
- [ ] 添加导航逻辑

**验收标准**：
- ✅ 页面可以访问
- ✅ 表单验证正常
- ✅ 登录成功跳转
- ✅ 错误消息显示

---

### 阶段三：SignalR集成（0.5-1天）

#### 第9步：修改SignalR连接管理（0.5天）

**任务清单**：
- [ ] 修改SignalRConnectionManager
- [ ] 添加Token提供器
- [ ] 测试连接认证

**验收标准**：
- ✅ 登录后可以连接SignalR
- ✅ 未登录连接被拒绝
- ✅ Token传递正确

#### 第10步：端到端测试（0.5天）

**任务清单**：
- [ ] 测试登录流程
- [ ] 测试SignalR连接
- [ ] 测试令牌刷新
- [ ] 验证所有功能

**验收标准**：
- ✅ 所有手动测试通过
- ✅ 日志无错误
- ✅ 性能符合要求

---

### 总时间估算

- **阶段一**（服务端）：2-3天
- **阶段二**（客户端）：1-2天
- **阶段三**（集成测试）：0.5-1天

**总计**：3.5-6天（约1周）

---

## 验收标准

### 功能性验收

- ✅ 用户可以使用测试账户登录
- ✅ 登录后获得有效JWT令牌
- ✅ Token自动附加到HTTP请求
- ✅ Token自动附加到SignalR连接
- ✅ GameHub正确识别用户身份
- ✅ Token过期后自动拒绝
- ✅ 令牌刷新机制正常工作

### 安全性验收

- ✅ 密码使用BCrypt哈希
- ✅ JWT使用HMAC-SHA256签名
- ✅ HTTPS强制启用
- ✅ 未授权请求返回401
- ✅ 错误消息不泄露敏感信息

### 性能验收

- ✅ 登录响应时间 < 100ms
- ✅ Token验证时间 < 10ms
- ✅ SignalR连接建立时间 < 1秒
- ✅ 内存使用稳定（无泄漏）

### 可维护性验收

- ✅ 代码结构清晰
- ✅ 中文注释完整
- ✅ 单元测试覆盖率 > 80%
- ✅ 文档完善

---

## 后续扩展方向

### 短期扩展（1-2周）

1. **数据持久化**：
   - 使用SQLite/PostgreSQL存储用户数据
   - 替换InMemoryUserStore为DatabaseUserStore
   - 迁移测试账户到数据库

2. **密码重置**：
   - 邮件验证码
   - 密码重置流程
   - 安全问题

3. **用户管理界面**：
   - 用户列表页面
   - 用户详情页面
   - 修改密码功能

### 中期扩展（1个月）

1. **角色和权限**：
   - 添加角色系统（Admin、User）
   - 权限控制
   - [Authorize(Roles = "Admin")]

2. **OAuth集成**：
   - Google登录
   - GitHub登录
   - Microsoft登录

3. **审计日志**：
   - 登录历史
   - 操作日志
   - 安全事件

### 长期扩展（2-3个月）

1. **多因素认证（MFA）**：
   - TOTP（Google Authenticator）
   - SMS验证
   - 邮件验证

2. **会话管理**：
   - 查看活跃会话
   - 强制登出所有设备
   - 异常登录检测

3. **API密钥**：
   - 为第三方客户端生成API密钥
   - 密钥管理界面
   - 细粒度权限控制

---

## 故障排查

### 常见问题

#### 问题1: SignalR连接失败 401 Unauthorized

**症状**: 客户端无法连接到GameHub，返回401错误

**排查步骤**:
1. 检查客户端是否已登录
2. 检查Token是否正确附加到SignalR连接
3. 检查服务端JWT配置是否正确
4. 查看服务端日志

**解决方案**:
```csharp
// 确保SignalR连接附加Token
.WithUrl(_options.HubUrl, options =>
{
    options.AccessTokenProvider = async () => 
    {
        var token = await _authService.GetTokenAsync();
        return token;
    };
})
```

#### 问题2: Token过期后无法自动刷新

**症状**: Token过期后SignalR断开，无法自动恢复

**排查步骤**:
1. 检查RefreshToken是否存储
2. 检查刷新逻辑是否实现
3. 检查Token过期时间配置

**解决方案**:
```csharp
// 实现自动刷新逻辑
public async Task<string?> GetValidTokenAsync()
{
    var token = await GetTokenAsync();
    if (IsTokenExpired(token))
    {
        var refreshToken = await GetRefreshTokenAsync();
        var result = await RefreshTokenAsync(refreshToken);
        if (result.Success)
        {
            return result.Token;
        }
    }
    return token;
}
```

#### 问题3: 密码验证失败

**症状**: 正确的密码无法登录

**排查步骤**:
1. 检查BCrypt哈希是否正确
2. 检查测试账户初始化逻辑
3. 查看日志中的错误信息

**解决方案**:
```csharp
// 确保密码哈希正确
var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password, 12);

// 验证时使用Verify方法
var isValid = BCrypt.Net.BCrypt.Verify(inputPassword, user.PasswordHash);
```

---

## 总结

本设计方案提供了轻量级、易于实施的JWT用户认证系统，专门为BlazorIdle项目的SignalR连接测试设计。

### 关键特性

1. **简单实用**: 内存存储，无需数据库
2. **标准合规**: 遵循JWT和ASP.NET Core最佳实践
3. **安全可靠**: BCrypt密码哈希，JWT签名验证
4. **易于扩展**: 后续可轻松迁移到数据库
5. **完整文档**: 详细的实施指南和代码示例

### 下一步

1. ✅ 阅读本设计方案
2. 🚀 开始阶段一实施（服务端）
3. 🚀 开始阶段二实施（客户端）
4. 🚀 开始阶段三实施（SignalR集成）
5. ✅ 测试和验证
6. 🎉 继续SignalR阶段二（战斗系统集成）

---

**文档状态**: ✅ 完成  
**最后更新**: 2025年10月23日  
**作者**: GitHub Copilot
