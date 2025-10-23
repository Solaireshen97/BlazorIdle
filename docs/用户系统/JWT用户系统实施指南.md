# JWT 用户系统实施指南 - 分步实施

**文档版本**: 1.0  
**生成日期**: 2025年10月23日  
**状态**: 实施指导  
**目标**: 提供详细的、可按步骤实施的JWT用户系统开发指南

---

## 📚 目录

1. [实施概览](#实施概览)
2. [阶段一：服务端基础实施](#阶段一服务端基础实施)
3. [阶段二：客户端实施](#阶段二客户端实施)
4. [阶段三：SignalR集成](#阶段三signalr集成)
5. [验收标准](#验收标准)
6. [故障排查](#故障排查)

---

## 实施概览

### 总体时间规划

```
阶段一：服务端基础实施    [██████░░░░] 2-3天
阶段二：客户端实施        [░░░░░███░░] 1-2天
阶段三：SignalR集成       [░░░░░░░░██] 0.5-1天

总计：3.5-6天（约1周）
```

### 依赖关系

```
阶段一（服务端）
    │
    ├──→ 步骤1: 安装依赖和配置
    ├──→ 步骤2: 实现UserStore
    ├──→ 步骤3: 实现AuthService
    ├──→ 步骤4: 实现AuthController
    └──→ 步骤5: 配置JWT认证中间件
           │
           └──→ 阶段二（客户端）
                   │
                   ├──→ 步骤6: 安装客户端依赖
                   ├──→ 步骤7: 实现AuthenticationService
                   └──→ 步骤8: 创建登录页面
                           │
                           └──→ 阶段三（SignalR集成）
                                   │
                                   ├──→ 步骤9: 修改SignalR连接管理
                                   └──→ 步骤10: 端到端测试
```

---

## 阶段一：服务端基础实施

**目标**: 实现服务端JWT认证系统  
**时间**: 2-3天  
**人员**: 1名后端开发

---

### 步骤1：安装依赖和配置（0.5天）✅ 已完成

#### 任务清单

- [x] 安装NuGet包（JWT、BCrypt）
- [x] 创建目录结构
- [x] 配置appsettings.json
- [x] 验证编译

**实施日期**: 2025-10-23  
**完成者**: AI Agent

#### 详细步骤

**1.1 安装NuGet包**

```bash
cd /home/runner/work/BlazorIdle/BlazorIdle/BlazorIdle.Server

# JWT令牌生成和验证
dotnet add package System.IdentityModel.Tokens.Jwt --version 8.2.1

# 密码哈希
dotnet add package BCrypt.Net-Next --version 4.0.3

# JWT Bearer认证
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 9.0.9
```

**1.2 创建目录结构**

```bash
# 创建Auth相关目录
mkdir -p BlazorIdle.Server/Auth/Models
mkdir -p BlazorIdle.Server/Auth/Services
mkdir -p BlazorIdle.Server/Auth/DTOs
mkdir -p BlazorIdle.Server/Api/Controllers
```

**1.3 配置appsettings.json**

修改文件：`BlazorIdle.Server/appsettings.json`

```json
{
  "Jwt": {
    "SecretKey": "这里应该使用至少32字符的强密钥_BlazorIdleJwtSecret2025_请在生产环境使用环境变量",
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
  },
  "SignalR": {
    "EnableDetailedErrors": false,
    "MaximumReceiveMessageSize": 102400,
    "HandshakeTimeoutSeconds": 15,
    "KeepAliveIntervalSeconds": 15,
    "ClientTimeoutSeconds": 30,
    "EnableMessagePackCompression": true
  }
}
```

创建文件：`BlazorIdle.Server/appsettings.Development.json`

```json
{
  "Jwt": {
    "ExpirationMinutes": 1440,
    "SecretKey": "Development_Only_Secret_Key_32_Characters_Minimum"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "BlazorIdle.Server.Auth": "Debug"
    }
  }
}
```

**1.4 验证编译**

```bash
cd /home/runner/work/BlazorIdle/BlazorIdle
dotnet build
```

#### 验收标准

- ✅ 所有NuGet包安装成功（System.IdentityModel.Tokens.Jwt 8.2.1, BCrypt.Net-Next 4.0.3, Microsoft.AspNetCore.Authentication.JwtBearer 9.0.9）
- ✅ 目录结构创建完成（Auth/Models, Auth/Services, Auth/DTOs, Api/Controllers）
- ✅ 配置文件格式正确（appsettings.json和appsettings.Development.json已配置）
- ✅ 项目编译无错误（验证通过）

---

### 步骤2：实现UserStore（0.5天）✅ 已完成

#### 任务清单

- [x] 创建User模型
- [x] 创建UserInfo DTO
- [x] 创建IUserStore接口
- [x] 实现InMemoryUserStore
- [x] 初始化测试账户
- [x] 注册服务

**实施日期**: 2025-10-23  
**完成者**: AI Agent  
**测试结果**: 25个单元测试全部通过

#### 详细步骤

**2.1 创建User模型**

创建文件：`BlazorIdle.Server/Auth/Models/User.cs`

```csharp
namespace BlazorIdle.Server.Auth.Models;

/// <summary>
/// 用户实体模型
/// </summary>
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

**2.2 创建UserInfo DTO**

创建文件：`BlazorIdle.Server/Auth/DTOs/UserInfo.cs`

```csharp
namespace BlazorIdle.Server.Auth.DTOs;

/// <summary>
/// 用户信息DTO（返回给客户端）
/// </summary>
public class UserInfo
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
```

**2.3 创建IUserStore接口**

创建文件：`BlazorIdle.Server/Auth/Services/IUserStore.cs`

```csharp
using BlazorIdle.Server.Auth.Models;

namespace BlazorIdle.Server.Auth.Services;

/// <summary>
/// 用户存储接口
/// </summary>
public interface IUserStore
{
    /// <summary>
    /// 根据用户ID获取用户
    /// </summary>
    Task<User?> GetUserByIdAsync(string userId);
    
    /// <summary>
    /// 根据用户名获取用户
    /// </summary>
    Task<User?> GetUserByUsernameAsync(string username);
    
    /// <summary>
    /// 创建新用户
    /// </summary>
    Task<User> CreateUserAsync(string username, string password);
    
    /// <summary>
    /// 验证用户密码
    /// </summary>
    Task<bool> ValidatePasswordAsync(string username, string password);
    
    /// <summary>
    /// 更新用户最后登录时间
    /// </summary>
    Task UpdateLastLoginAsync(string userId);
    
    /// <summary>
    /// 保存刷新令牌
    /// </summary>
    Task SaveRefreshTokenAsync(string userId, string refreshToken, DateTime expiresAt);
    
    /// <summary>
    /// 验证刷新令牌
    /// </summary>
    Task<User?> ValidateRefreshTokenAsync(string refreshToken);
    
    /// <summary>
    /// 获取所有用户（仅供测试）
    /// </summary>
    Task<IEnumerable<User>> GetAllUsersAsync();
}
```

**2.4 实现InMemoryUserStore**

创建文件：`BlazorIdle.Server/Auth/Services/InMemoryUserStore.cs`

```csharp
using System.Collections.Concurrent;
using BlazorIdle.Server.Auth.Models;

namespace BlazorIdle.Server.Auth.Services;

/// <summary>
/// 基于内存的用户存储实现
/// 用于开发和测试，数据在服务重启后会丢失
/// </summary>
public class InMemoryUserStore : IUserStore
{
    private readonly ConcurrentDictionary<string, User> _usersById = new();
    private readonly ConcurrentDictionary<string, string> _usernameToId = new();
    private readonly ConcurrentDictionary<string, string> _refreshTokenToUserId = new();
    private readonly ILogger<InMemoryUserStore> _logger;

    public InMemoryUserStore(ILogger<InMemoryUserStore> logger)
    {
        _logger = logger;
        InitializeTestAccounts();
    }

    /// <summary>
    /// 初始化测试账户
    /// </summary>
    private void InitializeTestAccounts()
    {
        _logger.LogInformation("初始化测试账户...");
        
        // 创建3个测试账户
        CreateTestUser("test1", "password123");
        CreateTestUser("test2", "password123");
        CreateTestUser("admin", "admin123");
        
        _logger.LogInformation("测试账户初始化完成，共 {Count} 个账户", _usersById.Count);
    }

    /// <summary>
    /// 创建测试用户
    /// </summary>
    private void CreateTestUser(string username, string password)
    {
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 12),
            CreatedAt = DateTime.UtcNow
        };

        _usersById[user.Id] = user;
        _usernameToId[username.ToLowerInvariant()] = user.Id;
        
        _logger.LogDebug("创建测试账户：{Username} (ID: {UserId})", username, user.Id);
    }

    public Task<User?> GetUserByIdAsync(string userId)
    {
        _usersById.TryGetValue(userId, out var user);
        return Task.FromResult(user);
    }

    public Task<User?> GetUserByUsernameAsync(string username)
    {
        var normalizedUsername = username.ToLowerInvariant();
        if (_usernameToId.TryGetValue(normalizedUsername, out var userId))
        {
            _usersById.TryGetValue(userId, out var user);
            return Task.FromResult(user);
        }
        return Task.FromResult<User?>(null);
    }

    public Task<User> CreateUserAsync(string username, string password)
    {
        var normalizedUsername = username.ToLowerInvariant();
        
        // 检查用户名是否已存在
        if (_usernameToId.ContainsKey(normalizedUsername))
        {
            throw new InvalidOperationException($"用户名 '{username}' 已存在");
        }

        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 12),
            CreatedAt = DateTime.UtcNow
        };

        _usersById[user.Id] = user;
        _usernameToId[normalizedUsername] = user.Id;
        
        _logger.LogInformation("创建新用户：{Username} (ID: {UserId})", username, user.Id);
        
        return Task.FromResult(user);
    }

    public async Task<bool> ValidatePasswordAsync(string username, string password)
    {
        var user = await GetUserByUsernameAsync(username);
        if (user == null)
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "密码验证失败：{Username}", username);
            return false;
        }
    }

    public Task UpdateLastLoginAsync(string userId)
    {
        if (_usersById.TryGetValue(userId, out var user))
        {
            user.LastLoginAt = DateTime.UtcNow;
            _logger.LogDebug("更新用户 {UserId} 的最后登录时间", userId);
        }
        return Task.CompletedTask;
    }

    public Task SaveRefreshTokenAsync(string userId, string refreshToken, DateTime expiresAt)
    {
        if (_usersById.TryGetValue(userId, out var user))
        {
            // 移除旧的刷新令牌映射
            if (!string.IsNullOrEmpty(user.RefreshToken))
            {
                _refreshTokenToUserId.TryRemove(user.RefreshToken, out _);
            }

            // 保存新的刷新令牌
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiresAt = expiresAt;
            _refreshTokenToUserId[refreshToken] = userId;
            
            _logger.LogDebug("保存用户 {UserId} 的刷新令牌", userId);
        }
        return Task.CompletedTask;
    }

    public Task<User?> ValidateRefreshTokenAsync(string refreshToken)
    {
        if (_refreshTokenToUserId.TryGetValue(refreshToken, out var userId))
        {
            if (_usersById.TryGetValue(userId, out var user))
            {
                // 检查刷新令牌是否过期
                if (user.RefreshTokenExpiresAt.HasValue && 
                    user.RefreshTokenExpiresAt.Value > DateTime.UtcNow)
                {
                    return Task.FromResult<User?>(user);
                }
                
                _logger.LogWarning("用户 {UserId} 的刷新令牌已过期", userId);
            }
        }
        
        return Task.FromResult<User?>(null);
    }

    public Task<IEnumerable<User>> GetAllUsersAsync()
    {
        return Task.FromResult<IEnumerable<User>>(_usersById.Values.ToList());
    }
}
```

**2.5 注册服务**

修改文件：`BlazorIdle.Server/Program.cs`

在`builder.Services.AddInfrastructure(...)`之后添加：

```csharp
// 注册用户存储服务
builder.Services.AddSingleton<IUserStore, InMemoryUserStore>();
```

#### 验收标准

- ✅ User模型创建完成（包含详细的中文注释）
- ✅ IUserStore接口定义完成（包含详细的中文注释）
- ✅ InMemoryUserStore实现完成（包含详细的中文注释和实现说明）
- ✅ 测试账户自动初始化（test1、test2、admin三个账户）
- ✅ 服务注册成功（已在Program.cs中注册为Singleton）
- ✅ 项目编译无错误（验证通过）
- ✅ 单元测试完成（25个测试全部通过，覆盖所有主要功能）

---

### 步骤3：实现AuthService（1天）

#### 任务清单

- [ ] 创建JwtOptions配置类
- [ ] 创建AuthResult和相关DTOs
- [ ] 创建IAuthService接口
- [ ] 实现AuthService（JWT生成和验证）
- [ ] 注册服务
- [ ] 编写单元测试

#### 详细步骤

**3.1 创建JwtOptions配置类**

创建文件：`BlazorIdle.Server/Auth/JwtOptions.cs`

```csharp
namespace BlazorIdle.Server.Auth;

/// <summary>
/// JWT配置选项
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// JWT签名密钥（至少32字符）
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// JWT发行者
    /// </summary>
    public string Issuer { get; set; } = "BlazorIdleServer";

    /// <summary>
    /// JWT受众
    /// </summary>
    public string Audience { get; set; } = "BlazorIdleClient";

    /// <summary>
    /// JWT令牌过期时间（分钟）
    /// </summary>
    public int ExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// 刷新令牌过期时间（天）
    /// </summary>
    public int RefreshTokenExpirationDays { get; set; } = 7;

    /// <summary>
    /// 验证配置有效性
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SecretKey))
            throw new InvalidOperationException("JWT SecretKey 不能为空");

        if (SecretKey.Length < 32)
            throw new InvalidOperationException("JWT SecretKey 长度至少32字符");

        if (string.IsNullOrWhiteSpace(Issuer))
            throw new InvalidOperationException("JWT Issuer 不能为空");

        if (string.IsNullOrWhiteSpace(Audience))
            throw new InvalidOperationException("JWT Audience 不能为空");

        if (ExpirationMinutes <= 0)
            throw new InvalidOperationException("JWT ExpirationMinutes 必须大于0");

        if (RefreshTokenExpirationDays <= 0)
            throw new InvalidOperationException("JWT RefreshTokenExpirationDays 必须大于0");
    }
}
```

**3.2 创建AuthResult和DTOs**

创建文件：`BlazorIdle.Server/Auth/DTOs/AuthResult.cs`

```csharp
namespace BlazorIdle.Server.Auth.DTOs;

/// <summary>
/// 认证结果
/// </summary>
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

创建文件：`BlazorIdle.Server/Auth/DTOs/LoginRequest.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace BlazorIdle.Server.Auth.DTOs;

/// <summary>
/// 登录请求
/// </summary>
public class LoginRequest
{
    [Required(ErrorMessage = "用户名不能为空")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "密码不能为空")]
    [MinLength(6, ErrorMessage = "密码至少6位")]
    public string Password { get; set; } = string.Empty;
}
```

创建文件：`BlazorIdle.Server/Auth/DTOs/RegisterRequest.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace BlazorIdle.Server.Auth.DTOs;

/// <summary>
/// 注册请求
/// </summary>
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

创建文件：`BlazorIdle.Server/Auth/DTOs/RefreshTokenRequest.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace BlazorIdle.Server.Auth.DTOs;

/// <summary>
/// 刷新令牌请求
/// </summary>
public class RefreshTokenRequest
{
    [Required(ErrorMessage = "刷新令牌不能为空")]
    public string RefreshToken { get; set; } = string.Empty;
}
```

**3.3 创建IAuthService接口**

创建文件：`BlazorIdle.Server/Auth/Services/IAuthService.cs`

```csharp
using System.Security.Claims;
using BlazorIdle.Server.Auth.DTOs;
using BlazorIdle.Server.Auth.Models;

namespace BlazorIdle.Server.Auth.Services;

/// <summary>
/// 认证服务接口
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// 用户登录
    /// </summary>
    Task<AuthResult> LoginAsync(string username, string password);

    /// <summary>
    /// 用户注册
    /// </summary>
    Task<AuthResult> RegisterAsync(string username, string password);

    /// <summary>
    /// 刷新令牌
    /// </summary>
    Task<AuthResult> RefreshTokenAsync(string refreshToken);

    /// <summary>
    /// 生成JWT令牌
    /// </summary>
    string GenerateJwtToken(User user);

    /// <summary>
    /// 生成刷新令牌
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// 验证JWT令牌
    /// </summary>
    ClaimsPrincipal? ValidateToken(string token);
}
```

**3.4 实现AuthService**

创建文件：`BlazorIdle.Server/Auth/Services/AuthService.cs`

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using BlazorIdle.Server.Auth.DTOs;
using BlazorIdle.Server.Auth.Models;

namespace BlazorIdle.Server.Auth.Services;

/// <summary>
/// 认证服务实现
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserStore _userStore;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserStore userStore,
        JwtOptions jwtOptions,
        ILogger<AuthService> logger)
    {
        _userStore = userStore;
        _jwtOptions = jwtOptions;
        _logger = logger;
    }

    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        try
        {
            // 验证用户名和密码
            var isValid = await _userStore.ValidatePasswordAsync(username, password);
            if (!isValid)
            {
                _logger.LogWarning("登录失败：用户名或密码错误 - {Username}", username);
                return new AuthResult
                {
                    Success = false,
                    Message = "用户名或密码错误"
                };
            }

            // 获取用户信息
            var user = await _userStore.GetUserByUsernameAsync(username);
            if (user == null)
            {
                _logger.LogError("用户验证成功但无法获取用户信息：{Username}", username);
                return new AuthResult
                {
                    Success = false,
                    Message = "登录失败"
                };
            }

            // 生成JWT令牌
            var token = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();
            var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes);
            var refreshTokenExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays);

            // 保存刷新令牌
            await _userStore.SaveRefreshTokenAsync(user.Id, refreshToken, refreshTokenExpiresAt);

            // 更新最后登录时间
            await _userStore.UpdateLastLoginAsync(user.Id);

            _logger.LogInformation("用户登录成功：{Username} (ID: {UserId})", username, user.Id);

            return new AuthResult
            {
                Success = true,
                Token = token,
                RefreshToken = refreshToken,
                ExpiresAt = expiresAt,
                User = new UserInfo
                {
                    Id = user.Id,
                    Username = user.Username,
                    CreatedAt = user.CreatedAt,
                    LastLoginAt = DateTime.UtcNow
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "登录过程中发生错误：{Username}", username);
            return new AuthResult
            {
                Success = false,
                Message = "登录失败，请稍后重试"
            };
        }
    }

    public async Task<AuthResult> RegisterAsync(string username, string password)
    {
        try
        {
            // 检查用户名是否已存在
            var existingUser = await _userStore.GetUserByUsernameAsync(username);
            if (existingUser != null)
            {
                _logger.LogWarning("注册失败：用户名已存在 - {Username}", username);
                return new AuthResult
                {
                    Success = false,
                    Message = "用户名已存在"
                };
            }

            // 创建新用户
            var user = await _userStore.CreateUserAsync(username, password);

            // 生成JWT令牌
            var token = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();
            var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes);
            var refreshTokenExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays);

            // 保存刷新令牌
            await _userStore.SaveRefreshTokenAsync(user.Id, refreshToken, refreshTokenExpiresAt);

            // 更新最后登录时间
            await _userStore.UpdateLastLoginAsync(user.Id);

            _logger.LogInformation("用户注册成功：{Username} (ID: {UserId})", username, user.Id);

            return new AuthResult
            {
                Success = true,
                Token = token,
                RefreshToken = refreshToken,
                ExpiresAt = expiresAt,
                User = new UserInfo
                {
                    Id = user.Id,
                    Username = user.Username,
                    CreatedAt = user.CreatedAt,
                    LastLoginAt = DateTime.UtcNow
                }
            };
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "注册失败：{Username}", username);
            return new AuthResult
            {
                Success = false,
                Message = ex.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "注册过程中发生错误：{Username}", username);
            return new AuthResult
            {
                Success = false,
                Message = "注册失败，请稍后重试"
            };
        }
    }

    public async Task<AuthResult> RefreshTokenAsync(string refreshToken)
    {
        try
        {
            // 验证刷新令牌
            var user = await _userStore.ValidateRefreshTokenAsync(refreshToken);
            if (user == null)
            {
                _logger.LogWarning("刷新令牌无效或已过期");
                return new AuthResult
                {
                    Success = false,
                    Message = "刷新令牌无效或已过期"
                };
            }

            // 生成新的JWT令牌和刷新令牌
            var newToken = GenerateJwtToken(user);
            var newRefreshToken = GenerateRefreshToken();
            var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes);
            var refreshTokenExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays);

            // 保存新的刷新令牌
            await _userStore.SaveRefreshTokenAsync(user.Id, newRefreshToken, refreshTokenExpiresAt);

            _logger.LogInformation("令牌刷新成功：用户 {UserId}", user.Id);

            return new AuthResult
            {
                Success = true,
                Token = newToken,
                RefreshToken = newRefreshToken,
                ExpiresAt = expiresAt,
                User = new UserInfo
                {
                    Id = user.Id,
                    Username = user.Username,
                    CreatedAt = user.CreatedAt,
                    LastLoginAt = user.LastLoginAt
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刷新令牌过程中发生错误");
            return new AuthResult
            {
                Success = false,
                Message = "刷新令牌失败，请重新登录"
            };
        }
    }

    public string GenerateJwtToken(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtOptions.SecretKey);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _jwtOptions.Issuer,
                ValidAudience = _jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JWT令牌验证失败");
            return null;
        }
    }
}
```

**3.5 注册服务**

修改文件：`BlazorIdle.Server/Program.cs`

在用户存储服务注册之后添加：

```csharp
// 配置JWT选项
var jwtOptions = new JwtOptions();
builder.Configuration.GetSection(JwtOptions.SectionName).Bind(jwtOptions);
jwtOptions.Validate();
builder.Services.AddSingleton(jwtOptions);

// 注册认证服务
builder.Services.AddScoped<IAuthService, AuthService>();
```

#### 验收标准

- ✅ JwtOptions配置类创建完成
- ✅ 所有DTOs创建完成
- ✅ IAuthService接口定义完成
- ✅ AuthService实现完成
- ✅ JWT令牌生成正确
- ✅ 刷新令牌生成正确
- ✅ 服务注册成功
- ✅ 项目编译无错误

---

(继续在下一部分...)
