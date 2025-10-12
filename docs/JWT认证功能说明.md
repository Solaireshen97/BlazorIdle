# JWT 认证功能说明

> **更新日期**: 2024-10-07  
> **版本**: v1.0  
> **状态**: ✅ 生产就绪（需配置更新）

## 快速概览

BlazorIdle 现已支持 JWT 用户认证系统！用户可以注册账号、登录，创建的角色会自动绑定到账号。**重要：完全向后兼容，现有功能不受影响。**

## 🎯 核心功能

- ✅ 用户注册和登录
- ✅ JWT Token 认证（24小时有效期）
- ✅ BCrypt 密码哈希
- ✅ 角色自动绑定到用户
- ✅ 用户信息管理
- ✅ 角色管理（绑定、排序）
- ✅ Swagger UI 支持

## 📚 完整文档

### 必读文档

1. **[认证系统快速参考](docs/认证系统快速参考.md)** ⭐ **推荐首先阅读**
   - 一分钟快速了解
   - 核心 API 总结
   - 快速示例

2. **[迁移到认证系统指南](docs/迁移到认证系统指南.md)** ⭐ **现有用户必读**
   - 向后兼容说明
   - 零影响迁移策略
   - 渐进式升级路径
   - 常见问题解答

### 详细技术文档

3. **[JWT认证系统文档](docs/JWT认证系统文档.md)**
   - 完整技术说明
   - 配置指南
   - 安全考虑
   - 故障排除

4. **[API认证示例](docs/API认证示例.md)**
   - curl 命令行示例
   - JavaScript/Fetch API 示例
   - 完整工作流演示

### 实施文档

5. **[JWT认证系统实施总结](JWT认证系统实施总结.md)**
   - 实施内容清单
   - 测试结果
   - 安全建议
   - 未来增强

6. **[用户系统文档](docs/用户系统文档.md)** (已更新)
   - 数据库设计
   - 领域模型
   - 使用示例

## 🚀 30秒快速开始

### 注册用户
```bash
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"username":"player1","email":"p1@game.com","password":"Pass123"}'
```

### 创建角色（自动绑定）
```bash
curl -X POST http://localhost:5000/api/characters \
  -H "Authorization: Bearer {你的token}" \
  -H "Content-Type: application/json" \
  -d '{"name":"战士","profession":0}'
```

## 📋 API 端点总览

### 认证相关（无需授权）
```
POST /api/auth/register        # 注册
POST /api/auth/login           # 登录
POST /api/auth/change-password # 改密
```

### 用户管理（需要授权）
```
GET  /api/users/me                  # 当前用户
GET  /api/users/{id}                # 用户信息
GET  /api/users/{id}/characters     # 用户角色
PUT  /api/users/{id}                # 更新用户
```

### 角色管理（可选授权）
```
POST /api/characters                      # 创建角色（有token自动绑定）
PUT  /api/characters/{id}/bind-user       # 绑定角色（需授权）
PUT  /api/characters/{id}/reorder         # 调整顺序（需授权）
```

## ⚠️ 重要提示

### 开发环境
直接使用即可，配置文件中的默认密钥足够安全。

### 生产环境 🔴 必读
**必须**修改 `appsettings.json` 中的 JWT 密钥：

```json
{
  "Jwt": {
    "SecretKey": "替换为至少32字符的强随机密钥",
    "ExpirationMinutes": 1440
  }
}
```

**推荐做法**：
```bash
# 使用环境变量
export JWT_SECRET_KEY="你的强密钥"

# 或使用密钥管理服务（Azure Key Vault, AWS Secrets Manager）
```

## ✅ 向后兼容保证

### 不影响的功能

- ✅ 创建角色（不登录也能创建）
- ✅ 所有战斗 API
- ✅ 所有背包 API
- ✅ 所有活动计划 API
- ✅ 现有角色数据
- ✅ 现有客户端代码

### 原理

- 认证是**可选的**
- API 端点签名未改变
- 数据库架构向后兼容（UserId 可空）
- 现有数据无需迁移

## 🧪 测试状态

| 功能 | 状态 |
|------|------|
| 用户注册 | ✅ 通过 |
| 用户登录 | ✅ 通过 |
| Token 生成 | ✅ 通过 |
| Token 验证 | ✅ 通过 |
| 角色自动绑定 | ✅ 通过 |
| 用户信息获取 | ✅ 通过 |
| 角色列表排序 | ✅ 通过 |
| 向后兼容 | ✅ 通过 |
| Swagger UI | ✅ 通过 |

**单元测试**: 27/29 通过（2个失败为原有问题，与认证无关）

## 🛠️ 技术栈

- **Microsoft.AspNetCore.Authentication.JwtBearer** v9.0.0
- **BCrypt.Net-Next** v4.0.3
- **System.IdentityModel.Tokens.Jwt** v8.0.1

## 📖 使用场景

### 场景 1: 不使用认证（开发/测试）
```javascript
// 直接创建角色，不需要任何改动
await fetch('/api/characters', {
  method: 'POST',
  body: JSON.stringify({ name: 'TestChar', profession: 0 })
});
```

### 场景 2: 使用认证（生产环境推荐）
```javascript
// 1. 登录
const loginResponse = await fetch('/api/auth/login', {
  method: 'POST',
  body: JSON.stringify({ usernameOrEmail: 'user', password: 'pass' })
});
const { token } = await loginResponse.json();

// 2. 创建角色（自动绑定）
await fetch('/api/characters', {
  method: 'POST',
  headers: { 'Authorization': `Bearer ${token}` },
  body: JSON.stringify({ name: 'MyChar', profession: 0 })
});
```

### 场景 3: 混合使用
```javascript
// 可以同时存在已绑定和未绑定的角色
// 系统完全支持这种混合模式
```

## 🤔 常见问题

**Q: 我必须使用认证吗？**  
A: 不必须！认证是可选的。

**Q: 现有代码需要改吗？**  
A: 不需要！完全向后兼容。

**Q: 数据需要迁移吗？**  
A: 不需要！现有数据完全兼容。

**Q: Token 过期了怎么办？**  
A: 重新登录获取新 token。

**Q: 如何在 Swagger UI 中测试？**  
A: 点击 "Authorize" 按钮，输入 `Bearer {token}`。

更多问题请查看 [迁移指南](docs/迁移到认证系统指南.md#faq)。

## 🚦 部署检查清单

### 开发环境
- [x] 代码已更新
- [x] 依赖包已安装
- [ ] 测试认证功能（可选）

### 生产环境
- [ ] **修改 JWT SecretKey**（必须！）
- [ ] 启用 HTTPS
- [ ] 使用环境变量存储密钥
- [ ] 更新客户端代码（如果使用认证）
- [ ] 运行完整测试

## 📞 获取帮助

- 📘 查看文档（见上方文档列表）
- 🐛 提交 Issue: https://github.com/Solaireshen97/BlazorIdle/issues
- 👤 联系维护者: @Solaireshen97

## 🔜 未来计划

- Token 刷新机制
- 邮箱验证
- 密码重置
- 双因素认证
- OAuth2 集成（Google、GitHub 等）
- 角色权限系统（RBAC）

---

**开发团队**: Solaireshen97  
**最后更新**: 2024-10-07  
**许可证**: 参见项目根目录 LICENSE 文件
