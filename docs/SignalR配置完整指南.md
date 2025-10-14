# SignalR 配置完整指南

**文档版本**: 1.0  
**最后更新**: 2025-10-14  
**状态**: 最新

---

## 📋 概述

本文档详细说明 BlazorIdle 项目中 SignalR 实时通信系统的配置方法、最佳实践和故障排查指南。

---

## 🗂️ 配置文件结构

### 服务端配置
位置：`BlazorIdle.Server/appsettings.json`

```
BlazorIdle.Server/
├── appsettings.json                    # 基础配置
├── appsettings.Development.json        # 开发环境配置（可选）
└── appsettings.Production.json         # 生产环境配置（推荐）
```

### 客户端配置
位置：`BlazorIdle/wwwroot/`

```
BlazorIdle/wwwroot/
├── appsettings.json                    # 主配置文件
└── config/
    ├── README.md                       # 配置说明
    ├── signalr.json                    # SignalR 基础配置
    ├── signalr.Development.json        # 开发环境配置
    └── signalr.Production.json         # 生产环境配置
```

---

## ⚙️ 服务端配置详解

### 完整配置示例

```json
{
  "SignalR": {
    "HubEndpoint": "/hubs/battle",
    "EnableSignalR": true,
    "MaxReconnectAttempts": 5,
    "ReconnectBaseDelayMs": 1000,
    "MaxReconnectDelayMs": 30000,
    "EnableDetailedLogging": false,
    "ConnectionTimeoutSeconds": 30,
    "KeepAliveIntervalSeconds": 15,
    "ServerTimeoutSeconds": 30,
    "Notification": {
      "EnablePlayerDeathNotification": true,
      "EnablePlayerReviveNotification": true,
      "EnableEnemyKilledNotification": true,
      "EnableTargetSwitchedNotification": true,
      "EnableWaveSpawnNotification": false,
      "EnableSkillCastNotification": false,
      "EnableBuffChangeNotification": false
    },
    "Performance": {
      "EnableThrottling": false,
      "ThrottleWindowMs": 1000,
      "EnableBatching": false,
      "BatchDelayMs": 100,
      "AutoDegradeOnMobile": false
    }
  }
}
```

### 配置项说明

#### 基础连接配置

| 配置项 | 类型 | 默认值 | 说明 | 推荐值 |
|--------|------|--------|------|--------|
| `HubEndpoint` | string | "/hubs/battle" | Hub 端点路径 | 保持默认 |
| `EnableSignalR` | bool | true | 是否启用 SignalR | true |
| `ConnectionTimeoutSeconds` | int | 30 | 连接超时（秒） | 30-60 |
| `KeepAliveIntervalSeconds` | int | 15 | 保活间隔（秒） | 10-20 |
| `ServerTimeoutSeconds` | int | 30 | 服务器超时（秒） | 30-60 |

#### 重连配置

| 配置项 | 类型 | 默认值 | 说明 | 推荐值 |
|--------|------|--------|------|--------|
| `MaxReconnectAttempts` | int | 5 | 最大重连次数 | 开发:5, 生产:10 |
| `ReconnectBaseDelayMs` | int | 1000 | 基础重连延迟（毫秒） | 1000 |
| `MaxReconnectDelayMs` | int | 30000 | 最大重连延迟（毫秒） | 开发:30000, 生产:60000 |

#### 日志配置

| 配置项 | 类型 | 默认值 | 说明 | 推荐值 |
|--------|------|--------|------|--------|
| `EnableDetailedLogging` | bool | false | 是否启用详细日志 | 开发:true, 生产:false |

#### 通知配置

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| `EnablePlayerDeathNotification` | bool | true | 玩家死亡通知 |
| `EnablePlayerReviveNotification` | bool | true | 玩家复活通知 |
| `EnableEnemyKilledNotification` | bool | true | 敌人击杀通知 |
| `EnableTargetSwitchedNotification` | bool | true | 目标切换通知 |
| `EnableWaveSpawnNotification` | bool | false | 波次刷新通知（预留） |
| `EnableSkillCastNotification` | bool | false | 技能施放通知（预留） |
| `EnableBuffChangeNotification` | bool | false | Buff变化通知（预留） |

#### 性能配置

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| `EnableThrottling` | bool | false | 是否启用节流 |
| `ThrottleWindowMs` | int | 1000 | 节流窗口（毫秒） |
| `EnableBatching` | bool | false | 是否启用批量发送 |
| `BatchDelayMs` | int | 100 | 批量延迟（毫秒） |
| `AutoDegradeOnMobile` | bool | false | 移动端自动降级 |

---

## 💻 客户端配置详解

### 完整配置示例

```json
{
  "ApiBaseUrl": "https://localhost:7056",
  "SignalR": {
    "HubEndpoint": "/hubs/battle",
    "EnableSignalR": true,
    "MaxReconnectAttempts": 5,
    "ReconnectBaseDelayMs": 1000,
    "MaxReconnectDelayMs": 30000,
    "ConnectionTimeoutSeconds": 30,
    "EnableDetailedLogging": false,
    "KeepAliveIntervalSeconds": 15,
    "ServerTimeoutSeconds": 30,
    "AutoReconnect": true,
    "ConnectionStatusNotifications": true
  }
}
```

### 新增配置项

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| `AutoReconnect` | bool | true | 是否自动重连 |
| `ConnectionStatusNotifications` | bool | true | 是否显示连接状态通知 |

---

## 🌍 环境特定配置

### 开发环境配置

**服务端** (`appsettings.Development.json`):
```json
{
  "SignalR": {
    "EnableDetailedLogging": true,
    "Notification": {
      "EnableWaveSpawnNotification": true,
      "EnableSkillCastNotification": true,
      "EnableBuffChangeNotification": true
    }
  }
}
```

**客户端** (`signalr.Development.json`):
```json
{
  "SignalR": {
    "EnableDetailedLogging": true,
    "ConnectionStatusNotifications": true
  }
}
```

### 生产环境配置

**服务端** (`appsettings.Production.json`):
```json
{
  "SignalR": {
    "EnableDetailedLogging": false,
    "MaxReconnectAttempts": 10,
    "MaxReconnectDelayMs": 60000,
    "Performance": {
      "EnableThrottling": true,
      "ThrottleWindowMs": 1000,
      "AutoDegradeOnMobile": true
    }
  }
}
```

**客户端** (`signalr.Production.json`):
```json
{
  "SignalR": {
    "EnableDetailedLogging": false,
    "ConnectionStatusNotifications": false,
    "MaxReconnectAttempts": 10,
    "MaxReconnectDelayMs": 60000
  }
}
```

---

## 🔐 认证配置

### JWT 认证必需配置

**服务端 Program.cs**:
```csharp
// 1. CORS 必须允许凭据
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        policy
            .WithOrigins("https://localhost:5001", ...)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // ⚠️ SignalR JWT 认证必需
    });
});

// 2. JWT Bearer 必须支持查询字符串 Token
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // ... 其他配置
        
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
```

### 关键要点

1. **CORS AllowCredentials 是必须的**
   - SignalR 需要通过 Cookie 或 Authorization Header 传递 Token
   - 没有此配置会导致连接失败

2. **JWT Token 传递方式**
   - SignalR 使用查询字符串 `?access_token=xxx` 传递 Token
   - 必须在 `OnMessageReceived` 事件中处理

3. **Hub 授权**
   - Hub 类必须添加 `[Authorize]` 属性
   - Hub 方法可以使用 `Context.User` 获取用户信息

---

## 🔄 重连策略

### 指数退避算法

客户端采用指数退避重连策略：

```
重连次数  |  延迟时间
---------|----------
  1      |  1秒
  2      |  2秒
  3      |  4秒
  4      |  8秒
  5      |  16秒
  6      |  32秒（受MaxReconnectDelayMs限制）
```

### 配置示例

```json
{
  "SignalR": {
    "MaxReconnectAttempts": 5,
    "ReconnectBaseDelayMs": 1000,
    "MaxReconnectDelayMs": 30000
  }
}
```

### 重连流程

```
连接断开 → 等待延迟 → 尝试重连 → 成功？
                              ↓ 否
                         增加延迟 → 重试
                              ↓ 达到最大次数
                         停止重连 → 降级到轮询模式
```

---

## 📊 性能优化

### 节流配置

```json
{
  "SignalR": {
    "Performance": {
      "EnableThrottling": true,
      "ThrottleWindowMs": 1000
    }
  }
}
```

**效果**：同一事件在 1000ms 内只发送一次，避免高频通知

### 批量发送

```json
{
  "SignalR": {
    "Performance": {
      "EnableBatching": true,
      "BatchDelayMs": 100
    }
  }
}
```

**效果**：将 100ms 内的多个事件合并发送，减少网络请求

### 移动端降级

```json
{
  "SignalR": {
    "Performance": {
      "AutoDegradeOnMobile": true
    }
  }
}
```

**效果**：移动端自动禁用某些高频通知，降低电量消耗

---

## 🐛 故障排查

### 常见问题

#### 1. 连接失败：401 Unauthorized

**症状**：
- 浏览器控制台显示 401 错误
- SignalR 无法建立连接

**原因**：
- CORS 未配置 `AllowCredentials()`
- JWT Token 未正确传递

**解决方案**：
```csharp
// 检查 Program.cs
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        policy
            .WithOrigins(...)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // ⚠️ 确保添加此行
    });
});

// 检查 JWT Bearer Events
options.Events = new JwtBearerEvents
{
    OnMessageReceived = context =>
    {
        var accessToken = context.Request.Query["access_token"];
        // ... 处理逻辑
    }
};
```

#### 2. 连接建立但无法订阅

**症状**：
- SignalR 显示"已连接"
- 但调用 `SubscribeBattle` 失败

**原因**：
- Hub 方法未授权
- battleId 参数错误

**解决方案**：
```csharp
// 确保 Hub 有 [Authorize] 属性
[Authorize]
public sealed class BattleNotificationHub : Hub
{
    // ...
}

// 检查客户端调用
await SignalRService.SubscribeBattleAsync(battleId);
```

#### 3. 重连失败

**症状**：
- 连接断开后无法自动重连
- 显示"已断开"状态

**原因**：
- 重连次数已达上限
- Token 已过期

**解决方案**：
```json
// 增加重连次数
{
  "SignalR": {
    "MaxReconnectAttempts": 10,
    "MaxReconnectDelayMs": 60000
  }
}
```

### 调试技巧

#### 1. 启用详细日志

```json
{
  "SignalR": {
    "EnableDetailedLogging": true
  }
}
```

#### 2. 浏览器控制台

- 打开浏览器开发者工具
- 查看 Console 标签页
- 搜索 "SignalR" 关键字

#### 3. 网络监控

- 打开浏览器开发者工具
- 查看 Network 标签页
- 过滤 WebSocket 连接
- 检查连接状态和消息

---

## 📚 最佳实践

### 1. 配置管理

✅ **推荐**：
- 使用环境特定配置文件
- 敏感信息（如密钥）存储在环境变量或密钥管理服务中
- 定期审查和更新配置

❌ **避免**：
- 在代码中硬编码配置
- 将生产环境配置提交到版本控制

### 2. 错误处理

✅ **推荐**：
- 始终处理连接失败情况
- 实现降级策略（轮询模式）
- 向用户显示友好的错误提示

❌ **避免**：
- 忽略连接错误
- 假设 SignalR 始终可用

### 3. 性能优化

✅ **推荐**：
- 生产环境启用节流和批量发送
- 只订阅需要的事件类型
- 及时取消不需要的订阅

❌ **避免**：
- 订阅过多不需要的事件
- 频繁建立和断开连接

### 4. 监控

✅ **推荐**：
- 监控连接成功率
- 记录重连频率
- 跟踪通知延迟

❌ **避免**：
- 不监控 SignalR 健康状态
- 忽略异常日志

---

## 🔗 相关文档

- [SignalR前端集成方案.md](./SignalR前端集成方案.md) - 集成指南
- [SignalR优化进度更新.md](./SignalR优化进度更新.md) - 进度跟踪
- [SignalR配置优化指南.md](./SignalR配置优化指南.md) - 配置优化
- [wwwroot/config/README.md](../BlazorIdle/wwwroot/config/README.md) - 客户端配置

---

**维护者**: GitHub Copilot Agent  
**联系方式**: 项目 Issue 或 Pull Request
