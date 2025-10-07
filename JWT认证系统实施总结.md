# JWT 认证系统实施总结

## 概述

本文档总结了 BlazorIdle 项目中 JWT 认证系统的实施情况，包括新增功能、修改内容和使用指南。

## 实施日期
2024年10月7日

## 实施内容

### 1. 新增 NuGet 包

- **Microsoft.AspNetCore.Authentication.JwtBearer** (v9.0.0)
  - 提供 JWT Bearer 认证中间件
  - 用于验证和解析 JWT Token

- **BCrypt.Net-Next** (v4.0.3)
  - 提供 BCrypt 密码哈希算法
  - 确保密码安全存储

### 2. 配置文件更新

**文件**: `BlazorIdle.Server/appsettings.json`

新增 JWT 配置节:
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

⚠️ **安全提示**: 生产环境中必须修改 `SecretKey` 为强随机密钥，并使用环境变量或密钥管理服务存储。

### 3. 程序启动配置

**文件**: `BlazorIdle.Server/Program.cs`

新增内容:
- JWT 认证服务配置
- JWT Token 验证参数设置
- 认证和授权中间件注册
- Swagger JWT 支持配置

关键代码:
```csharp
// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => { /* 配置 */ });

builder.Services.AddAuthorization();
builder.Services.AddScoped<JwtTokenService>();

// Swagger JWT 支持
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", /* ... */);
    options.AddSecurityRequirement(/* ... */);
});

// 中间件
app.UseAuthentication();
app.UseAuthorization();
```

### 4. 新增服务类

**文件**: `BlazorIdle.Server/Application/Auth/JwtTokenService.cs`

功能:
- 生成 JWT Token
- 从 ClaimsPrincipal 提取用户 ID
- 提供 Token 相关工具方法

### 5. 新增 API 控制器

#### AuthController
**文件**: `BlazorIdle.Server/Api/AuthController.cs`

端点:
- `POST /api/auth/register` - 用户注册
- `POST /api/auth/login` - 用户登录（支持用户名或邮箱）
- `POST /api/auth/change-password` - 修改密码

#### UsersController
**文件**: `BlazorIdle.Server/Api/UsersController.cs`

端点（需要认证）:
- `GET /api/users/me` - 获取当前用户信息
- `GET /api/users/{id}` - 获取指定用户信息
- `GET /api/users/{id}/characters` - 获取用户的所有角色
- `PUT /api/users/{id}` - 更新用户信息

### 6. 更新现有控制器

**文件**: `BlazorIdle.Server/Api/CharactersController.cs`

新增功能:
- 创建角色时自动绑定到已认证用户
- `PUT /api/characters/{id}/bind-user` - 绑定角色到用户（需认证）
- `PUT /api/characters/{id}/reorder` - 调整角色 Roster 顺序（需认证）

关键逻辑:
```csharp
// 创建角色时自动绑定
if (User.Identity?.IsAuthenticated == true)
{
    var userId = JwtTokenService.GetUserIdFromClaims(User);
    if (userId != null)
    {
        c.UserId = userId.Value;
        var characterCount = await _db.Characters.CountAsync(ch => ch.UserId == userId.Value);
        c.RosterOrder = characterCount;
    }
}
```

### 7. 新增文档

1. **JWT认证系统文档.md**
   - 完整的 JWT 认证系统说明
   - 配置指南
   - API 端点详细说明
   - 安全考虑
   - 故障排除

2. **API认证示例.md**
   - 实际 API 调用示例（curl 和 JavaScript）
   - 完整工作流示例
   - 错误处理示例

3. **本文档** (JWT认证系统实施总结.md)
   - 实施内容总结
   - 向后兼容性说明

## 向后兼容性

### 设计原则

本实现严格遵循**最小修改**和**向后兼容**原则：

1. **可选认证**: 大部分 API 端点不强制要求认证
2. **现有功能保留**: 未认证用户可以继续创建和使用角色
3. **数据库兼容**: UserId 字段可空，支持现有的未绑定角色
4. **API 签名不变**: 现有 API 端点的参数和返回值保持不变

### 具体实现

#### 角色创建
```csharp
// 认证用户 - 自动绑定
POST /api/characters + Bearer Token
→ 角色自动绑定到用户，分配 RosterOrder

// 未认证用户 - 继续正常工作
POST /api/characters
→ 角色创建成功，UserId = NULL
```

#### 现有 API
- 所有现有战斗、背包、活动计划 API 保持不变
- 不需要认证即可使用
- 不影响现有客户端代码

#### 数据库
- Character 表的 UserId 字段可空
- 现有角色数据不受影响（UserId = NULL）
- 可以随时通过 `/api/characters/{id}/bind-user` 绑定到用户

## 测试结果

### 功能测试

✅ **用户注册**
```bash
POST /api/auth/register
{
  "username": "testuser123",
  "email": "test123@example.com",
  "password": "TestPass123"
}
→ 返回 JWT Token 和用户信息
```

✅ **用户登录**
```bash
POST /api/auth/login
{
  "usernameOrEmail": "testuser123",
  "password": "TestPass123"
}
→ 返回 JWT Token
```

✅ **获取用户信息**
```bash
GET /api/users/me
Authorization: Bearer {token}
→ 返回用户信息和角色列表
```

✅ **角色自动绑定**
```bash
POST /api/characters
Authorization: Bearer {token}
{
  "name": "TestWarrior",
  "profession": 0
}
→ 角色自动绑定到用户，RosterOrder = 0
```

✅ **查询用户角色**
```bash
GET /api/users/{userId}/characters
Authorization: Bearer {token}
→ 返回用户的所有角色（按 RosterOrder 排序）
```

### 安全测试

✅ Token 验证正常工作
✅ 无效 Token 返回 401 Unauthorized
✅ 权限检查正常（用户只能操作自己的数据）
✅ 密码哈希正确（BCrypt）

## 使用指南

### 快速开始

1. **注册用户**
```bash
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"username":"myuser","email":"user@example.com","password":"Pass123"}'
```

2. **登录获取 Token**
```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"usernameOrEmail":"myuser","password":"Pass123"}'
```

3. **使用 Token 访问受保护 API**
```bash
curl -X GET http://localhost:5000/api/users/me \
  -H "Authorization: Bearer {token}"
```

4. **创建角色（自动绑定）**
```bash
curl -X POST http://localhost:5000/api/characters \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {token}" \
  -d '{"name":"MyWarrior","profession":0}'
```

### Swagger UI 使用

1. 访问 `http://localhost:5000/swagger`
2. 点击右上角 "Authorize" 按钮
3. 输入 Token（格式: `Bearer {token}`）
4. 点击 "Authorize" 完成认证
5. 现在可以测试需要认证的 API

## 安全建议

### 生产环境配置

1. **修改 JWT 密钥**
   - 使用至少 32 字符的强随机密钥
   - 使用环境变量: `export JWT_SECRET_KEY="your-strong-secret-key"`
   - 或使用 Azure Key Vault / AWS Secrets Manager

2. **启用 HTTPS**
   - 生产环境必须使用 HTTPS
   - Token 应通过安全通道传输

3. **Token 过期时间**
   - 根据业务需求调整 `ExpirationMinutes`
   - 考虑实现 Refresh Token 机制

4. **密码策略**
   - 实施密码强度检查（最小长度、复杂度）
   - 添加登录失败次数限制
   - 考虑添加验证码

## 未来增强

1. **Token 刷新**: 实现 Refresh Token 避免频繁登录
2. **邮箱验证**: 注册后发送验证邮件
3. **密码重置**: 邮箱验证的密码重置功能
4. **双因素认证**: 增强账号安全
5. **OAuth2 集成**: 支持第三方登录
6. **角色权限**: 实现基于角色的访问控制（RBAC）

## 相关文档

- 📘 [JWT认证系统文档](./docs/JWT认证系统文档.md) - 完整技术文档
- 📦 [API认证示例](./docs/API认证示例.md) - 实际使用示例
- 🔐 [用户系统文档](./docs/用户系统文档.md) - 用户系统设计
- 🚀 [用户系统快速开始](./docs/用户系统快速开始.md) - 快速入门

## 问题反馈

如有问题或建议，请通过以下方式反馈：
- GitHub Issues: https://github.com/Solaireshen97/BlazorIdle/issues
- 项目维护者: Solaireshen97

---

**实施完成**: 2024年10月7日
**版本**: v1.0
**状态**: ✅ 生产就绪（需更新配置）
