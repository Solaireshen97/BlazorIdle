# JWT 认证系统文档

## 概述

本文档说明 BlazorIdle 项目中 JWT (JSON Web Token) 认证系统的设计和使用方法。该系统为用户登录、注册和 API 访问控制提供安全的认证机制。

## 功能特性

1. **用户注册和登录**: 支持用户名/邮箱 + 密码的注册和登录
2. **JWT Token 认证**: 使用行业标准 JWT 进行无状态身份验证
3. **密码安全**: 使用 BCrypt 算法安全存储密码哈希
4. **可选认证**: 保持向后兼容，支持未认证用户访问部分功能
5. **用户-角色绑定**: 已认证用户创建的角色自动绑定到用户账号

## 技术栈

- **Microsoft.AspNetCore.Authentication.JwtBearer**: JWT 认证中间件
- **BCrypt.Net-Next**: 密码哈希算法
- **System.IdentityModel.Tokens.Jwt**: JWT Token 生成和验证

## 配置说明

### appsettings.json

```json
{
  "Jwt": {
    "SecretKey": "YourSecretKeyHere-ChangeThisInProduction-MustBeAtLeast32CharactersLong!",
    "Issuer": "BlazorIdle.Server",
    "Audience": "BlazorIdle.Client",
    "ExpirationMinutes": 1440
  }
}
```

**配置项说明**:
- `SecretKey`: JWT 签名密钥（生产环境必须修改为强密钥，至少 32 字符）
- `Issuer`: Token 签发者标识
- `Audience`: Token 接收者标识
- `ExpirationMinutes`: Token 有效期（分钟），默认 1440 分钟（24小时）

**⚠️ 安全警告**: 生产环境中必须：
1. 修改 `SecretKey` 为强随机密钥
2. 使用环境变量或密钥管理服务存储密钥，不要提交到代码库
3. 启用 HTTPS 确保 Token 传输安全

## API 端点

### 认证端点 (无需授权)

#### 1. 用户注册
```
POST /api/auth/register
Content-Type: application/json

{
  "username": "testuser",
  "email": "test@example.com",
  "password": "SecurePassword123"
}
```

**响应示例**:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "username": "testuser",
  "email": "test@example.com"
}
```

**错误响应**:
- `400 Bad Request`: 用户名或邮箱已存在

#### 2. 用户登录
```
POST /api/auth/login
Content-Type: application/json

{
  "usernameOrEmail": "testuser",  // 支持用户名或邮箱
  "password": "SecurePassword123"
}
```

**响应示例**:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "username": "testuser",
  "email": "test@example.com"
}
```

**错误响应**:
- `401 Unauthorized`: 用户名或密码错误

#### 3. 修改密码
```
POST /api/auth/change-password
Content-Type: application/json

{
  "username": "testuser",
  "oldPassword": "OldPassword123",
  "newPassword": "NewPassword456"
}
```

**响应示例**:
```json
{
  "message": "密码修改成功"
}
```

**错误响应**:
- `404 Not Found`: 用户不存在
- `400 Bad Request`: 旧密码错误

### 用户管理端点 (需要授权)

#### 4. 获取当前用户信息
```
GET /api/users/me
Authorization: Bearer {token}
```

**响应示例**:
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "username": "testuser",
  "email": "test@example.com",
  "createdAt": "2024-01-01T00:00:00Z",
  "lastLoginAt": "2024-01-02T10:30:00Z",
  "characters": [
    {
      "id": "4fa85f64-5717-4562-b3fc-2c963f66afa7",
      "name": "MyWarrior",
      "level": 10,
      "profession": "Warrior"
    }
  ]
}
```

#### 5. 获取指定用户信息
```
GET /api/users/{userId}
Authorization: Bearer {token}
```

#### 6. 获取用户的所有角色
```
GET /api/users/{userId}/characters
Authorization: Bearer {token}
```

#### 7. 更新用户信息
```
PUT /api/users/{userId}
Authorization: Bearer {token}
Content-Type: application/json

{
  "email": "newemail@example.com"
}
```

### 角色管理端点

#### 8. 创建角色 (自动绑定)
```
POST /api/characters
Authorization: Bearer {token}  // 可选，有 token 则自动绑定
Content-Type: application/json

{
  "name": "MyWarrior",
  "profession": "Warrior"
}
```

**说明**: 
- 如果请求包含有效的 JWT Token，角色会自动绑定到当前用户
- 未认证的请求仍然可以创建角色（向后兼容），但不会绑定用户

#### 9. 绑定角色到用户
```
PUT /api/characters/{characterId}/bind-user
Authorization: Bearer {token}
```

**说明**: 将未绑定的角色绑定到当前用户

#### 10. 调整角色顺序
```
PUT /api/characters/{characterId}/reorder
Authorization: Bearer {token}
Content-Type: application/json

{
  "rosterOrder": 2
}
```

## 使用指南

### 客户端认证流程

1. **注册或登录**:
```javascript
// 登录
const response = await fetch('https://localhost:5000/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    usernameOrEmail: 'testuser',
    password: 'SecurePassword123'
  })
});

const data = await response.json();
const token = data.token;

// 存储 Token（使用 localStorage 或 sessionStorage）
localStorage.setItem('jwt_token', token);
```

2. **携带 Token 访问受保护的 API**:
```javascript
const token = localStorage.getItem('jwt_token');

const response = await fetch('https://localhost:5000/api/users/me', {
  headers: {
    'Authorization': `Bearer ${token}`
  }
});

const userData = await response.json();
```

3. **处理 Token 过期**:
```javascript
if (response.status === 401) {
  // Token 过期或无效，需要重新登录
  localStorage.removeItem('jwt_token');
  // 跳转到登录页面
}
```

### 服务端获取当前用户

在需要获取当前用户信息的控制器中：

```csharp
using BlazorIdle.Server.Application.Auth;

[Authorize]
[HttpGet("protected-endpoint")]
public async Task<IActionResult> ProtectedEndpoint()
{
    var userId = JwtTokenService.GetUserIdFromClaims(User);
    if (userId == null)
    {
        return Unauthorized();
    }

    // 使用 userId 查询用户数据
    var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
    // ...
}
```

## 安全考虑

1. **密码存储**:
   - 使用 BCrypt 算法存储密码哈希
   - 每个密码自动加盐，防止彩虹表攻击
   - 永远不存储明文密码

2. **Token 安全**:
   - Token 包含用户 ID、用户名、邮箱等声明
   - Token 使用 HMAC-SHA256 签名，防止篡改
   - Token 有过期时间，默认 24 小时

3. **传输安全**:
   - 生产环境必须使用 HTTPS
   - Token 应通过 Authorization Header 传输，避免 URL 参数

4. **最佳实践**:
   - 不要在客户端代码中硬编码密钥
   - 实现 Token 刷新机制（未来增强）
   - 添加登录失败次数限制（未来增强）
   - 实现邮箱验证（未来增强）

## 向后兼容性

为保持向后兼容，本系统：

1. **可选认证**: 大部分 API 端点不强制要求认证
2. **自动绑定**: 已认证用户创建角色时自动绑定，未认证用户可继续使用原有功能
3. **UserId 可空**: Character 的 UserId 字段可空，支持未绑定用户的角色
4. **现有功能不受影响**: 战斗、背包、活动计划等功能继续正常工作

## 故障排除

### Token 验证失败

**问题**: API 返回 401 Unauthorized

**可能原因**:
1. Token 格式错误（确保使用 `Bearer {token}` 格式）
2. Token 过期（检查 ExpirationMinutes 配置）
3. 密钥不匹配（确保客户端和服务器使用相同的密钥）

**解决方法**:
```bash
# 检查 Token 内容（使用 jwt.io）
# 确认 Token 未过期
# 验证配置文件中的密钥一致性
```

### CORS 问题

**问题**: 浏览器报 CORS 错误

**解决方法**:
确保 `Program.cs` 中的 CORS 配置包含客户端地址：
```csharp
policy.WithOrigins(
    "https://localhost:5001",
    "http://localhost:5001",
    "http://localhost:5000"
)
.AllowAnyHeader()
.AllowAnyMethod();
```

## 未来增强

1. **Token 刷新**: 实现 Refresh Token 机制，避免频繁登录
2. **密码重置**: 实现邮箱验证的密码重置功能
3. **邮箱验证**: 注册后发送验证邮件
4. **登录失败限制**: 防止暴力破解
5. **双因素认证**: 增强账号安全
6. **OAuth2 集成**: 支持第三方登录（Google、GitHub 等）

## 相关文档

- 📘 [用户系统文档](./用户系统文档.md)
- 📦 [用户系统快速开始](./用户系统快速开始.md)
- 🔐 [ASP.NET Core Security](https://docs.microsoft.com/en-us/aspnet/core/security/)
- 🔑 [JWT Best Practices](https://tools.ietf.org/html/rfc8725)
